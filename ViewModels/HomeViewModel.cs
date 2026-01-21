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
        // 1. Khai báo các dịch vụ và biến riêng tư
        private readonly MusicApiService _musicService;
        private readonly AudioViewModel _audioViewModel;

        // 2. Danh sách bài hát
        public ObservableCollection<Song> Playlist { get; set; } = new();

        // 3. Audio Player và Biến điều khiển Menu
        public AudioViewModel AudioPlayer => _audioViewModel;

        // Biến ẩn/hiện Menu Dropdown
        [ObservableProperty]
        private bool _isUserMenuVisible = false;

        // Biến hiển thị chữ cái Avatar
        [ObservableProperty]
        private string _userAvatarText;

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

                // Lấy 5 bài (Giả lập logic)
                var recentSongs = allSongs.Skip(5).Take(5).ToList();

                // TẠO DỮ LIỆU GIẢ ĐỂ TEST VIP
                for (int i = 0; i < recentSongs.Count; i++)
                {
                    var song = recentSongs[i];
                    if (i == 1 || i == 3)
                    {
                        song.IsPremium = true;
                        song.Title += " (👑)";
                    }
                    else
                    {
                        song.IsPremium = false;
                    }
                    Playlist.Add(song);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tải nhạc: {ex.Message}");
            }
        }

        // --- HÀM 1: CHỌN BÀI HÁT ---
        [RelayCommand]
        public async Task SelectSong(Song song)
        {
            if (song == null) return;

            bool isUserPremium = Preferences.Get("IsPremium", false);

            if (song.IsPremium == true && isUserPremium == false)
            {
                bool answer = await Shell.Current.DisplayAlert(
                    "Premium Content 👑",
                    "Bài hát này dành riêng cho tài khoản VIP.",
                    "Nâng cấp", "Để sau");

                if (answer) await Shell.Current.DisplayAlert("Info", "Chức năng thanh toán đang phát triển", "OK");
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

            var navigationParameter = new Dictionary<string, object>
            { { "SongData", libraryItem } };

            _audioViewModel.PlaySong(song, Playlist);
            await Shell.Current.GoToAsync(nameof(PlayerPage), navigationParameter);
        }

        // --- HÀM 2: CÁC ĐIỀU HƯỚNG CƠ BẢN ---
        [RelayCommand]
        public async Task NavigateToPlayer()
        {
            if (_audioViewModel.CurrentSong != null)
            {
                // Logic tạo LibraryItem từ CurrentSong...
                // (Giản lược bớt code lặp để gọn, nhưng bạn giữ nguyên logic cũ cũng được)
                await Shell.Current.GoToAsync(nameof(PlayerPage));
            }
        }

        [RelayCommand]
        public async Task NavigateToSearch()
        {
            await Shell.Current.GoToAsync(nameof(SearchPage));
        }

        [RelayCommand]
        public async Task OpenAlbum(Song song)
        {
            if (song == null) return;
            var album = new Album
            {
                Title = song.Title,
                Artist = song.Artist,
                CoverImage = song.CoverImage,
                Description = $"Album by {song.Artist} • 2023"
            };
            var navigationParameter = new Dictionary<string, object>
            { { "AlbumData", album } };
            await Shell.Current.GoToAsync(nameof(AlbumDetailPage), navigationParameter);
        }


        // ==========================================================
        // 👇 PHẦN LOGIC CHO USER MENU & AVATAR (MỚI) 👇
        // ==========================================================

        public void LoadUserAvatar()
        {
            string email = Preferences.Get("UserEmail", "");
            if (!string.IsNullOrEmpty(email))
            {
                UserAvatarText = email.Substring(0, 1).ToUpper();
            }
            else
            {
                UserAvatarText = "?";
            }
        }

        // 1. Bấm vào Avatar -> Bật/Tắt Menu
        [RelayCommand]
        public void TapUserAvatar()
        {
            IsUserMenuVisible = !IsUserMenuVisible;
        }

        // 2. Bấm vào nền mờ -> Đóng Menu
        [RelayCommand]
        public void CloseUserMenu()
        {
            IsUserMenuVisible = false;
        }

        // 3. Bấm vào "Thông tin cá nhân"
        [RelayCommand]
        public async Task OpenProfile()
        {
            IsUserMenuVisible = false; // Đóng menu trước
            await Shell.Current.DisplayAlert("Cosmic Info", "Tính năng Hồ sơ đang phát triển!", "OK");
        }

        // 4. Bấm vào "Cài đặt"
        [RelayCommand]
        public async Task OpenSettings()
        {
            IsUserMenuVisible = false;
            await Shell.Current.DisplayAlert("Cosmic Settings", "Tính năng Cài đặt đang phát triển!", "OK");
        }

        // 5. Bấm vào "Đăng xuất" (Sửa lại hàm cũ của bạn thành Public Command)
        [RelayCommand]
        public async Task PerformLogout()
        {
            IsUserMenuVisible = false; // Đóng menu trước

            // Hỏi xác nhận
            bool answer = await Shell.Current.DisplayAlert("Đăng xuất", "Bạn có chắc chắn muốn thoát vũ trụ không?", "Có", "Không");

            if (answer)
            {
                // Xóa dữ liệu đăng nhập
                Preferences.Clear();
                // Quay về trang Login
                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
            }
        }
    }
}