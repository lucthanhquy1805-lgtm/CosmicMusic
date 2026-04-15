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
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
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
                string url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(SearchText)}&entity=song&limit=15";
                var response = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var results = doc.RootElement.GetProperty("results").EnumerateArray();

            
                var existingSongs = await _firestoreService.GetAllSongsAsync();

                var tempSongs = new List<Song>();
                foreach (var item in results)
                {
                    string title = item.TryGetProperty("trackName", out var t) ? t.GetString() : "Unknown";
                    string artist = item.TryGetProperty("artistName", out var a) ? a.GetString() : "Unknown";

                   
                    string rawGenre = item.TryGetProperty("primaryGenreName", out var g) ? g.GetString() : "Pop";

                   
                    string formattedGenreId = "genre_" + rawGenre.ToLower().Replace(" ", "").Replace("-", "").Replace("/", "");

                    bool isAlreadyAdded = existingSongs.Any(x =>
                    {
                        if (string.IsNullOrWhiteSpace(x.Title) || string.IsNullOrWhiteSpace(x.Artist)) return false;

                        // 1. Quét Tên bài: IgnoreNonSpace giúp ép "Sơn Tùng" thành "Son Tung" để so sánh
                        bool titleMatch = string.Compare(x.Title.Trim(), title.Trim(), CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0
                                          || title.ToLower().Contains(x.Title.ToLower()); // Hoặc tên này bao bọc tên kia (VD: Mashup)

                        // 2. Quét Ca sĩ tương tự
                        bool artistMatch = string.Compare(x.Artist.Trim(), artist.Trim(), CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0
                                           || artist.ToLower().Contains(x.Artist.ToLower());

                        // Phải khớp cả Tên bài lẫn Ca sĩ (Tránh việc Adele hát bài Hello lại bị trùng với Lionel Richie)
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
                MainThread.BeginInvokeOnMainThread(async () => {
                    IsLoading = false;
                    await Shell.Current.DisplayAlert("Lỗi", "Lỗi mạng: " + ex.Message, "OK");
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