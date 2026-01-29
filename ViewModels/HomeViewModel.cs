using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;
using System.Linq; // 👈 QUAN TRỌNG: Cần dòng này để dùng hàm GroupBy

namespace CosmicMusic.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private readonly MusicApiService _musicService;
        private readonly AudioViewModel _audioViewModel;

        // ==========================================================
        // 1. CÁC DANH SÁCH DỮ LIỆU HIỂN THỊ
        // ==========================================================

        // Danh sách bài hát lẻ (Dùng cho Recently Played - Binding vào Playlist)
        public ObservableCollection<Song> Playlist { get; set; } = new();

        // 👇 MỚI: Danh sách Album (Đã gom nhóm, không trùng)
        public ObservableCollection<Album> FeaturedAlbums { get; set; } = new();

        // 👇 MỚI: Danh sách Ca sĩ (Đã gom nhóm, không trùng)
        public ObservableCollection<Album> TopArtists { get; set; } = new();

        // Expose AudioPlayer để View có thể binding (MiniPlayer)
        public AudioViewModel AudioPlayer => _audioViewModel;

        // ==========================================================
        // 2. CÁC BIẾN GIAO DIỆN (Menu, User...)
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
            LoadSongs(); // Gọi hàm tải và phân loại nhạc
        }

        // ==========================================================
        // 4. HÀM TẢI VÀ GOM NHÓM DỮ LIỆU (QUAN TRỌNG NHẤT)
        // ==========================================================
        // 👇 HÀM LoadSongs ĐÃ NÂNG CẤP (XỬ LÝ DẤU CÁCH THỪA)
        private async void LoadSongs()
        {
            try
            {
                var allSongs = await _musicService.GetSongsAsync();

                if (allSongs == null || allSongs.Count == 0) return;

                // 1. NẠP LIST BÀI HÁT (Recently Played)
                Playlist.Clear();
                foreach (var song in allSongs) Playlist.Add(song);

                // ---------------------------------------------------------
                // 2. XỬ LÝ ALBUM (Dùng Trim() để cắt khoảng trắng thừa)
                // ---------------------------------------------------------
                FeaturedAlbums.Clear();

                var uniqueAlbums = allSongs
                    .Where(s => !string.IsNullOrEmpty(s.Album))
                    // 👇 MỚI: .Trim() để "Tuyển tập A " giống "Tuyển tập A"
                    .GroupBy(s => s.Album.Trim())
                    .Select(g => g.First())
                    .ToList();

                foreach (var s in uniqueAlbums)
                {
                    FeaturedAlbums.Add(new Album
                    {
                        Title = s.Album.Trim(), // Hiển thị tên đã làm sạch
                        Artist = s.Artist ?? "Unknown",
                        CoverImage = s.CoverImage,
                        Description = "Album"
                    });
                }

                // ---------------------------------------------------------
                // 3. XỬ LÝ CA SĨ (Dùng Trim() để gộp Sơn Tùng lại)
                // ---------------------------------------------------------
                TopArtists.Clear();

                var uniqueArtists = allSongs
                    .Where(s => !string.IsNullOrEmpty(s.Artist))
                    // 👇 MỚI: .Trim() quan trọng nhất ở đây!
                    .GroupBy(s => s.Artist.Trim())
                    .Select(g => g.First())
                    .ToList();

                foreach (var s in uniqueArtists)
                {
                    TopArtists.Add(new Album
                    {
                        Title = s.Artist.Trim(), // Hiển thị tên đã làm sạch
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
        // 5. CÁC HÀM CẬP NHẬT USER (Giữ nguyên)
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
        // 6. CÁC LỆNH ĐIỀU HƯỚNG (COMMANDS)
        // ==========================================================

        // 👇 HÀM MỞ ALBUM HOẶC CA SĨ (Sửa đổi tham số thành Album)
        [RelayCommand]
        public async Task OpenAlbum(Album albumItem)
        {
            if (albumItem == null) return;

            // Chuyển sang trang AlbumDetailPage
            // Chúng ta gửi cả cục "albumItem" sang. Bên kia sẽ check xem nó là Album hay Artist
            var param = new Dictionary<string, object> { { "AlbumData", albumItem } };
            await Shell.Current.GoToAsync(nameof(AlbumDetailPage), param);
        }

        // Chọn bài hát lẻ để nghe ngay (Giữ nguyên)
        [RelayCommand]
        public async Task SelectSong(Song song)
        {
            if (song == null) return;

            // Check VIP
            bool isCurrentVip = Preferences.Get("IsPremium", false);
            if (song.IsPremium == true && isCurrentVip == false)
            {
                bool answer = await Shell.Current.DisplayAlert("Premium Content 👑", "Bài này dành cho VIP. Nâng cấp nhé?", "Xem gói VIP", "Để sau");
                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                return;
            }

            // Phát nhạc
            _audioViewModel.PlaySong(song, Playlist);
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }

        // Các lệnh Menu & MiniPlayer (Giữ nguyên như cũ)
        [RelayCommand] public async Task NavigateToPlayer() { if (_audioViewModel.CurrentSong != null) await Shell.Current.GoToAsync(nameof(PlayerPage)); }
        [RelayCommand] public async Task NavigateToSearch() { await Shell.Current.GoToAsync(nameof(SearchPage)); }

        [RelayCommand] public void TapUserAvatar() { IsUserMenuVisible = !IsUserMenuVisible; }
        [RelayCommand] public void CloseUserMenu() { IsUserMenuVisible = false; }
        [RelayCommand] public async Task OpenProfile() { IsUserMenuVisible = false; await Shell.Current.GoToAsync(nameof(ProfilePage)); }
        [RelayCommand] public async Task OpenSettings() { IsUserMenuVisible = false; await Shell.Current.GoToAsync(nameof(SettingsPage)); }

        [RelayCommand]
        public async Task PerformLogout()
        {
            IsUserMenuVisible = false;
            bool answer = await Shell.Current.DisplayAlert("Đăng xuất", "Bạn muốn thoát?", "Có", "Không");
            if (answer)
            {
                Preferences.Remove("AuthToken");
                Preferences.Remove("UserEmail");
                Preferences.Remove("UserName");
                Preferences.Remove("UserId");
                Preferences.Remove("IsPremium");
                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
            }
        }

        // Các lệnh placeholder
        [RelayCommand] public async Task AddAccount() { await Shell.Current.DisplayAlert("Thông báo", "Tính năng đang phát triển", "OK"); }
        [RelayCommand] public async Task OpenWhatsNew() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Mới", "Update tính năng Group Album!", "OK"); }
        [RelayCommand] public async Task OpenStats() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Thống kê", "Bạn đã nghe nhạc rất nhiều!", "OK"); }
        [RelayCommand] public async Task OpenHistory() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Gần đây", "Danh sách đã xem...", "OK"); }
    }
}