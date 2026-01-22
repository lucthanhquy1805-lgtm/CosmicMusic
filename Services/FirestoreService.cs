using System.Text;
using System.Text.Json;
using CosmicMusic.Models; // Đảm bảo có namespace này nếu cần dùng Model

namespace CosmicMusic.Services
{
    public class FirestoreService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public FirestoreService()
        {
            _httpClient = new HttpClient();
            // Đường dẫn gốc đến Database của bạn
            _baseUrl = $"https://firestore.googleapis.com/v1/projects/{Constants.ProjectId}/databases/(default)/documents";
        }

        // 1. HÀM LƯU (HOẶC CẬP NHẬT) THÔNG TIN USER
        // Dùng khi: Đăng ký, Đăng nhập (để update lần cuối), hoặc Mua VIP
        public async Task UpdateUserAsync(string uid, string email, string displayName, bool isPremium)
        {
            // URL trỏ đến document của user này
            // updateMask: Chỉ định những trường nào cần cập nhật (để không xóa mất dữ liệu khác nếu có)
            string url = $"{_baseUrl}/users/{uid}?updateMask.fieldPaths=email&updateMask.fieldPaths=displayName&updateMask.fieldPaths=isPremium";

            // Cấu trúc JSON "đặc biệt" của Firestore
            var payload = new
            {
                fields = new
                {
                    email = new { stringValue = email },
                    displayName = new { stringValue = displayName },
                    isPremium = new { booleanValue = isPremium }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Dùng PATCH để cập nhật (Nếu chưa có nó sẽ tự tạo, nếu có rồi nó chỉ sửa các dòng updateMask)
            await _httpClient.PatchAsync(url, content);
        }

        // 2. HÀM ĐỌC THÔNG TIN USER
        // Dùng khi: Đăng nhập (để xem ông này có phải VIP không)
        public async Task<UserFirestoreInfo> GetUserInfoAsync(string uid)
        {
            string url = $"{_baseUrl}/users/{uid}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    // Firestore trả về dạng: { "fields": { "isPremium": { "booleanValue": true }, ... } }
                    if (root.TryGetProperty("fields", out var fields))
                    {
                        var info = new UserFirestoreInfo();

                        // Lấy VIP
                        if (fields.TryGetProperty("isPremium", out var premProp) &&
                            premProp.TryGetProperty("booleanValue", out var boolVal))
                        {
                            info.IsPremium = boolVal.GetBoolean();
                        }

                        // Lấy Tên
                        if (fields.TryGetProperty("displayName", out var nameProp) &&
                            nameProp.TryGetProperty("stringValue", out var nameVal))
                        {
                            info.DisplayName = nameVal.GetString();
                        }

                        return info;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi đọc Firestore: {ex.Message}");
            }

            return null; // Không tìm thấy hoặc lỗi
        }
    }

    // Class phụ để hứng dữ liệu trả về cho gọn
    public class UserFirestoreInfo
    {
        public string DisplayName { get; set; }
        public bool IsPremium { get; set; }
    }
}