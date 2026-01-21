namespace CosmicMusic.Models
{
    public class User
    {
        // ID định danh duy nhất từ Firebase (User ID)
        public string Uid { get; set; }

        public string Email { get; set; }

        public string DisplayName { get; set; }

        // 👇 ĐÂY LÀ CHÌA KHÓA CỦA TÍNH NĂNG NÂNG CAO
        // false = Bản thường (nghe 100 bài)
        // true = Bản nâng cao (nghe 500 bài)
        public bool IsPremium { get; set; } = false;

        // Link ảnh đại diện (nếu có)
        public string PhotoUrl { get; set; }
    }
}