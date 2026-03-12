namespace CosmicMusic.Models
{
    public class Subscription
    {
        public string Id { get; set; }
        public string Name { get; set; }          // Tên gói (vd: "Premium 1 Tháng")
        public string Price { get; set; }         // Giá tiền (vd: "59.000đ")
        public int DurationInMonths { get; set; } // Thời hạn (vd: 1)
        public string Description { get; set; }   // Mô tả ngắn
        public string BackgroundColor { get; set; } // Màu nền cho thẻ (vd: "#D946EF")
        public List<string> Features { get; set; } = new(); // Danh sách các quyền lợi
    }
}