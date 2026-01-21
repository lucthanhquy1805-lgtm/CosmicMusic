using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Views;
using System.Text;
using System.Text.Json; // 👇 Dùng thư viện có sẵn của .NET

namespace CosmicMusic.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _email;

        [ObservableProperty]
        private string _password;

        [ObservableProperty]
        private bool _isBusy;

        private readonly HttpClient _httpClient;

        public LoginViewModel()
        {
            _httpClient = new HttpClient();
        }

        [RelayCommand]
        public async Task Login()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Shell.Current.DisplayAlert("Lỗi", "Vui lòng nhập Email và Mật khẩu", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                // 1. GỬI LỆNH ĐĂNG NHẬP TRỰC TIẾP LÊN GOOGLE
                string apiKey = Constants.FirebaseApiKey;
                string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={apiKey}";

                var payload = new
                {
                    email = Email,
                    password = Password,
                    returnSecureToken = true
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // 2. NHẬN KẾT QUẢ
                var response = await _httpClient.PostAsync(url, content);
                string responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // === THÀNH CÔNG ===
                    var result = JsonSerializer.Deserialize<FirebaseAuthResult>(responseString);

                    // Tạo User
                    var user = new CosmicMusic.Models.User
                    {
                        Uid = result.localId,
                        Email = result.email,
                        DisplayName = result.displayName ?? "Cosmic Traveler"
                    };

                    // Phân quyền Admin
                    if (user.Email.ToLower().Contains("admin"))
                    {
                        user.IsPremium = true;
                    }
                    else
                    {
                        user.IsPremium = false;
                    }

                    // Lưu thông tin
                    Preferences.Set("IsPremium", user.IsPremium);
                    Preferences.Set("UserEmail", user.Email);

                    // Vào App
                    await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
                }
                else
                {
                    // === THẤT BẠI ===
                    await Shell.Current.DisplayAlert("Đăng nhập thất bại", "Kiểm tra lại Email/Pass", "OK");
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
        public async Task NavigateToRegister()
        {
            await Shell.Current.GoToAsync(nameof(RegisterPage));
        }
    }

    // Class hứng dữ liệu (Copy vào cuối file LoginViewModel.cs luôn cũng được)
    public class FirebaseAuthResult
    {
        public string localId { get; set; }
        public string email { get; set; }
        public string displayName { get; set; }
        public string idToken { get; set; }
    }
}