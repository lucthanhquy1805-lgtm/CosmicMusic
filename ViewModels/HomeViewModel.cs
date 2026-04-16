using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Storage; 
using Microsoft.Maui.ApplicationModel; 
using System;

namespace CosmicMusic.ViewModels
{
    
    public partial class HomeViewModel : ObservableObject, IRecipient<SongPlayedMessage>, IRecipient<RefreshLibraryMessage>, IRecipient<UserAvatarChangedMessage>
    {
        private readonly FirestoreService _firestoreService;
        private readonly AudioViewModel _audioViewModel;
        private bool _isNavigating = false;

        
        public ObservableCollection<Song> Playlist { get; set; } = new();
        public ObservableCollection<Album> FeaturedAlbums { get; set; } = new();
        public ObservableCollection<Artist> Artists { get; set; } = new();
        public ObservableCollection<Genre> Genres { get; set; } = new();

        
        public ObservableCollection<Album> TopArtists { get; set; } = new();

        
        public ObservableCollection<Song> RecentlyPlayed { get; set; } = new();
        public AudioViewModel AudioPlayer => _audioViewModel;

        
        [ObservableProperty] private bool _isUserMenuVisible = false;
        [ObservableProperty] private string _userAvatarText;
        [ObservableProperty] private string _userName;
        [ObservableProperty] private bool _isPremiumUser;
        [ObservableProperty] private string _avatarBorderColor = "#6C63FF";
        [ObservableProperty] private ObservableCollection<Song> _recommendedSongs = new();
        [ObservableProperty] private string _recommendationTitle;
        [ObservableProperty] private bool _hasRecommendations = false;
        [ObservableProperty]
        private string _headerPhotoUrl;
        [ObservableProperty]
        private bool _isAdmin;

        [ObservableProperty] private bool _hasRecentlyPlayed;

       
        public HomeViewModel(FirestoreService firestoreService, AudioViewModel audioViewModel)
        {
            _firestoreService = firestoreService;
            _audioViewModel = audioViewModel;

          
            WeakReferenceMessenger.Default.Register<SongPlayedMessage>(this);

          
            WeakReferenceMessenger.Default.Register<RefreshLibraryMessage>(this);

           
            WeakReferenceMessenger.Default.Register<UserAvatarChangedMessage>(this);

            LoadUserAvatar();
            LoadDataFromFirebase();

            
            HeaderPhotoUrl = Preferences.Get("UserPhotoUrl", "");
            WeakReferenceMessenger.Default.Register<RefreshRecentlyPlayedMessage>(this, async (r, m) =>
            {

                await LoadRecentlyPlayedAsync();
            });
        }
        public async Task InitializeAsync()
        {
            LoadDataFromFirebase();
            await Task.CompletedTask;
        }

