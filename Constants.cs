using System;
using System.Collections.Generic;
using System.Text;

namespace CosmicMusic
{
    public static class Constants
    {
        // ============================================================
        // 1. CẤU HÌNH FIREBASE (MỚI THÊM)
        // ============================================================

        // 👇 HÃY DÁN API KEY BẠN VỪA COPY TỪ FIREBASE CONSOLE VÀO ĐÂY
        public const string FirebaseApiKey = "AIzaSyDPUF_xVA9cF7T91vH-Q0J8RxDDE5sqK0M";
        public const string ProjectId = "cosmicmusic-50df6";
        // ============================================================
        // 2. CẤU HÌNH SQLITE (GIỮ NGUYÊN CŨ)
        // ============================================================
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