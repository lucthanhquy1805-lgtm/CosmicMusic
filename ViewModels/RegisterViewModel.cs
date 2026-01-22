using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Services; // 👇 Nhớ namespace này
using CosmicMusic.Views;
using System.Text;
using System.Text.Json;

namespace CosmicMusic.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        [ObservableProperty] private string _fullName;
        [ObservableProperty] private string _email;
        [ObservableProperty] private string _password;
        [ObservableProperty] private string _confirmPassword;
        [ObservableProperty] private bool _isBusy;

        private readonly HttpClient _httpClient;
        private readonly FirestoreService _firestoreService; // 👇 1. Khai báo Service

        // 👇 2. Tiêm FirestoreService
        public RegisterViewModel(FirestoreService firestoreService)
        {
            _httpClient = new HttpClient();
            _firestoreService = firestoreService;
        }

        [RelayCommand]
        public async Task Register()
        {
            // Validate đầu vào
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Shell.Current.DisplayAlert("Lỗi", "Vui lòng nhập đầy đủ thông tin", "OK");
                return;
            }

            if (Password != ConfirmPassword)
            {
                await Shell.Current.DisplayAlert("Lỗi", "Mật khẩu xác nhận không khớp", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                // A. TẠO TÀI KHOẢN BÊN AUTHENTICATION (Cũ)
                string apiKey = Constants.FirebaseApiKey;
                string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={apiKey}";

                var payload = new { email = Email, password = Password, returnSecureToken = true };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                string responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Lấy UID vừa tạo
                    var result = JsonSerializer.Deserialize<FirebaseAuthResult>(responseString);
                    string uid = result.localId;
                    string idToken = result.idToken;

                    // ==========================================================
                    // 👇 B. LƯU HỒ SƠ SANG FIRESTORE (MỚI) 👇
                    // ==========================================================

                    // Lưu ngay Tên thật + VIP = false (Mặc định)
                    await _firestoreService.UpdateUserAsync(uid, Email, FullName, false);

                    // C. CẬP NHẬT DISPLAY NAME CHO AUTHENTICATION (Để đồng bộ cả 2 bên)
                    await UpdateAuthDisplayName(idToken, FullName);

                    await Shell.Current.DisplayAlert("Thành công", "Tài khoản đã được khởi tạo trên hệ thống đám mây!", "Đăng nhập ngay");

                    // Quay về trang đăng nhập
                    await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
                }
                else
                {
                    // Parse lỗi từ Firebase để báo chi tiết hơn (ví dụ: Email tồn tại)
                    await Shell.Current.DisplayAlert("Lỗi", "Đăng ký thất bại. Email có thể đã được sử dụng.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Lỗi mạng", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Hàm phụ: Cập nhật tên hiển thị bên Auth (cho đủ bộ)
        private async Task UpdateAuthDisplayName(string idToken, string name)
        {
            try
            {
                string url = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={Constants.FirebaseApiKey}";
                var payload = new { idToken = idToken, displayName = name, returnSecureToken = false };
                await _httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            }
            catch { /* Lỗi cập nhật tên phụ không quan trọng lắm, bỏ qua */ }
        }

        [RelayCommand]
        public async Task NavigateToLogin()
        {
            await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
        }
    }
}