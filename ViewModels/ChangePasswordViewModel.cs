using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text;
using System.Text.Json;

namespace CosmicMusic.ViewModels
{
    public partial class ChangePasswordViewModel : ObservableObject
    {
        [ObservableProperty] private string _oldPassword;
        [ObservableProperty] private string _newPassword;
        [ObservableProperty] private string _confirmPassword;
        [ObservableProperty] private bool _isBusy;

        private readonly HttpClient _httpClient = new HttpClient();

        [RelayCommand]
        public async Task ChangePassword()
        {
            // 1. KIỂM TRA ĐẦU VÀO
            if (string.IsNullOrWhiteSpace(OldPassword) || string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                await Shell.Current.DisplayAlert("Lỗi", "Vui lòng nhập đầy đủ thông tin", "OK");
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                await Shell.Current.DisplayAlert("Lỗi", "Mật khẩu xác nhận không khớp", "OK");
                return;
            }

            if (NewPassword.Length < 6)
            {
                await Shell.Current.DisplayAlert("Lỗi", "Mật khẩu mới phải có ít nhất 6 ký tự", "OK");
                return;
            }

            IsBusy = true;

            try
            {
                string apiKey = Constants.FirebaseApiKey;
                string email = Preferences.Get("UserEmail", "");

                // 2. BƯỚC QUAN TRỌNG: KIỂM TRA MẬT KHẨU CŨ (Đăng nhập lại)
                // Gửi email + mật khẩu cũ lên Google xem có đúng không
                string loginUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={apiKey}";
                var loginPayload = new { email = email, password = OldPassword, returnSecureToken = true };

                var loginResponse = await _httpClient.PostAsync(loginUrl, new StringContent(JsonSerializer.Serialize(loginPayload), Encoding.UTF8, "application/json"));

                if (!loginResponse.IsSuccessStatusCode)
                {
                    IsBusy = false;
                    await Shell.Current.DisplayAlert("Thất bại", "Mật khẩu cũ không chính xác. Vui lòng kiểm tra lại!", "Thử lại");
                    return;
                }

                // Nếu đúng mật khẩu cũ -> Lấy cái ID Token mới toanh này để dùng
                var loginResultJson = await loginResponse.Content.ReadAsStringAsync();
                var loginResult = JsonSerializer.Deserialize<JsonElement>(loginResultJson);
                string idToken = loginResult.GetProperty("idToken").GetString();

                // 3. TIẾN HÀNH CẬP NHẬT MẬT KHẨU MỚI
                string updateUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={apiKey}";
                var updatePayload = new
                {
                    idToken = idToken,
                    password = NewPassword,
                    returnSecureToken = true
                };

                var updateResponse = await _httpClient.PostAsync(updateUrl, new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json"));

                if (updateResponse.IsSuccessStatusCode)
                {
                    // Cập nhật Token mới vào máy (để dùng cho các lần sau)
                    var updateResultJson = await updateResponse.Content.ReadAsStringAsync();
                    var updateResult = JsonSerializer.Deserialize<JsonElement>(updateResultJson);

                    if (updateResult.TryGetProperty("idToken", out var newToken))
                    {
                        Preferences.Set("AuthToken", newToken.GetString());
                    }

                    await Shell.Current.DisplayAlert("Thành công", "Đổi mật khẩu thành công!", "OK");

                    // Quay về trang trước
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Lỗi", "Hệ thống đang bận. Vui lòng thử lại sau.", "OK");
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
        public async Task Cancel()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}