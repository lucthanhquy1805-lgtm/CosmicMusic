using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Views;
using System.Text;          // <-- Kiểm tra kỹ dòng này
using System.Text.Json;     // <-- Và dòng này

namespace CosmicMusic.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        [ObservableProperty] private string _fullName;
        [ObservableProperty] private string _email;
        [ObservableProperty] private string _password;
        [ObservableProperty] private string _confirmPassword;
        [ObservableProperty] private bool _isBusy;

        // Biến để ẩn/hiện mật khẩu (cho icon con mắt)
        [ObservableProperty] private bool _isPasswordHidden = true;

        private readonly HttpClient _httpClient;

        public RegisterViewModel()
        {
            _httpClient = new HttpClient();
        }

        [RelayCommand]
        public void TogglePasswordVisibility()
        {
            IsPasswordHidden = !IsPasswordHidden;
        }

        [RelayCommand]
        public async Task Register()
        {
            // 1. Kiểm tra nhập liệu
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Shell.Current.DisplayAlert("Lỗi", "Vui lòng điền đầy đủ thông tin!", "OK");
                return;
            }

            if (Password != ConfirmPassword)
            {
                await Shell.Current.DisplayAlert("Lỗi", "Mật khẩu xác nhận không khớp!", "OK");
                return;
            }

            if (Password.Length < 6)
            {
                await Shell.Current.DisplayAlert("Lỗi", "Mật khẩu phải dài hơn 6 ký tự!", "OK");
                return;
            }

            IsBusy = true;

            try
            {
                // 2. GỬI YÊU CẦU "SIGN UP" LÊN GOOGLE (Endpoint khác với Login)
                string apiKey = Constants.FirebaseApiKey;
                string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={apiKey}";

                var payload = new
                {
                    email = Email,
                    password = Password,
                    returnSecureToken = true
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                string responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // === ĐĂNG KÝ THÀNH CÔNG ===
                    // (Tạm thời chúng ta chưa lưu FullName lên Server để đơn giản hóa,
                    // chỉ cần tạo tài khoản thành công là được)

                    bool answer = await Shell.Current.DisplayAlert("Thành công", "Tài khoản đã được tạo! Hãy đăng nhập ngay.", "Đăng nhập", "Ở lại");

                    if (answer)
                    {
                        // Quay về trang Login
                        await Shell.Current.GoToAsync("..");
                    }
                }
                else
                {
                    // === LỖI TỪ GOOGLE (VD: Email đã tồn tại) ===
                    await Shell.Current.DisplayAlert("Đăng ký thất bại", "Email này có thể đã được sử dụng.", "OK");
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

        [RelayCommand]
        public async Task NavigateBackToLogin()
        {
            // Quay lại trang cũ
            await Shell.Current.GoToAsync("..");
        }
    }
}