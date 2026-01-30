using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace CosmicMusic.ViewModels
{
    public partial class HomeViewModel : ObservableObject, IRecipient<SongPlayedMessage>
    {
        private readonly MusicApiService _musicService;
        private readonly AudioViewModel _audioViewModel;

        // ==========================================================
        // 1. CÁC DANH SÁCH DỮ LIỆU HIỂN THỊ
        // ==========================================================
        public ObservableCollection<Song> Playlist { get; set; } = new();
        public ObservableCollection<Album> FeaturedAlbums { get; set; } = new();
        public ObservableCollection<Album> TopArtists { get; set; } = new();

        // Danh sách bài hát vừa nghe
        public ObservableCollection<Song> RecentlyPlayedList { get; set; } = new();

        public AudioViewModel AudioPlayer => _audioViewModel;

        // ==========================================================
        // 2. CÁC BIẾN GIAO DIỆN
        // ==========================================================
        [ObservableProperty] private bool _isUserMenuVisible = false;
        [ObservableProperty] private string _userAvatarText;
        [ObservableProperty] private string _userName;
        [ObservableProperty] private bool _isPremiumUser;
        [ObservableProperty] private string _avatarBorderColor = "#6C63FF";

        [ObservableProperty] private bool _hasHistory;

        // 3. Khởi tạo
        public HomeViewModel(MusicApiService musicService, AudioViewModel audioViewModel)
        {
            _musicService = musicService;
            _audioViewModel = audioViewModel;

            // Đăng ký nhận tin nhắn
            WeakReferenceMessenger.Default.Register<SongPlayedMessage>(this);

            LoadUserAvatar();
            LoadHistory();
            LoadSongs();
        }

        // ==========================================================
        // XỬ LÝ LỊCH SỬ NGHE NHẠC (RECENTLY PLAYED)
        // ==========================================================
        public void Receive(SongPlayedMessage message)
        {
            var song = message.PlayedSong;
            if (song == null) return;

            // Đảm bảo cập nhật UI trên Main Thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var existing = RecentlyPlayedList.FirstOrDefault(s => s.Title == song.Title);
                if (existing != null) RecentlyPlayedList.Remove(existing);

                RecentlyPlayedList.Insert(0, song);

                if (RecentlyPlayedList.Count > 10)
                {
                    RecentlyPlayedList.RemoveAt(RecentlyPlayedList.Count - 1);
                }

                HasHistory = RecentlyPlayedList.Count > 0;
                SaveHistory();
            });
        }

        private void SaveHistory()
        {
            try
            {
                string userId = Preferences.Get("UserId", "Guest");
                string key = $"History_{userId}";
                var json = JsonSerializer.Serialize(RecentlyPlayedList);
                Preferences.Set(key, json);
            }
            catch { }
        }

        private void LoadHistory()
        {
            try
            {
                string userId = Preferences.Get("UserId", "Guest");
                string key = $"History_{userId}";
                string json = Preferences.Get(key, "");

                RecentlyPlayedList.Clear();

                if (!string.IsNullOrEmpty(json))
                {
                    var list = JsonSerializer.Deserialize<List<Song>>(json);
                    if (list != null && list.Count > 0)
                    {
                        foreach (var item in list) RecentlyPlayedList.Add(item);
                    }
                }
                HasHistory = RecentlyPlayedList.Count > 0;
            }
            catch
            {
                HasHistory = false;
            }
        }

        // ==========================================================
        // 4. HÀM TẢI VÀ GOM NHÓM DỮ LIỆU
        // ==========================================================
        private async void LoadSongs()
        {
            try
            {
                var allSongs = await _musicService.GetSongsAsync();
                if (allSongs == null || allSongs.Count == 0) return;

                // Cập nhật UI trên Main Thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Playlist.Clear();
                    foreach (var song in allSongs) Playlist.Add(song);

                    FeaturedAlbums.Clear();
                    var uniqueAlbums = allSongs
                        .Where(s => !string.IsNullOrEmpty(s.Album))
                        .GroupBy(s => s.Album.Trim())
                        .Select(g => g.First())
                        .ToList();

                    foreach (var s in uniqueAlbums)
                    {
                        FeaturedAlbums.Add(new Album
                        {
                            Title = s.Album.Trim(),
                            Artist = s.Artist ?? "Unknown",
                            CoverImage = s.CoverImage,
                            Description = "Album"
                        });
                    }

                    TopArtists.Clear();
                    var uniqueArtists = allSongs
                        .Where(s => !string.IsNullOrEmpty(s.Artist))
                        .GroupBy(s => s.Artist.Trim())
                        .Select(g => g.First())
                        .ToList();

                    foreach (var s in uniqueArtists)
                    {
                        TopArtists.Add(new Album
                        {
                            Title = s.Artist.Trim(),
                            Artist = "Nghệ sĩ",
                            CoverImage = s.CoverImage,
                            Description = "Artist"
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi HomeViewModel: {ex.Message}");
            }
        }

        // ==========================================================
        // 5. CÁC HÀM CẬP NHẬT USER
        // ==========================================================
        public void LoadUserAvatar()
        {
            string email = Preferences.Get("UserEmail", "");
            UserAvatarText = !string.IsNullOrEmpty(email) ? email.Substring(0, 1).ToUpper() : "?";

            string savedFullName = Preferences.Get("UserName", "");
            if (!string.IsNullOrEmpty(savedFullName)) UserName = savedFullName;
            else if (!string.IsNullOrEmpty(email)) UserName = email;
            else UserName = "Khách";

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
            if (albumItem == null) return;
            var param = new Dictionary<string, object> { { "AlbumData", albumItem } };
            await Shell.Current.GoToAsync(nameof(AlbumDetailPage), param);
        }

        [RelayCommand]
        public async Task SelectSong(Song song)
        {
            if (song == null) return;
            bool isCurrentVip = Preferences.Get("IsPremium", false);
            if (song.IsPremium == true && isCurrentVip == false)
            {
                bool answer = await Shell.Current.DisplayAlert("Premium Content 👑", "Bài này dành cho VIP. Nâng cấp nhé?", "Xem gói VIP", "Để sau");
                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                return;
            }

            _audioViewModel.PlaySong(song, Playlist);
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }

        [RelayCommand]
        public async Task PerformLogout()
        {
            IsUserMenuVisible = false;
            bool answer = await Shell.Current.DisplayAlert("Đăng xuất", "Bạn muốn thoát?", "Có", "Không");
            if (answer)
            {
                _audioViewModel.Cleanup();

                Preferences.Remove("AuthToken");
                Preferences.Remove("UserEmail");
                Preferences.Remove("UserName");
                Preferences.Remove("UserId");
                Preferences.Remove("IsPremium");

                IsPremiumUser = false;
                AvatarBorderColor = "#6C63FF";
                UserAvatarText = "?";
                UserName = "Khách";

                RecentlyPlayedList.Clear();
                HasHistory = false;

                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
            }
        }

        [RelayCommand] public async Task NavigateToPlayer() { if (_audioViewModel.CurrentSong != null) await Shell.Current.GoToAsync(nameof(PlayerPage)); }
        // Lưu ý: Đã sửa lại đường dẫn điều hướng SearchPage thành Route tuyệt đối để tránh chồng chéo trang
        [RelayCommand] public async Task NavigateToSearch() { await Shell.Current.GoToAsync("//SearchTab/SearchPage"); }
        [RelayCommand] public void TapUserAvatar() { IsUserMenuVisible = !IsUserMenuVisible; }
        [RelayCommand] public void CloseUserMenu() { IsUserMenuVisible = false; }
        [RelayCommand] public async Task OpenProfile() { IsUserMenuVisible = false; await Shell.Current.GoToAsync(nameof(ProfilePage)); }
        [RelayCommand] public async Task OpenSettings() { IsUserMenuVisible = false; await Shell.Current.GoToAsync(nameof(SettingsPage)); }
        [RelayCommand] public async Task AddAccount() { await Shell.Current.DisplayAlert("Thông báo", "Tính năng đang phát triển", "OK"); }
        [RelayCommand] public async Task OpenWhatsNew() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Mới", "Update tính năng Group Album!", "OK"); }
        [RelayCommand] public async Task OpenStats() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Thống kê", "Bạn đã nghe nhạc rất nhiều!", "OK"); }
        [RelayCommand] public async Task OpenHistory() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Gần đây", "Danh sách đã xem...", "OK"); }
    }
}