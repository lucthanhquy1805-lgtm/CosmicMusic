using System;
using System.Collections.Generic;
using System.Text;
using SQLite; // Giữ nguyên SQLite cho tính năng tải nhạc
using System.IO; // 👇 Thêm cái này để dùng File.Exists

namespace CosmicMusic.Models
{
    public class Song
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Title { get; set; }       // Tên bài hát
        public string Artist { get; set; }      // Ca sĩ
        public string Album { get; set; }       // Album
        public string CoverImage { get; set; }  // Link ảnh bìa

        public string AudioUrl { get; set; }    // Link nhạc AWS S3
        public string LocalPath { get; set; }   // Đường dẫn file tải về (nếu có)

        public double Duration { get; set; }    // Thời lượng (giây)
        public bool IsFavorite { get; set; }    // Yêu thích

        // --- CÁC TRƯỜNG QUAN TRỌNG CHO FIREBASE ---

        public bool IsPremium { get; set; }     // Bài VIP (FirestoreService cần cái này)

        // 👇 BẮT BUỘC THÊM DÒNG NÀY (Nếu không FirestoreService sẽ lỗi đỏ)
        public bool IsFeatured { get; set; }    // Bài nổi bật hiện Home

        [Ignore] // SQLite không lưu List, nên cần đánh dấu Ignore hoặc dùng Converter
        public List<string> SearchKeywords { get; set; } = new List<string>();

        // --- Thuộc tính tính toán ---
        [Ignore]
        public bool IsDownloaded => !string.IsNullOrEmpty(LocalPath) && File.Exists(LocalPath);
        public int LikeCount { get; set; } = 0;
    }
}