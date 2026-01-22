using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text;
using System.Text.Json;

namespace CosmicMusic.ViewModels
{
    public partial class EditProfileViewModel : ObservableObject
    {
        // Chỉ còn biến Tên Mới
        [ObservableProperty]
        private string _newName;

        [ObservableProperty]
        private bool _isBusy;

        private readonly HttpClient _httpClient = new HttpClient();

        public EditProfileViewModel()
        {
            // Lấy tên hiện tại điền sẵn vào ô nhập
            NewName = Preferences.Get("UserName", "");
        }

        [RelayCommand]
        public async Task SaveChanges()
        {
            // Validate: Không được để tên trống
            if (string.IsNullOrWhiteSpace(NewName))
            {
                await Shell.Current.DisplayAlert("Lỗi", "Tên hiển thị không được để trống", "OK");
                return;
            }

            IsBusy = true;

            try
            {
                // Lấy Token và Key
                string idToken = Preferences.Get("AuthToken", "");
                string apiKey = Constants.FirebaseApiKey;

                // URL cập nhật thông tin
                string url = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={apiKey}";

                // 👇 QUAN TRỌNG: Payload chỉ chứa displayName, KHÔNG có password
                var payload = new
                {
                    idToken = idToken,
                    displayName = NewName,
                    returnSecureToken = true
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Gửi yêu cầu
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    // 1. Lưu tên mới vào bộ nhớ máy
                    Preferences.Set("UserName", NewName);

                    // 2. Cập nhật lại Token (đề phòng token cũ hết hạn)
                    var responseString = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(responseString);
                    if (result.TryGetProperty("idToken", out var newToken))
                    {
                        Preferences.Set("AuthToken", newToken.GetString());
                    }

                    await Shell.Current.DisplayAlert("Thành công", "Đã cập nhật tên hiển thị!", "OK");

                    // Quay về trang trước
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Lỗi", "Không thể cập nhật. Vui lòng đăng nhập lại.", "OK");
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
            // Hủy thì quay về
            await Shell.Current.GoToAsync("..");
        }
    }
}