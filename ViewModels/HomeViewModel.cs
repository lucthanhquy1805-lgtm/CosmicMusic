using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;

namespace CosmicMusic.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        // 1. Khai báo dịch vụ
        private readonly MusicApiService _musicService;
        private readonly AudioViewModel _audioViewModel;

        // 2. Danh sách bài hát & Audio Player
        public ObservableCollection<Song> Playlist { get; set; } = new();
        public AudioViewModel AudioPlayer => _audioViewModel;

        // 3. Biến giao diện (Menu, Avatar)
        [ObservableProperty]
        private bool _isUserMenuVisible = false;

        [ObservableProperty]
        private string _userAvatarText;

        // Biến tên người dùng hiển thị
        [ObservableProperty]
        private string _userName;

        // Biến theo dõi trạng thái VIP
        [ObservableProperty]
        private bool _isPremiumUser;

        [ObservableProperty]
        private string _avatarBorderColor = "#6C63FF";

        // 4. Hàm khởi tạo
        public HomeViewModel(MusicApiService musicService, AudioViewModel audioViewModel)
        {
            _musicService = musicService;
            _audioViewModel = audioViewModel;
            LoadSongs();
        }

        private async void LoadSongs()
        {
            try
            {
                var allSongs = await _musicService.GetSongsAsync();
                Playlist.Clear();

                var recentSongs = allSongs.Skip(5).Take(5).ToList();

                // DỮ LIỆU GIẢ VIP (Bài 2 và 4)
                for (int i = 0; i < recentSongs.Count; i++)
                {
                    var song = recentSongs[i];
                    if (i == 1 || i == 3)
                    {
                        song.IsPremium = true;
                        song.Title += " (👑)";
                    }
                    else song.IsPremium = false;
                    Playlist.Add(song);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi: {ex.Message}"); }
        }

        // --- CẬP NHẬT TRẠNG THÁI NGƯỜI DÙNG ---
        public void LoadUserAvatar()
        {
            string email = Preferences.Get("UserEmail", "");
            UserAvatarText = !string.IsNullOrEmpty(email) ? email.Substring(0, 1).ToUpper() : "?";

            // Xử lý Tên hiển thị (Ưu tiên tên thật)
            string savedFullName = Preferences.Get("UserName", "");

            if (!string.IsNullOrEmpty(savedFullName))
            {
                UserName = savedFullName;
            }
            else if (!string.IsNullOrEmpty(email))
            {
                int atIndex = email.IndexOf('@');
                string namePart = atIndex > 0 ? email.Substring(0, atIndex) : email;
                UserName = (namePart.Length > 0) ? char.ToUpper(namePart[0]) + namePart.Substring(1) : namePart;
            }
            else
            {
                UserName = "Cosmic Guest";
            }

            CheckPremiumStatus();
        }

        private void CheckPremiumStatus()
        {
            bool isSessionVip = Preferences.Get("IsPremium", false);
            string email = Preferences.Get("UserEmail", "");
            string userVipKey = $"VIP_{email}";
            bool isHistoryVip = Preferences.Get(userVipKey, false);

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

        // --- CHỌN BÀI HÁT ---
        [RelayCommand]
        public async Task SelectSong(Song song)
        {
            if (song == null) return;

            bool isCurrentVip = Preferences.Get("IsPremium", false);

            if (song.IsPremium == true && isCurrentVip == false)
            {
                bool answer = await Shell.Current.DisplayAlert(
                    "Premium Content 👑",
                    "Bài hát này dành riêng cho tài khoản VIP. Nâng cấp ngay để mở khóa?",
                    "Xem gói VIP", "Để sau");

                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                return;
            }

            var libraryItem = new LibraryItem
            {
                Title = song.Title,
                Subtitle = song.Artist,
                CoverImage = song.CoverImage,
                Url = song.AudioUrl,
                ImageColor = "#120520"
            };

            var navigationParameter = new Dictionary<string, object> { { "SongData", libraryItem } };
            _audioViewModel.PlaySong(song, Playlist);
            await Shell.Current.GoToAsync(nameof(PlayerPage), navigationParameter);
        }

        // --- ĐIỀU HƯỚNG ---
        [RelayCommand] public async Task NavigateToPlayer() { if (_audioViewModel.CurrentSong != null) await Shell.Current.GoToAsync(nameof(PlayerPage)); }
        [RelayCommand] public async Task NavigateToSearch() { await Shell.Current.GoToAsync(nameof(SearchPage)); }
        [RelayCommand]
        public async Task OpenAlbum(Song song)
        {
            if (song == null) return;
            var album = new Album { Title = song.Title, Artist = song.Artist, CoverImage = song.CoverImage, Description = $"Album by {song.Artist} • 2023" };
            var param = new Dictionary<string, object> { { "AlbumData", album } };
            await Shell.Current.GoToAsync(nameof(AlbumDetailPage), param);
        }

        // --- MENU SIDE DRAWER ---
        [RelayCommand] public void TapUserAvatar() { IsUserMenuVisible = !IsUserMenuVisible; }
        [RelayCommand] public void CloseUserMenu() { IsUserMenuVisible = false; }

        [RelayCommand]
        public async Task OpenProfile()
        {
            IsUserMenuVisible = false;
            await Shell.Current.GoToAsync(nameof(ProfilePage));
        }

        [RelayCommand] public async Task AddAccount() { await Shell.Current.DisplayAlert("Thông báo", "Chức năng Thêm tài khoản đang phát triển", "OK"); }
        [RelayCommand] public async Task OpenWhatsNew() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Cosmic Music v1.0", "- Giao diện vũ trụ mới\n- Âm thanh chất lượng cao", "Tuyệt"); }
        [RelayCommand] public async Task OpenStats() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Thống kê", "Bạn đã nghe nhạc 120 phút hôm nay!", "OK"); }
        [RelayCommand] public async Task OpenHistory() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Gần đây", "Danh sách bài hát vừa nghe...", "OK"); }

        // 👇 ĐÃ SỬA: Chuyển hướng sang SettingsPage thật
        [RelayCommand]
        public async Task OpenSettings()
        {
            IsUserMenuVisible = false;
            await Shell.Current.GoToAsync(nameof(SettingsPage));
        }

        // 👇 ĐÃ SỬA: Logic Đăng xuất an toàn (Không xóa VIP)
        [RelayCommand]
        public async Task PerformLogout()
        {
            IsUserMenuVisible = false;
            bool answer = await Shell.Current.DisplayAlert("Đăng xuất", "Bạn có chắc chắn muốn thoát vũ trụ âm nhạc không?", "Có", "Không");

            if (answer)
            {
                // ✅ CHỈ XÓA PHIÊN HIỆN TẠI - GIỮ LẠI LỊCH SỬ VIP
                Preferences.Remove("AuthToken");
                Preferences.Remove("UserEmail");
                Preferences.Remove("UserName");
                Preferences.Remove("UserId");
                Preferences.Remove("IsPremium");

                // Lưu ý: Không dùng Preferences.Clear() để tránh mất dữ liệu "VIP_email..."

                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
            }
        }
    }
}