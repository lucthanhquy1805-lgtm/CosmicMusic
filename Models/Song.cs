using System;
using System.Collections.Generic;
using System.IO;
using SQLite; // Giữ nguyên SQLite cho tính năng tải nhạc

namespace CosmicMusic.Models
{
    public class Song
    {
        // 👇 ĐÃ SỬA: Đổi sang chuỗi (string) để khớp với Document ID của Firebase.
        // Vẫn giữ [PrimaryKey] để SQLite lưu offline được.
        [PrimaryKey]
        public string Id { get; set; }

        // --- 1. THÔNG TIN CƠ BẢN (Giữ nguyên để Giao diện không bị lỗi) ---
        public string Title { get; set; }       // Tên bài hát
        public string Artist { get; set; }      // Tên Ca sĩ (Dùng để hiển thị UI)
        public string Album { get; set; }       // Tên Album (Dùng để hiển thị UI)
        public string CoverImage { get; set; }  // Link ảnh bìa
        public string AudioUrl { get; set; }    // Link nhạc AWS S3
        public string LocalPath { get; set; }   // Đường dẫn file tải về (nếu có)
        public double Duration { get; set; }    // Thời lượng (giây)
        public string Lyrics { get; set; } = "";// Lời bài hát

        // --- 2. CÁC TRƯỜNG MỚI ĐỂ LIÊN KẾT 10 COLLECTION (CHUẨN FIREBASE) ---
        public string ArtistId { get; set; }    // Khóa ngoại nối đến bảng 'artists'
        public string AlbumId { get; set; }     // Khóa ngoại nối đến bảng 'albums'
        public string GenreId { get; set; }     // Khóa ngoại nối đến bảng 'genres'
        public string GenreName { get; set; }   // Tên thể loại (Ví dụ: Rap, Pop)

        // --- 3. PHÂN LOẠI VÀ TƯƠNG TÁC ---
        public bool IsPremium { get; set; }     // Bài VIP 
        public bool IsFeatured { get; set; }    // Bài nổi bật hiện Home
        public bool IsFavorite { get; set; }    // Trạng thái yêu thích (Local)

        public int LikeCount { get; set; } = 0; // Lượt thả tim
        public int PlayCount { get; set; } = 0; // Số lượt nghe
        public DateTime CreatedAt { get; set; } // Ngày tạo trên Firebase

        // --- 4. THUỘC TÍNH BỎ QUA KHÔNG LƯU VÀO SQLITE ---
        [Ignore]
        public List<string> SearchKeywords { get; set; } = new List<string>();

        [Ignore]
        public bool IsDownloaded => !string.IsNullOrEmpty(LocalPath) && File.Exists(LocalPath);
    }
}