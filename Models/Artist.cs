using System;

namespace CosmicMusic.Models
{
    public class Artist
    {
        public string Id { get; set; }          // ID của nghệ sĩ (ví dụ: "artist_lowg")
        public string Name { get; set; }        // Tên (ví dụ: "Low G")
        public string Avatar { get; set; }      // Link ảnh đại diện
        public string Bio { get; set; }         // Tiểu sử ngắn
        public int Followers { get; set; }      // Số người theo dõi
    }
}