using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Services;
using CosmicMusic.Views; // Để nhận diện được EditProfilePage, SettingsPage
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls;

namespace CosmicMusic.ViewModels
{
    public partial class ProfileViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        // ==========================================
        // 1. CÁC BIẾN CỦA BẠN (CŨ)
        // ==========================================
        [ObservableProperty] private string _userName;
        [ObservableProperty] private string _userEmail;
        [ObservableProperty] private string _userAvatarText;
        [ObservableProperty] private bool _isEditing;
        [ObservableProperty] private string _newName;
        [ObservableProperty] private string _newPassword;

        // ==========================================
        // 2. CÁC BIẾN GIAO DIỆN (MỚI)
        // ==========================================
        [ObservableProperty] private bool _isPremiumUser;
        [ObservableProperty] private string _avatarBorderColor;
        [ObservableProperty] private string _totalPlaylists = "..."; // Để "..." lúc đang load mạng
        [ObservableProperty] private string _totalFavorites = "...";
        [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<CosmicMusic.Models.Playlist> _userPlaylists = new();
        [ObservableProperty] private bool _hasPlaylists = false;

        // Yêu cầu tiêm FirestoreService vào để lấy số liệu thật
        public ProfileViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            LoadUserData();

            // Chạy ngầm lệnh đếm số liệu để không làm đơ giao diện khi mở trang
            _ = LoadRealStatsAsync();
        }

        public void LoadUserData()
        {
            // Lấy dữ liệu từ bộ nhớ
            UserEmail = Preferences.Get("UserEmail", "Unknown Email");
            UserName = Preferences.Get("UserName", "Cosmic User");

            // Xử lý Avatar chữ cái
            if (!string.IsNullOrEmpty(UserEmail) && UserEmail.Length > 0)
                UserAvatarText = UserEmail.Substring(0, 1).ToUpper();

            // Gán giá trị mặc định cho ô nhập liệu
            NewName = UserName;
            IsEditing = false;

            // Kiểm tra trạng thái VIP
            IsPremiumUser = Preferences.Get("IsPremium", false);
            AvatarBorderColor = IsPremiumUser ? "#FFD700" : "#D946EF";
        }

        // 👇 NÂNG CẤP: Lấy số liệu đếm thật từ Firebase 👇
        private async Task LoadRealStatsAsync()
        {
            string uid = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(uid)) return;

            try
            {
                // Lấy danh sách từ Firebase
                var playlists = await _firestoreService.GetUserPlaylists(uid);
                var favorites = await _firestoreService.GetFavoritesAsync();

                // Cập nhật giao diện an toàn trên luồng chính
                // Cập nhật giao diện an toàn trên luồng chính
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TotalPlaylists = playlists != null ? playlists.Count.ToString() : "0";
                    TotalFavorites = favorites != null ? favorites.Count.ToString() : "0";

                    // 👇 ĐỔ DỮ LIỆU PLAYLIST VÀO DANH SÁCH 👇
                    UserPlaylists.Clear();
                    if (playlists != null && playlists.Count > 0)
                    {
                        foreach (var p in playlists)
                        {
                            // Đảm bảo có ảnh bìa mặc định nếu Playlist chưa có ảnh
                            if (string.IsNullOrEmpty(p.CoverImage)) p.CoverImage = "cover_chill.jpg";
                            UserPlaylists.Add(p);
                        }
                        HasPlaylists = true;
                    }
                    else
                    {
                        HasPlaylists = false;
                    }
                });
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tải thống kê: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TotalPlaylists = "0";
                    TotalFavorites = "0";
                });
            }
        }

        // ==========================================
        // 3. CÁC LỆNH ĐIỀU HƯỚNG (COMMANDS)
        // ==========================================
        [RelayCommand]
        public async Task GoBack()
        {
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        public async Task OpenEditPage()
        {
            await Shell.Current.GoToAsync(nameof(EditProfilePage));
        }

        [RelayCommand]
        public async Task OpenSettings()
        {
            await Shell.Current.GoToAsync(nameof(SettingsPage));
        }

        [RelayCommand]
        public async Task OpenPremium()
        {
            await Shell.Current.GoToAsync(nameof(PremiumPage));
        }

        [RelayCommand]
        public async Task Logout()
        {
            bool answer = await Shell.Current.DisplayAlert("Đăng xuất", "Bạn có chắc chắn muốn rời khỏi vũ trụ Cosmic Music?", "Đăng xuất", "Ở lại");

            if (answer)
            {
                // Xóa sạch bộ nhớ phiên đăng nhập
                Preferences.Clear();

                // Quay về màn hình Login tuyệt đối
                await Shell.Current.GoToAsync($"///{nameof(LoginPage)}");
            }
        }
        [RelayCommand]
        public async Task OpenPlaylist(CosmicMusic.Models.Playlist selectedPlaylist)
        {
            if (selectedPlaylist == null) return;
            // Tạm thời hiện thông báo. Bạn có thể thay bằng lệnh GoToAsync mở trang Playlist Detail sau.
            await Shell.Current.DisplayAlert("Mở Playlist", $"Bạn vừa chọn Playlist: {selectedPlaylist.Name}", "OK");
        }
    }
}