        public void Receive(SongPlayedMessage message)
        {
            var song = message.PlayedSong;
            if (song == null) return;

            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                
                var existingSong = RecentlyPlayed.FirstOrDefault(s => s.Id == song.Id);

            
                if (existingSong != null)
                {
                    RecentlyPlayed.Remove(existingSong);
                }

               
                RecentlyPlayed.Insert(0, song);

               
                if (RecentlyPlayed.Count > 10)
                {
                    RecentlyPlayed.RemoveAt(RecentlyPlayed.Count - 1);
                }

                HasRecentlyPlayed = RecentlyPlayed.Count > 0;
            });
        }

        
        public void Receive(RefreshLibraryMessage message)
        {
           
            LoadDataFromFirebase();
        }

      
        public void Receive(UserAvatarChangedMessage message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                HeaderPhotoUrl = message.NewAvatarUrl;
            });
        }

      
        private async void LoadDataFromFirebase()
        {
            try
            {
                var taskSongs = _firestoreService.GetAllSongsAsync();
                var taskAlbums = _firestoreService.GetAllAlbumsAsync();
                var taskArtists = _firestoreService.GetAllArtistsAsync();
                var taskGenres = _firestoreService.GetAllGenresAsync();

                

                await LoadRecentlyPlayedAsync();

                
                await Task.WhenAll(taskSongs, taskAlbums, taskArtists, taskGenres);

                
                string uid = Preferences.Get("UserId", "");

            
                (List<Song> Songs, string Title) suggestionResult = (new List<Song>(), "");

                if (!string.IsNullOrEmpty(uid))
                {
                    
                    suggestionResult = await _firestoreService.GetRecommendationsAsync(uid);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Playlist.Clear();
                    foreach (var song in taskSongs.Result) Playlist.Add(song);
                    for (int i = RecentlyPlayed.Count - 1; i >= 0; i--)
                    {
                        var freshSong = Playlist.FirstOrDefault(s => s.Id == RecentlyPlayed[i].Id);
                        if (freshSong != null)
                        {
                            RecentlyPlayed[i] = freshSong; 
                        }
                        else
                        {
                            RecentlyPlayed.RemoveAt(i); 
                        }
                    }
                    HasRecentlyPlayed = RecentlyPlayed.Count > 0;

                    FeaturedAlbums.Clear();
                    foreach (var album in taskAlbums.Result) FeaturedAlbums.Add(album);

                    Artists.Clear();
                    TopArtists.Clear();
                    foreach (var artist in taskArtists.Result)
                    {
                        
                        bool hasSongs = Playlist.Any(s => s.ArtistId == artist.Id || s.Artist == artist.Name);

                        if (hasSongs)
                        {
                            Artists.Add(artist);

                            TopArtists.Add(new Album
                            {
                                Id = artist.Id,
                                Title = artist.Name,
                                Artist = "Nghệ sĩ",
                                CoverImage = artist.Avatar,
                                Description = "Artist"
                            });
                        }
                    }

                    

                    Genres.Clear();
                    foreach (var genre in taskGenres.Result) Genres.Add(genre);

                   
                    RecommendedSongs.Clear();

                    
                    if (suggestionResult.Songs != null && suggestionResult.Songs.Count > 0)
                    {
                        RecommendationTitle = suggestionResult.Title;
                        foreach (var song in suggestionResult.Songs)
                        {
                            RecommendedSongs.Add(song);
                        }
                        HasRecommendations = true;
                    }
                    else
                    {
                        HasRecommendations = false; 
                    }

                   
                   
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi HomeViewModel (LoadData): {ex.Message}");
            }
        }


        private async Task LoadRecentlyPlayedAsync()
        {
            string uid = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(uid)) return;

            
            var recentSongs = await _firestoreService.GetRecentlyPlayedAsync(uid);

           
            var allValidSongs = await _firestoreService.GetAllSongsAsync();

            
            var cleanRecentSongs = new List<Song>();

            foreach (var recent in recentSongs)
            {
              
                var validSong = allValidSongs.FirstOrDefault(v => v.Id == recent.Id);

              
                if (validSong != null)
                {
                   
                    recent.Title = validSong.Title;
                    recent.Artist = validSong.Artist;
                    recent.CoverImage = validSong.CoverImage;

                    cleanRecentSongs.Add(recent);
                }
            }

           
            var finalList = cleanRecentSongs.Take(10).ToList();

           
            MainThread.BeginInvokeOnMainThread(() =>
            {
                RecentlyPlayed.Clear();
                foreach (var song in finalList)
                {
                    RecentlyPlayed.Add(song);
                }
                HasRecentlyPlayed = RecentlyPlayed.Count > 0;
            });

         
            Task.Run(() =>
            {
                var deadSongs = recentSongs.Where(recent => !allValidSongs.Any(valid => valid.Id == recent.Id)).ToList();
               
            });
        }

        public void LoadUserAvatar()
        {
            string email = Preferences.Get("UserEmail", "");
            UserAvatarText = !string.IsNullOrEmpty(email) ? email.Substring(0, 1).ToUpper() : "?";

            string savedFullName = Preferences.Get("UserName", "");
            if (!string.IsNullOrEmpty(savedFullName)) UserName = savedFullName;
            else if (!string.IsNullOrEmpty(email)) UserName = email;
            else UserName = "Khách";
            IsAdmin = Preferences.Get("IsAdmin", false);
            CheckPremiumStatus();
        }

        private void CheckPremiumStatus()
        {
            bool isSessionVip = Preferences.Get("IsPremium", false);
            string email = Preferences.Get("UserEmail", "");
            bool isHistoryVip = Preferences.Get($"VIP_{email}", false);
            IsPremiumUser = isSessionVip || isHistoryVip;

            if (IsPremiumUser)
            {
                AvatarBorderColor = "#FFD700";
                if (!isSessionVip) Preferences.Set("IsPremium", true);
            }
            else
            {
                AvatarBorderColor = "#6C63FF";
            }
        }

        // ==========================================================
        // 6. CÁC LỆNH ĐIỀU HƯỚNG
        // ==========================================================
        [RelayCommand]
        public async Task OpenAlbum(Album albumItem)
        {
            if (albumItem == null || _isNavigating) return;
            try
            {
                _isNavigating = true;
                var param = new Dictionary<string, object> { { "AlbumData", albumItem } };
                await Shell.Current.GoToAsync(nameof(AlbumDetailPage), param);
            }
            finally { await Task.Delay(500); _isNavigating = false; }
        }


        [RelayCommand]
        public async Task SelectSong(Song song)
        {
            // Chặn ngay lập tức nếu dữ liệu rỗng hoặc App đang trong quá trình chuyển trang
            if (song == null || _isNavigating) return;

            try
            {
                _isNavigating = true; // 🔒 Bấm chốt khóa cửa lại

                bool isCurrentVip = Preferences.Get("IsPremium", false);
                if (song.IsPremium == true && isCurrentVip == false)
                {
                    bool answer = await Shell.Current.DisplayAlert("Premium Content 👑", "Bài này dành cho VIP. Nâng cấp nhé?", "Xem gói VIP", "Để sau");
                    if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                    return;
                }

                // Phát nhạc
                var contextList = RecentlyPlayed.Contains(song) ? RecentlyPlayed : Playlist;
                _audioViewModel.PlaySong(song, contextList);

                // Mở trang Player
                await Shell.Current.GoToAsync(nameof(PlayerPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Lỗi chuyển trang Player: {ex.Message}");
            }
            finally
            {
                // 🔓 Đợi 0.5 giây rồi mới mở khóa, đảm bảo miễn nhiễm 100% với trò Spam Click
                await Task.Delay(500);
                _isNavigating = false;
            }
        }

      
        //  HÀM LOGOUT 
        [RelayCommand]
        public async Task PerformLogout()
        {
            IsUserMenuVisible = false; 

            bool answer = await Shell.Current.DisplayAlert("Đăng xuất", "Bạn có chắc chắn muốn đăng xuất khỏi Cosmic Music?", "Có", "Không");

            if (answer)
            {
                try
                {
                    _isNavigating = true;

                    if (_audioViewModel != null)
                    {
                        _audioViewModel.Cleanup();
                    }

                    
                    Preferences.Remove("AuthToken");
                    Preferences.Remove("UserEmail");
                    Preferences.Remove("UserName");
                    Preferences.Remove("UserId");
                    Preferences.Remove("IsPremium");           
                    Preferences.Remove("UserPhotoUrl");
                    IsPremiumUser = false;
                    AvatarBorderColor = "#6C63FF";
                    UserAvatarText = "?";
                    UserName = "Khách";
                    HeaderPhotoUrl = "";
                    RecentlyPlayed?.Clear();
                    HasRecentlyPlayed = false;
                    await Shell.Current.GoToAsync($"///{nameof(LoginPage)}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Lỗi Đăng xuất: {ex.Message}");
                }
                finally
                {
                   
                    await Task.Delay(500);
                    _isNavigating = false;
                }
            }
        }



        [RelayCommand]
        public async Task NavigateToPlayer()
        {
            if (_audioViewModel.CurrentSong == null || _isNavigating) return;
            try { _isNavigating = true; await Shell.Current.GoToAsync(nameof(PlayerPage)); }
            finally { await Task.Delay(500); _isNavigating = false; }
        }

        [RelayCommand]
        public async Task NavigateToSearch()
        {
            if (_isNavigating) return;
            try { _isNavigating = true; await Shell.Current.GoToAsync("//SearchTab"); }
            finally { await Task.Delay(500); _isNavigating = false; }
        }

        [RelayCommand]
        public async Task OpenProfile()
        {
            if (_isNavigating) return;
            IsUserMenuVisible = false;
            try { _isNavigating = true; await Shell.Current.GoToAsync(nameof(ProfilePage)); }
            finally { await Task.Delay(500); _isNavigating = false; }
        }

        [RelayCommand]
        public async Task OpenSettings()
        {
            if (_isNavigating) return;
            IsUserMenuVisible = false;
            try { _isNavigating = true; await Shell.Current.GoToAsync(nameof(SettingsPage)); }
            finally { await Task.Delay(500); _isNavigating = false; }
        }
        [RelayCommand]
        public async Task GoToAdminPage()
        {
            CloseUserMenu(); 

            
            await Shell.Current.GoToAsync(nameof(AdminDashboardPage));
        }


        [RelayCommand] public void TapUserAvatar() { IsUserMenuVisible = !IsUserMenuVisible; }
        [RelayCommand] public void CloseUserMenu() { IsUserMenuVisible = false; }
        [RelayCommand] public async Task AddAccount() { await Shell.Current.DisplayAlert("Thông báo", "Tính năng đang phát triển", "OK"); }
        [RelayCommand] public async Task OpenWhatsNew() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Mới", "Update tính năng Group Album!", "OK"); }
        [RelayCommand] public async Task OpenStats() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Thống kê", "Bạn đã nghe nhạc rất nhiều!", "OK"); }
        [RelayCommand] public async Task OpenHistory() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Gần đây", "Tính năng này hiện được hiển thị ở màn hình chính.", "OK"); }

    }
}