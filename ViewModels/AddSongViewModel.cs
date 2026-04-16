using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; 
using CosmicMusic.Models;
using CosmicMusic.Services;
using System.Collections.ObjectModel;
using System.Text.Json;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using System.Globalization;

namespace CosmicMusic.ViewModels
{
    
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

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CosmicMusicApp/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        
      
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
                // ==========================================
                // BẪY SỐ 1: KIỂM TRA MÁY CHỦ FIREBASE
                // ==========================================
                List<Song> existingSongs = new();
                try
                {
                    existingSongs = await _firestoreService.GetAllSongsAsync();
                }
                catch (Exception fbEx)
                {
                    // Nếu lỗi ở đây, khả năng cao Project ID Firestore bị sai hoặc collection bị xóa
                    throw new Exception($"[LỖI FIREBASE] Kho nhạc gốc có vấn đề. Chi tiết: {fbEx.Message}");
                }

                // ==========================================
                // BẪY SỐ 2: KIỂM TRA MÁY CHỦ APPLE ITUNES
                // ==========================================
                string response = "";
                try
                {
                    // 👇 SỬA LẠI ĐÚNG 2 DÒNG NÀY 👇

                    // 1. Xóa khoảng trắng thừa 2 đầu và thay thế toàn bộ dấu cách ở giữa thành dấu "+"
                    string safeSearchTerm = SearchText.Trim().Replace(" ", "+");

                    // 2. Đưa safeSearchTerm vào URL (KHÔNG cần dùng Uri.EscapeDataString nữa vì Apple chỉ nhận dấu +)
                    string url = $"https://itunes.apple.com/search?term={safeSearchTerm}&entity=song&limit=15&country=VN";

                    response = await _httpClient.GetStringAsync(url);
                }
                catch (Exception appleEx)
                {
                    throw new Exception($"[LỖI APPLE API] Không kết nối được iTunes. Chi tiết: {appleEx.Message}");
                }
                // ==========================================
                // XỬ LÝ DỮ LIỆU BÌNH THƯỜNG
                // ==========================================
                using var doc = JsonDocument.Parse(response);
                var results = doc.RootElement.GetProperty("results").EnumerateArray();
                var tempSongs = new List<Song>();

