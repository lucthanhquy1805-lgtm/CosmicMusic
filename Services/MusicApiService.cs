using CosmicMusic.Models;

namespace CosmicMusic.Services
{
    public class MusicApiService
    {
        private readonly FirestoreService _firestoreService;
        private List<Song> _cachedSongs;

        // Constructor phải nhận FirestoreService
        public MusicApiService(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public async Task<List<Song>> GetSongsAsync()
        {
            // 1. Kiểm tra cache
            if (_cachedSongs != null && _cachedSongs.Count > 0) return _cachedSongs;

            // 2. Gọi Firestore
            try
            {
                var songs = await _firestoreService.GetAllSongsAsync();

                if (songs != null && songs.Count > 0)
                {
                    _cachedSongs = songs;
                    return _cachedSongs;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Lỗi MusicApiService: {ex.Message}");
            }

            // 3. ⚠️ QUAN TRỌNG: NẾU KHÔNG CÓ MẠNG HOẶC LỖI -> TRẢ VỀ 1 BÀI TEST
            // Để bạn biết là App vẫn sống, chỉ là chưa nối được Data
            return new List<Song>
            {
                new Song
                {
                    Title = "Đang kiểm tra kết nối...",
                    Artist = "Hệ thống",
                    AudioUrl = "",
                    CoverImage = "https://via.placeholder.com/150",
                    IsFeatured = true
                }
            };
        }
    }
}