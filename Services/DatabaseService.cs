using System;
using System.Collections.Generic;
using System.Text;

using SQLite;
using CosmicMusic.Models;

namespace CosmicMusic.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;

        // Hàm khởi tạo Database (Tạo bảng nếu chưa có)
        async Task Init()
        {
            if (_database is not null)
                return;

            _database = new SQLiteAsyncConnection(Constants.DatabasePath, Constants.Flags);

            // Tạo 2 bảng
            await _database.CreateTableAsync<Song>();
            await _database.CreateTableAsync<Playlist>();
        }

        // --- CÁC HÀM XỬ LÝ BÀI HÁT ---

        // Lấy tất cả bài hát
        public async Task<List<Song>> GetSongsAsync()
        {
            await Init();
            return await _database.Table<Song>().ToListAsync();
        }

        // Lấy danh sách yêu thích
        public async Task<List<Song>> GetFavoriteSongsAsync()
        {
            await Init();
            return await _database.Table<Song>().Where(s => s.IsFavorite).ToListAsync();
        }

        // Lưu bài hát (Thêm mới hoặc Cập nhật)
        public async Task<int> SaveSongAsync(Song song)
        {
            await Init();
            if (song.Id != 0)
                return await _database.UpdateAsync(song);
            else
                return await _database.InsertAsync(song);
        }

        // Xóa bài hát
        public async Task<int> DeleteSongAsync(Song song)
        {
            await Init();
            return await _database.DeleteAsync(song);
        }
    }
}