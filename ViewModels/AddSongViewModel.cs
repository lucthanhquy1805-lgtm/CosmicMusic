using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // 👇 BỔ SUNG: Dùng để gửi thư yêu cầu phát nhạc
using CosmicMusic.Models;
using CosmicMusic.Services;
using System.Collections.ObjectModel;
using System.Text.Json;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace CosmicMusic.ViewModels
{
    // 👇 BỔ SUNG 1: Bức thư yêu cầu Nhạc trưởng (AudioViewModel) phát nhạc ngay lập tức
    public class PlayRequestedMessage
    {
        public Song SongToPlay { get; set; }
        public PlayRequestedMessage(Song song) => SongToPlay = song;
    }

    public partial class AddSongViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private readonly S3Service _s3Service;
        private readonly HttpClient _httpClient;

        [ObservableProperty]
        private string _searchText;

        [ObservableProperty]
        private bool _isLoading;

        public ObservableCollection<Song> ApiSearchResults { get; set; } = new();

        public AddSongViewModel(FirestoreService firestoreService, S3Service s3Service)
        {
            _firestoreService = firestoreService;
            _s3Service = s3Service;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        }

        // ==========================================
        // NHỊP 1: TÌM KIẾM THÔNG TIN TỪ APPLE ITUNES
        // ==========================================
        [RelayCommand]
        public async Task SearchApi()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;

            MainThread.BeginInvokeOnMainThread(() => {
                IsLoading = true;
                ApiSearchResults.Clear();
            });

            try
            {
                string url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(SearchText)}&entity=song&limit=15";
                var response = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var results = doc.RootElement.GetProperty("results").EnumerateArray();

                // 👇 BỔ SUNG 2: QUÉT FIREBASE - Lấy danh sách nhạc ĐÃ CÓ trong hệ thống để so sánh
                var existingSongs = await _firestoreService.GetAllSongsAsync();

                var tempSongs = new List<Song>();
                foreach (var item in results)
                {
                    string title = item.TryGetProperty("trackName", out var t) ? t.GetString() : "Unknown";
                    string artist = item.TryGetProperty("artistName", out var a) ? a.GetString() : "Unknown";

                    // 👇 Apple trả về Thể loại ở biến primaryGenreName (Ví dụ: "K-Pop", "Hip-Hop/Rap")
                    string rawGenre = item.TryGetProperty("primaryGenreName", out var g) ? g.GetString() : "Pop";

                    // Chuyển "K-Pop" thành "genre_kpop" cho chuẩn định dạng ID của bạn
                    string formattedGenreId = "genre_" + rawGenre.ToLower().Replace(" ", "").Replace("-", "").Replace("/", "");

                    bool isAlreadyAdded = existingSongs.Any(x =>
                        x.Title.Equals(title, StringComparison.OrdinalIgnoreCase) &&
                        x.Artist.Equals(artist, StringComparison.OrdinalIgnoreCase));

                    tempSongs.Add(new Song
                    {
                        Title = title,
                        Artist = artist,
                        GenreId = rawGenre, // 👈 Truyền Thể Loại vào đây!
                        CoverImage = item.TryGetProperty("artworkUrl100", out var c) ? c.GetString()?.Replace("100x100bb", "600x600bb") : "https://via.placeholder.com/600",
                        IsPremium = false,
                        IsAdded = isAlreadyAdded
                    });
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    foreach (var s in tempSongs) ApiSearchResults.Add(s);
                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () => {
                    IsLoading = false;
                    await Shell.Current.DisplayAlert("Lỗi", "Lỗi mạng: " + ex.Message, "OK");
                });
            }
        }

        // ==========================================
        // NHỊP 2: TỰ ĐỘNG LẤY NHẠC TỪ YOUTUBE -> UP LÊN S3
        // ==========================================
       
        [RelayCommand]
        public async Task SelectAndUploadSong(Song selectedSong)
        {
            if (selectedSong == null || selectedSong.IsAdded) return; // Khóa chặn thao tác nhấn đúp

            bool confirm = await Shell.Current.DisplayAlert(
                "Tự động tải nhạc",
                $"Hệ thống sẽ tải bản Audio chuẩn nhất của '{selectedSong.Title}' lên kho nhạc. Bắt đầu ngay?",
                "Tải Tự Động", "Hủy");

            if (!confirm) return;

            IsLoading = true;

            // QUAN TRỌNG: Ném toàn bộ tác vụ mạng sang Luồng Ngầm (Background Thread)
            await Task.Run(async () =>
            {
                try
                {
                    var youtube = new YoutubeClient();
                    string searchQuery = $"{selectedSong.Title} {selectedSong.Artist} official audio";

                    // 1. Tìm video trên YouTube
                    var searchResults = await youtube.Search.GetVideosAsync(searchQuery).CollectAsync(1);
                    var video = searchResults.FirstOrDefault();

                    if (video == null)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                            await Shell.Current.DisplayAlert("Thất bại", "Không tìm thấy luồng âm thanh phù hợp.", "OK"));
                        return;
                    }

                    // 👇 BẢO BỐI 1: Tự động điền số giây bài hát nếu bị thiếu
                    if (selectedSong.Duration <= 0 && video.Duration.HasValue)
                    {
                        selectedSong.Duration = video.Duration.Value.TotalSeconds;
                    }

                    // 2. Lấy luồng âm thanh
                    var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
                    var audioStreams = streamManifest.GetAudioOnlyStreams();

                    // 👇 BẢO BỐI 2: Lọc siêu chuẩn, tuyệt đối từ chối file WebM gây lỗi ExoPlayer
                    var validStreams = audioStreams.Where(s => s.Container.Name.ToLower().Contains("mp4") || s.Container.Name.ToLower().Contains("m4a")).ToList();

                    var audioStreamInfo = validStreams.Any()
                        ? validStreams.GetWithHighestBitrate()
                        : null;

                    if (audioStreamInfo == null)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                            await Shell.Current.DisplayAlert("Lỗi Định Dạng", "Bài hát này bị YouTube khóa định dạng chuẩn. Hãy thử tìm một bài khác.", "OK"));
                        return;
                    }

                    // 👇 BẢO BỐI 3: Ép đuôi file thành .m4a để Android/iOS nhận diện 100% là Nhạc
                    string ext = "m4a";
                    string tempFilePath = Path.Combine(FileSystem.CacheDirectory, $"temp_{Guid.NewGuid()}.{ext}");

                    try
                    {
                        // 3. Tải file từ YouTube xuống ổ cứng máy ảo
                        await youtube.Videos.Streams.DownloadAsync(audioStreamInfo, tempFilePath);

                        // 4. Mở file đó ra và Upload lên AWS S3
                        using (var fileStream = File.OpenRead(tempFilePath))
                        {
                            string fileName = $"auto_yt_{Guid.NewGuid()}.{ext}";
                            string s3Url = await _s3Service.UploadMp3Async(fileStream, fileName);

                            if (!string.IsNullOrEmpty(s3Url))
                            {
                                selectedSong.AudioUrl = s3Url;

                                // 👇 BỔ SUNG MA THUẬT: TẢI ẢNH TỪ ITUNES VÀ BẮN LÊN S3 👇
                                // Kiểm tra xem ảnh có phải là link web không, và tránh up lại nếu nó đã là link S3
                                if (!string.IsNullOrEmpty(selectedSong.CoverImage) && selectedSong.CoverImage.StartsWith("http") && !selectedSong.CoverImage.Contains("amazonaws.com"))
                                {
                                    try
                                    {
                                        // Tải ảnh từ Apple về dạng Luồng (Stream)
                                        using var imageStream = await _httpClient.GetStreamAsync(selectedSong.CoverImage);
                                        string imageExt = selectedSong.CoverImage.Contains(".png") ? "png" : "jpg";
                                        string imageFileName = $"cover_{Guid.NewGuid():N}.{imageExt}";

                                        // Up ngược luồng đó lên S3 (Sử dụng hàm UploadImageAsync ở S3Service)
                                        string s3ImageUrl = await _s3Service.UploadImageAsync(imageStream, imageFileName);

                                        if (!string.IsNullOrEmpty(s3ImageUrl))
                                        {
                                            // Xóa link Apple, thay bằng link S3 vĩnh cửu!
                                            selectedSong.CoverImage = s3ImageUrl;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"❌ Lỗi up ảnh nền lên S3: {ex.Message}");
                                        // Nếu mạng lỗi, vẫn giữ link cũ của Apple làm bảo hiểm
                                    }
                                }
                                // 👆 =================================================== 👆

                                // 👇 BẢO BỐI 4A: GỌI CỖ MÁY TÁCH VÀ NHẬN DIỆN CA SĨ Ở ĐÂY 👇
                                // LƯU Ý: Biến selectedSong.CoverImage lúc này ĐÃ LÀ LINK S3 XỊN!
                                List<string> generatedArtistIds = await _firestoreService.ProcessArtistsAsync(selectedSong.Artist, selectedSong.CoverImage);
                                selectedSong.ArtistIds = generatedArtistIds;
                                selectedSong.ArtistId = generatedArtistIds.FirstOrDefault();

                                // 👇 BẢO BỐI 4B: GỌI CỖ MÁY DỊCH THUẬT VÀ TẠO THỂ LOẠI 👇
                                string finalGenreId = await _firestoreService.CheckAndCreateGenreAsync(selectedSong.GenreId);
                                selectedSong.GenreId = finalGenreId;
                                // 👆 ================================================== 👆

                                bool isSaved = await _firestoreService.AddSongAsync(selectedSong);

                                // Trở lại luồng chính để báo thành công và chuyển trang
                                MainThread.BeginInvokeOnMainThread(async () =>
                                {
                                    if (isSaved)
                                    {
                                        // Đổi màu giao diện tức thì
                                        selectedSong.IsAdded = true;
                                        int idx = ApiSearchResults.IndexOf(selectedSong);
                                        if (idx >= 0) ApiSearchResults[idx] = selectedSong;

                                        await Shell.Current.DisplayAlert("Thành công! 🎉", "Bài hát đã tải xong, chuẩn bị phát nhạc...", "Tuyệt vời");

                                        // GỬI THÔNG BÁO TỚI CÁC TRANG KHÁC
                                        WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage()); // Kêu trang Library load lại
                                        WeakReferenceMessenger.Default.Send(new PlayRequestedMessage(selectedSong)); // Kêu Nhạc trưởng hát bài này đi!

                                        await Shell.Current.Navigation.PopAsync();
                                    }
                                    else
                                    {
                                        await Shell.Current.DisplayAlert("Lỗi", "Không thể lưu thông tin vào Firebase.", "OK");
                                    }
                                });
                            }
                            else
                            {
                                MainThread.BeginInvokeOnMainThread(async () =>
                                    await Shell.Current.DisplayAlert("Lỗi Upload", "Không thể đẩy file nhạc lên AWS S3.", "OK"));
                            }
                        }
                    }
                    finally
                    {
                        // 👇 BẢO BỐI 5: Dọn rác an toàn tuyệt đối
                        if (File.Exists(tempFilePath))
                        {
                            File.Delete(tempFilePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                        await Shell.Current.DisplayAlert("Lỗi Hệ Thống", "Lỗi: " + ex.Message, "OK"));
                }
                finally
                {
                    MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
                }
            });
        }

        [RelayCommand]
        public async Task GoBack() => await Shell.Current.Navigation.PopAsync();
    }
}