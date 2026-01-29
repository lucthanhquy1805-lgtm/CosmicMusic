using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;
using System.Linq;

namespace CosmicMusic.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private readonly MusicApiService _musicService;
        private readonly AudioViewModel _audioViewModel;

        // ==========================================================
        // 1. CÁC DANH SÁCH DỮ LIỆU HIỂN THỊ
        // ==========================================================
        public ObservableCollection<Song> Playlist { get; set; } = new();
        public ObservableCollection<Album> FeaturedAlbums { get; set; } = new();
        public ObservableCollection<Album> TopArtists { get; set; } = new();

        public AudioViewModel AudioPlayer => _audioViewModel;

        // ==========================================================
        // 2. CÁC BIẾN GIAO DIỆN
        // ==========================================================
        [ObservableProperty] private bool _isUserMenuVisible = false;
        [ObservableProperty] private string _userAvatarText;
        [ObservableProperty] private string _userName;
        [ObservableProperty] private bool _isPremiumUser;
        [ObservableProperty] private string _avatarBorderColor = "#6C63FF";

        // 3. Khởi tạo
        public HomeViewModel(MusicApiService musicService, AudioViewModel audioViewModel)
        {
            _musicService = musicService;
            _audioViewModel = audioViewModel;

            LoadUserAvatar();
            LoadSongs();
        }

        // ==========================================================
        // 4. HÀM TẢI VÀ GOM NHÓM DỮ LIỆU (GIỮ NGUYÊN CỦA BẠN - ĐÃ TỐT)
        // ==========================================================
        private async void LoadSongs()
        {
            try
            {
                var allSongs = await _musicService.GetSongsAsync();
                if (allSongs == null || allSongs.Count == 0) return;

                // 1. NẠP LIST BÀI HÁT
                Playlist.Clear();
                foreach (var song in allSongs) Playlist.Add(song);

                // 2. XỬ LÝ ALBUM
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

                // 3. XỬ LÝ CA SĨ
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi HomeViewModel: {ex.Message}");
            }
        }

        // ==========================================================
        // 5. CÁC HÀM CẬP NHẬT USER (GIỮ NGUYÊN)
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

            if (IsPremiumUser) { AvatarBorderColor = "#FFD700"; if (!isSessionVip) Preferences.Set("IsPremium", true); }
            else { AvatarBorderColor = "#6C63FF"; }
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

            // Check VIP (Logic này của bạn đã đúng, giữ nguyên)
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

        // 👇 ĐÂY LÀ PHẦN TÔI ĐÃ SỬA LẠI CHO BẠN (QUAN TRỌNG)
        [RelayCommand]
        public async Task PerformLogout()
        {
            IsUserMenuVisible = false;
            bool answer = await Shell.Current.DisplayAlert("Đăng xuất", "Bạn muốn thoát?", "Có", "Không");
            if (answer)
            {
                // 1. 🛑 DỪNG NHẠC NGAY (Code cũ thiếu dòng này)
                // (Đảm bảo bạn đã thêm hàm Cleanup() vào AudioViewModel như hướng dẫn trước)
                _audioViewModel.Cleanup();

                // 2. Xóa dữ liệu Preferences
                Preferences.Remove("AuthToken");
                Preferences.Remove("UserEmail");
                Preferences.Remove("UserName");
                Preferences.Remove("UserId");
                Preferences.Remove("IsPremium");

                // 3. 🧹 RESET GIAO DIỆN VỀ MẶC ĐỊNH (Code cũ thiếu phần này)
                // Để tránh việc đăng nhập lại nick thường mà vẫn hiện màu vàng VIP cũ
                IsPremiumUser = false;
                AvatarBorderColor = "#6C63FF";
                UserAvatarText = "?";
                UserName = "Khách";

                // 4. Chuyển trang
                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
            }
        }

        // Các lệnh placeholder (Giữ nguyên)
        [RelayCommand] public async Task NavigateToPlayer() { if (_audioViewModel.CurrentSong != null) await Shell.Current.GoToAsync(nameof(PlayerPage)); }
        [RelayCommand] public async Task NavigateToSearch() { await Shell.Current.GoToAsync(nameof(SearchPage)); }
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