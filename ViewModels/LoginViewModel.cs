using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls;

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
        private readonly FirestoreService _firestoreService;

        public LoginViewModel(FirestoreService firestoreService)
        {
            _httpClient = new HttpClient();
            _firestoreService = firestoreService;
        }


        [RelayCommand]
        public async Task Login()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Shell.Current.DisplayAlert("Lỗi", "Vui lòng nhập PILOT ID và MÃ TRUY CẬP", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                // A. ĐĂNG NHẬP (FIREBASE AUTH REST API)
                string apiKey = Constants.FirebaseApiKey;
                string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={apiKey}";

                var payload = new { email = Email, password = Password, returnSecureToken = true };
                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                string responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // === ĐĂNG NHẬP THÀNH CÔNG ===
                    var result = JsonSerializer.Deserialize<FirebaseAuthResult>(responseString);

                    string uid = result.localId;
                    string email = result.email;
                    string displayName = result.displayName ?? "Cosmic Traveler";
                    string idToken = result.idToken;
                    bool isPremium = false;
                    bool isAdmin = false; // [THÊM 1] Khai báo biến isAdmin

                    // 👇 BỔ SUNG: Khai báo biến chứa link ảnh (Mặc định là rỗng)
                    string photoUrl = "";

                    // ==========================================================
                    // B. ĐỒNG BỘ DỮ LIỆU TỪ FIRESTORE (ĐÁM MÂY)
                    // ==========================================================
                    var firestoreUser = await _firestoreService.GetUserInfoAsync(uid);

                    if (firestoreUser != null)
                    {
                        isPremium = firestoreUser.IsPremium;
                        isAdmin = firestoreUser.IsAdmin; 

                        if (!string.IsNullOrEmpty(firestoreUser.DisplayName))
                        {
                            displayName = firestoreUser.DisplayName;
                        }

                        // 👇 BỔ SUNG CỰC QUAN TRỌNG: Kéo link ảnh từ Firestore về
                        // ⚠️ LƯU Ý: Tùy vào Model User của bạn đặt tên biến là gì. 
                        // Nếu nó báo lỗi đỏ ở chữ "PhotoUrl", bạn thử đổi thành "Avatar" hoặc "ProfileImage" cho khớp code của bạn nhé!
                        if (!string.IsNullOrEmpty(firestoreUser.PhotoUrl))
                        {
                            photoUrl = firestoreUser.PhotoUrl;
                        }
                    }
                    else
                    {
                        if (email.ToLower().Contains("admin")) isPremium = true;
                        // Cập nhật lên mây (Tạm thời truyền rỗng hoặc bỏ qua trường ảnh lúc tạo mới)
                        await _firestoreService.UpdateUserAsync(uid, email, displayName, isPremium);
                    }

                    // ==========================================================
                    // C. LƯU VÀO MÁY (LOCAL PREFERENCES) ĐỂ DÙNG
                    // ==========================================================
                    Preferences.Set("AuthToken", idToken);
                    Preferences.Set("UserEmail", email);
                    Preferences.Set("UserName", displayName);
                    Preferences.Set("IsPremium", isPremium);
                    Preferences.Set("UserId", uid);

                    // 👇 BỔ SUNG: Lưu link ảnh vừa lấy được xuống bộ nhớ máy để hiển thị ra UI
                    Preferences.Set("UserPhotoUrl", photoUrl);
                    Preferences.Set("IsAdmin", isAdmin); // [THÊM 3] Lưu quyền Admin vào Preferences

                    // Vào App
                    await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Truy cập từ chối", "Pilot ID hoặc mã truy cập không hợp lệ!", "Thử lại");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Mất kết nối", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ==========================================================
        // 👇 TÍNH NĂNG MỚI: QUÊN MẬT KHẨU (GỬI EMAIL RESET) 👇
        // ==========================================================
        [RelayCommand]
        public async Task ForgotPassword()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                await Shell.Current.DisplayAlert("Cảnh báo", "Vui lòng nhập PILOT ID (Email) của bạn vào ô trống để hệ thống gửi mã khôi phục!", "Đã hiểu");
                return;
            }

            IsBusy = true;
            try
            {
                string apiKey = Constants.FirebaseApiKey;
                // API của Firebase dùng để gửi link Reset Password
                string url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={apiKey}";

                var payload = new
                {
                    requestType = "PASSWORD_RESET",
                    email = Email
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    await Shell.Current.DisplayAlert("Truyền tin thành công", "Một liên kết thiết lập lại mã truy cập đã được gửi đến hộp thư (Email) của bạn. Vui lòng kiểm tra!", "Rõ");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Lỗi", "Không tìm thấy Pilot ID này trong trạm không gian. Hãy kiểm tra lại lỗi chính tả.", "Thử lại");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Mất kết nối", ex.Message, "OK");
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

    // Class này giữ nguyên, không xóa vì nó là bản gốc để nhận data
    public class FirebaseAuthResult
    {
        public string localId { get; set; }
        public string email { get; set; }
        public string displayName { get; set; }
        public string idToken { get; set; }
    }
}