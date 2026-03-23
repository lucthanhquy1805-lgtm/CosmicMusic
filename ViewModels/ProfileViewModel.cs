
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Services;
using CosmicMusic.Views;

using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YoutubeExplode.Channels;

namespace CosmicMusic.ViewModels
{
    // Class gửi thư báo đổi ảnh cho toàn bộ App
    public class UserAvatarChangedMessage
    {
        public string NewAvatarUrl { get; }
        public UserAvatarChangedMessage(string url) => NewAvatarUrl = url;
    }

    public partial class ProfileViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private readonly HttpClient _httpClient = new HttpClient();

        // Gọi đến S3Service của bạn
        private readonly S3Service _s3Service;

        [ObservableProperty] private string _userName;
        [ObservableProperty] private string _userEmail;
        [ObservableProperty] private string _userAvatarText;
        [ObservableProperty] private bool _isEditing;
        [ObservableProperty] private string _newName;
        [ObservableProperty] private string _newPassword;

        [ObservableProperty] private bool _isPremiumUser;
        [ObservableProperty] private string _avatarBorderColor;
        [ObservableProperty] private string _totalPlaylists = "...";
        [ObservableProperty] private string _totalFavorites = "...";

        [ObservableProperty] private ObservableCollection<CosmicMusic.Models.Playlist> _userPlaylists = new();
        [ObservableProperty] private bool _hasPlaylists = false;

        [ObservableProperty] private string _photoUrl;
        [ObservableProperty] private bool _isBusy;

        // Khởi tạo có chứa S3Service
        public ProfileViewModel(FirestoreService firestoreService, S3Service s3Service)
        {
            _firestoreService = firestoreService;
            _s3Service = s3Service;
            LoadUserData();
            _ = LoadRealStatsAsync();
        }

        public void LoadUserData()
        {
            UserEmail = Preferences.Get("UserEmail", "Unknown Email");
            UserName = Preferences.Get("UserName", "Cosmic User");

            // Lấy link ảnh từ bộ nhớ (nếu có)
            PhotoUrl = Preferences.Get("UserPhotoUrl", "");

            // Lấy chữ cái đầu
            if (!string.IsNullOrEmpty(UserName) && UserName.Length > 0)
                UserAvatarText = UserName.Substring(0, 1).ToUpper();
            else if (!string.IsNullOrEmpty(UserEmail) && UserEmail.Length > 0)
                UserAvatarText = UserEmail.Substring(0, 1).ToUpper();

            IsPremiumUser = Preferences.Get("IsPremium", false);
            AvatarBorderColor = IsPremiumUser ? "#FFD700" : "#D946EF";
        }

        private async Task LoadRealStatsAsync()
        {
            string uid = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(uid)) return;

            try
            {
                var playlists = await _firestoreService.GetUserPlaylists(uid);
                var favorites = await _firestoreService.GetFavoritesAsync();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TotalPlaylists = playlists != null ? playlists.Count.ToString() : "0";
                    TotalFavorites = favorites != null ? favorites.Count.ToString() : "0";

                    UserPlaylists.Clear();
                    if (playlists != null && playlists.Count > 0)
                    {
                        foreach (var p in playlists)
                        {
                            if (string.IsNullOrEmpty(p.CoverImage)) p.CoverImage = "cover_chill.jpg";
                            UserPlaylists.Add(p);
                        }
                        HasPlaylists = true;
                    }
                    else HasPlaylists = false;
                });
            }
            catch { /* Bỏ qua lỗi mạng */ }
        }

        // ========================================================
        // LOGIC UP ẢNH LÊN AWS S3 VÀ LƯU VÀO FIRESTORE
        // ========================================================
        [RelayCommand]
        public async Task EditAvatar()
        {
            if (IsBusy) return;

            try
            {
                var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = "Chọn ảnh đại diện mới" });
                if (photo == null) return;

                IsBusy = true;

                using var stream = await photo.OpenReadAsync();
                string uid = Preferences.Get("UserId", "");
                if (string.IsNullOrEmpty(uid)) throw new Exception("Không tìm thấy thông tin đăng nhập.");

                // 1. TẢI ẢNH LÊN S3
                string fileName = $"avatar_{DateTime.Now.Ticks}.jpg";

                // 👇 Đã gọi đúng hàm UploadImageAsync trong S3Service của bạn 👇
                string downloadUrl = await _s3Service.UploadImageAsync(stream, fileName);

                if (string.IsNullOrEmpty(downloadUrl)) throw new Exception("Tải lên S3 thất bại.");

                // 2. LƯU LINK S3 VÀO FIRESTORE DATABASE
                await _firestoreService.UpdateUserAvatarAsync(uid, downloadUrl);

                // 3. LƯU LINK VÀO FIREBASE AUTH
                string idToken = Preferences.Get("AuthToken", "");
                string apiKey = Constants.FirebaseApiKey;
                string url = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={apiKey}";

                var payload = new { idToken = idToken, photoUrl = downloadUrl, returnSecureToken = true };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(url, content);

                // 4. LƯU LINK VÀO BỘ NHỚ ĐỂ HIỂN THỊ
                Preferences.Set("UserPhotoUrl", downloadUrl);
                PhotoUrl = downloadUrl;

                // Thông báo cho toàn App biết Avatar đã đổi
                WeakReferenceMessenger.Default.Send(new UserAvatarChangedMessage(downloadUrl));

                await Shell.Current.DisplayAlert("Thành công", "Ảnh đại diện đã được cập nhật tuyệt đẹp! ✨", "Tuyệt vời");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Lỗi", $"Đã xảy ra sự cố: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ==========================================
        // CÁC COMMAND KHÁC
        // ==========================================
        [RelayCommand] public async Task GoBack() => await Shell.Current.GoToAsync("..");
        [RelayCommand] public async Task OpenEditPage() => await Shell.Current.GoToAsync(nameof(EditProfilePage));
        [RelayCommand] public async Task OpenSettings() => await Shell.Current.GoToAsync(nameof(SettingsPage));
        [RelayCommand] public async Task OpenPremium() => await Shell.Current.GoToAsync("PremiumPage");

        [RelayCommand]
        public async Task Logout()
        {
            bool answer = await Shell.Current.DisplayAlert("Đăng xuất", "Bạn có chắc chắn muốn rời khỏi vũ trụ Cosmic Music?", "Đăng xuất", "Ở lại");
            if (answer)
            {
                Preferences.Clear();
                await Shell.Current.GoToAsync($"///{nameof(LoginPage)}");
            }
        }

        [RelayCommand]
        public async Task OpenPlaylist(CosmicMusic.Models.Playlist selectedPlaylist)
        {
            if (selectedPlaylist == null) return;
            await Shell.Current.DisplayAlert("Mở Playlist", $"Bạn vừa chọn Playlist: {selectedPlaylist.Name}", "OK");
        }
    }
}