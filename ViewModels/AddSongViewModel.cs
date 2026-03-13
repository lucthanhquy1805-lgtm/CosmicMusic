using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;
using CosmicMusic.Services;
using System.Collections.ObjectModel;
using System.Text.Json;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace CosmicMusic.ViewModels
{
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

                var tempSongs = new List<Song>();
                foreach (var item in results)
                {
                    tempSongs.Add(new Song
                    {
                        Title = item.TryGetProperty("trackName", out var t) ? t.GetString() : "Unknown",
                        Artist = item.TryGetProperty("artistName", out var a) ? a.GetString() : "Unknown",
                        CoverImage = item.TryGetProperty("artworkUrl100", out var c) ? c.GetString()?.Replace("100x100bb", "600x600bb") : "https://via.placeholder.com/600",
                        IsPremium = false
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
            if (selectedSong == null) return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Tự động tải nhạc",
                $"Hệ thống sẽ tải bản Audio chuẩn nhất của '{selectedSong.Title}' lên kho nhạc. Bắt đầu ngay?",
                "Tải Tự Động", "Hủy");

            if (!confirm) return;

            IsLoading = true;

            // 👇 QUAN TRỌNG: Ném toàn bộ tác vụ mạng sang Luồng Ngầm (Background Thread) để Android không báo lỗi
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

                    // 2. Lấy luồng âm thanh tốt nhất
                    var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
                    var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                    if (audioStreamInfo != null)
                    {
                        // 3. Tải file từ YouTube xuống ổ cứng máy ảo
                        string ext = audioStreamInfo.Container.Name;
                        string tempFilePath = Path.Combine(FileSystem.CacheDirectory, $"temp_{Guid.NewGuid()}.{ext}");

                        await youtube.Videos.Streams.DownloadAsync(audioStreamInfo, tempFilePath);

                        // 4. Mở file đó ra và Upload lên S3
                        using (var fileStream = File.OpenRead(tempFilePath))
                        {
                            string fileName = $"auto_yt_{Guid.NewGuid()}.{ext}";
                            string s3Url = await _s3Service.UploadMp3Async(fileStream, fileName);

                            if (!string.IsNullOrEmpty(s3Url))
                            {
                                selectedSong.AudioUrl = s3Url;
                                bool isSaved = await _firestoreService.AddSongAsync(selectedSong);

                                // Trở lại luồng chính để báo thành công và chuyển trang
                                MainThread.BeginInvokeOnMainThread(async () =>
                                {
                                    if (isSaved)
                                    {
                                        await Shell.Current.DisplayAlert("Thành công! 🎉", "Bài hát đã được thêm vào hệ thống.", "Tuyệt");
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
                                    await Shell.Current.DisplayAlert("Lỗi Upload", "Không thể đẩy file lên AWS S3.", "OK"));
                            }
                        }

                        // 5. Xóa file rác sau khi làm xong để nhẹ máy
                        if (File.Exists(tempFilePath))
                        {
                            File.Delete(tempFilePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Lỗi mạng ném ra ngoài luồng chính
                    MainThread.BeginInvokeOnMainThread(async () =>
                        await Shell.Current.DisplayAlert("Lỗi Hệ Thống", "Lỗi: " + ex.Message, "OK"));
                }
                finally
                {
                    // Tắt loading phải thực hiện trên luồng chính
                    MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
                }
            });
        }

        [RelayCommand]
        public async Task GoBack() => await Shell.Current.Navigation.PopAsync();
    }
}