using System;
using System.Collections.Generic;
using System.Text;

namespace CosmicMusic
{
    public static class Constants
    {
        public const string DatabaseFilename = "CosmicMusic.db3";

        public const SQLite.SQLiteOpenFlags Flags =
            // Mở file để đọc và ghi
            SQLite.SQLiteOpenFlags.ReadWrite |
            // Tự động tạo file nếu chưa có
            SQLite.SQLiteOpenFlags.Create |
            // Cho phép nhiều luồng truy cập (Tăng tốc độ)
            SQLite.SQLiteOpenFlags.SharedCache;

        // Đường dẫn lưu file trên điện thoại
        public static string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
    }
}
