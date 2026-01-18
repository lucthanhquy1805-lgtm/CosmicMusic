using System;
using System.Collections.Generic;
using System.Text;

using SQLite;

namespace CosmicMusic.Models
{
    public class Song
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Title { get; set; }       // Tên bài hát
        public string Artist { get; set; }      // Ca sĩ
        public string Album { get; set; }       // Album
        public string CoverImage { get; set; }  // Link ảnh bìa (Online hoặc Local)

        public string AudioUrl { get; set; }    // Link nhạc Online (https://...)
        public string LocalPath { get; set; }   // Đường dẫn file sau khi tải về

        public double Duration { get; set; }    // Tổng thời gian (giây)
        public bool IsFavorite { get; set; }    // Đánh dấu yêu thích

        // --- Các thuộc tính hỗ trợ (Không lưu vào Database) ---

        [Ignore]
        public bool IsDownloaded => !string.IsNullOrEmpty(LocalPath) && File.Exists(LocalPath);
    }
}