                foreach (var item in results)
                {
                    string title = item.TryGetProperty("trackName", out var t) ? t.GetString() : "Unknown";
                    string artist = item.TryGetProperty("artistName", out var a) ? a.GetString() : "Unknown";
                    string rawGenre = item.TryGetProperty("primaryGenreName", out var g) ? g.GetString() : "Pop";
                    string formattedGenreId = "genre_" + rawGenre.ToLower().Replace(" ", "").Replace("-", "").Replace("/", "");

                    // Radar quét bài hát cũ
                    bool isAlreadyAdded = existingSongs.Any(x =>
                    {
                        if (string.IsNullOrWhiteSpace(x.Title) || string.IsNullOrWhiteSpace(x.Artist)) return false;
                        bool titleMatch = string.Compare(x.Title.Trim(), title.Trim(), CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0
                                          || title.ToLower().Contains(x.Title.ToLower());
                        bool artistMatch = string.Compare(x.Artist.Trim(), artist.Trim(), CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0
                                           || artist.ToLower().Contains(x.Artist.ToLower());
                        return titleMatch && artistMatch;
                    });

                    tempSongs.Add(new Song
                    {
                        Title = title,
                        Artist = artist,
                        GenreId = rawGenre,
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
                // Bảng báo lỗi bây giờ sẽ chỉ đích danh thủ phạm
                MainThread.BeginInvokeOnMainThread(async () => {
                    IsLoading = false;
                    await Shell.Current.DisplayAlert("Phát hiện lỗi", ex.Message, "OK");
                });
            }
        }

        [RelayCommand]
        public async Task SelectAndUploadSong(Song selectedSong)
        {
            if (selectedSong == null || selectedSong.IsAdded) return; 

            bool confirm = await Shell.Current.DisplayAlert(
                "Tự động tải nhạc",
                $"Hệ thống sẽ tải bản Audio chuẩn nhất của '{selectedSong.Title}' lên kho nhạc. Bắt đầu ngay?",
                "Tải Tự Động", "Hủy");

            if (!confirm) return;

            IsLoading = true;

            
            await Task.Run(async () =>
            {
                try
                {
                    var youtube = new YoutubeClient();
                    string searchQuery = $"{selectedSong.Title} {selectedSong.Artist} official audio";

                  
                    var searchResults = await youtube.Search.GetVideosAsync(searchQuery).CollectAsync(1);
                    var video = searchResults.FirstOrDefault();

                    if (video == null)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                            await Shell.Current.DisplayAlert("Thất bại", "Không tìm thấy luồng âm thanh phù hợp.", "OK"));
                        return;
                    }

                 
                    if (selectedSong.Duration <= 0 && video.Duration.HasValue)
                    {
                        selectedSong.Duration = video.Duration.Value.TotalSeconds;
                    }

                  
                    var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
                    var audioStreams = streamManifest.GetAudioOnlyStreams();

                   
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

                   
                    string ext = "m4a";
                    string tempFilePath = Path.Combine(FileSystem.CacheDirectory, $"temp_{Guid.NewGuid()}.{ext}");

                    try
                    {
                      
                        await youtube.Videos.Streams.DownloadAsync(audioStreamInfo, tempFilePath);

                       
                        using (var fileStream = File.OpenRead(tempFilePath))
                        {
                            string fileName = $"auto_yt_{Guid.NewGuid()}.{ext}";
                            string s3Url = await _s3Service.UploadMp3Async(fileStream, fileName);

                            if (!string.IsNullOrEmpty(s3Url))
                            {
                                selectedSong.AudioUrl = s3Url;

                               
                                if (!string.IsNullOrEmpty(selectedSong.CoverImage) && selectedSong.CoverImage.StartsWith("http") && !selectedSong.CoverImage.Contains("amazonaws.com"))
                                {
                                    try
                                    {

                                        using var imageStream = await _httpClient.GetStreamAsync(selectedSong.CoverImage);
                                        string imageExt = selectedSong.CoverImage.Contains(".png") ? "png" : "jpg";
                                        string imageFileName = $"cover_{Guid.NewGuid():N}.{imageExt}";

                                       
                                        string s3ImageUrl = await _s3Service.UploadImageAsync(imageStream, imageFileName);

                                        if (!string.IsNullOrEmpty(s3ImageUrl))
                                        {
                                           
                                            selectedSong.CoverImage = s3ImageUrl;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"❌ Lỗi up ảnh nền lên S3: {ex.Message}");
                                      
                                    }
                                }
                               

                                
                                List<string> generatedArtistIds = await _firestoreService.ProcessArtistsAsync(selectedSong.Artist, selectedSong.CoverImage);
                                selectedSong.ArtistIds = generatedArtistIds;
                                selectedSong.ArtistId = generatedArtistIds.FirstOrDefault();

                                string finalGenreId = await _firestoreService.CheckAndCreateGenreAsync(selectedSong.GenreId);
                                selectedSong.GenreId = finalGenreId;
                               

                                bool isSaved = await _firestoreService.AddSongAsync(selectedSong);

                                
                                MainThread.BeginInvokeOnMainThread(async () =>
                                {
                                    if (isSaved)
                                    {
                                       
                                        selectedSong.IsAdded = true;
                                        int idx = ApiSearchResults.IndexOf(selectedSong);
                                        if (idx >= 0) ApiSearchResults[idx] = selectedSong;

                                        await Shell.Current.DisplayAlert("Thành công! 🎉", "Bài hát đã tải xong, chuẩn bị phát nhạc...", "Tuyệt vời");

                                       
                                        WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage()); 
                                        WeakReferenceMessenger.Default.Send(new PlayRequestedMessage(selectedSong)); 

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