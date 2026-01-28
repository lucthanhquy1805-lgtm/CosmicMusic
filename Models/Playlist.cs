namespace CosmicMusic.Models
{
    public class Playlist
    {
        // ID dạng chuỗi để khớp với Firebase/AWS
        public string Id { get; set; }

        public string Name { get; set; }

        // CỦA AI? (Quan trọng nhất để chia dữ liệu)
        public string OwnerId { get; set; }

        // Để hiển thị đẹp
        public string CoverImage { get; set; }
        public int SongCount { get; set; }

        // True = Playlist hệ thống (như Liked Songs)
        public bool IsSystem { get; set; }
    }
}