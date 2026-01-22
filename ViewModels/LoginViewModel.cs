using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Services; // 👇 Nhớ namespace này
using CosmicMusic.Views;
using System.Text;
using System.Text.Json;

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
        private readonly FirestoreService _firestoreService; // 👇 1. Khai báo dịch vụ Firestore

        // 👇 2. Tiêm FirestoreService vào Constructor
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
                await Shell.Current.DisplayAlert("Lỗi", "Vui lòng nhập Email và Mật khẩu", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                // A. ĐĂNG NHẬP (FIREBASE AUTH)
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

                    // ==========================================================
                    // 👇 B. ĐỒNG BỘ DỮ LIỆU TỪ FIRESTORE (ĐÁM MÂY) 👇
                    // ==========================================================

                    // 1. Hỏi Firestore: "Ông này có thông tin gì trên mây chưa?"
                    var firestoreUser = await _firestoreService.GetUserInfoAsync(uid);

                    if (firestoreUser != null)
                    {
                        // TRƯỜNG HỢP 1: ĐÃ CÓ DỮ LIỆU TRÊN MÂY
                        // -> Lấy thông tin VIP và Tên từ trên mây về máy
                        isPremium = firestoreUser.IsPremium;

                        // Nếu trên mây có tên đẹp hơn (đã đổi) thì lấy tên đó
                        if (!string.IsNullOrEmpty(firestoreUser.DisplayName))
                        {
                            displayName = firestoreUser.DisplayName;
                        }
                    }
                    else
                    {
                        // TRƯỜNG HỢP 2: NGƯỜI DÙNG MỚI (Hoặc chưa có data trên mây)
                        // -> Kiểm tra xem có phải Admin không (logic cũ)
                        if (email.ToLower().Contains("admin")) isPremium = true;

                        // -> LƯU NGAY LÊN MÂY để lần sau đăng nhập còn nhớ
                        await _firestoreService.UpdateUserAsync(uid, email, displayName, isPremium);
                    }

                    // ==========================================================
                    // 👇 C. LƯU VÀO MÁY (LOCAL PREFERENCES) ĐỂ DÙNG 👇
                    // ==========================================================

                    Preferences.Set("AuthToken", idToken);
                    Preferences.Set("UserEmail", email);
                    Preferences.Set("UserName", displayName);
                    Preferences.Set("IsPremium", isPremium);

                    Preferences.Set("UserId", uid);

                    // Xóa các key "VIP_..." cũ kỹ đi vì giờ ta đã có Firestore xịn rồi
                    // (Hoặc giữ lại làm kỷ niệm cũng được, không ảnh hưởng)

                    // Vào App
                    await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Đăng nhập thất bại", "Email hoặc mật khẩu không đúng!", "Thử lại");
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

    public class FirebaseAuthResult
    {
        public string localId { get; set; }
        public string email { get; set; }
        public string displayName { get; set; }
        public string idToken { get; set; }
    }
}