using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Services;
using System.Text.Json;
using System.Text;

namespace CosmicMusic.ViewModels
{
    public partial class ProfileViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _userName;

        [ObservableProperty]
        private string _userEmail;

        [ObservableProperty]
        private string _userAvatarText;

        [ObservableProperty]
        private bool _isEditing; // Biến để bật/tắt chế độ chỉnh sửa

        [ObservableProperty]
        private string _newName;

        [ObservableProperty]
        private string _newPassword;

        public ProfileViewModel()
        {
            LoadUserData();
        }

        public void LoadUserData()
        {
            // Lấy dữ liệu từ bộ nhớ
            UserEmail = Preferences.Get("UserEmail", "Unknown Email");
            UserName = Preferences.Get("UserName", "Cosmic User");

            // Xử lý Avatar chữ cái
            if (!string.IsNullOrEmpty(UserEmail))
                UserAvatarText = UserEmail.Substring(0, 1).ToUpper();

            // Gán giá trị mặc định cho ô nhập liệu
            NewName = UserName;
            IsEditing = false;
        }

        [RelayCommand]
        public async Task GoBack()
        {
            await Shell.Current.GoToAsync("..");
        }

        // --- Mở trang chỉnh sửa (Thực tế là hiện Popup hoặc chuyển trang) ---
        [RelayCommand]
        public async Task OpenEditPage()
        {
            // Chúng ta sẽ dùng chính trang này nhưng bật chế độ Edit lên (hoặc chuyển trang riêng nếu muốn)
            // Ở đây tôi làm demo chuyển sang trang EditProfilePage cho chuyên nghiệp
            await Shell.Current.GoToAsync(nameof(Views.EditProfilePage));
        }
        [RelayCommand]
       
        public async Task OpenSettings()
        {
            // Chuyển sang trang Cài đặt (SettingsPage)
            await Shell.Current.GoToAsync(nameof(Views.SettingsPage));
        }
    }
}