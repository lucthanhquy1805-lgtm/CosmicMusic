using CosmicMusic.Models;
using System.Text;
using System.Text.Json;
using System.Net;

namespace CosmicMusic.Services
{
    public class FirestoreService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public FirestoreService()
        {
            _httpClient = new HttpClient();
            string projectId = "cosmicmusic-50df6"; // ID dự án của bạn
            _baseUrl = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents";
        }

        // ==========================================================
        // 1. CÁC HÀM LIÊN QUAN ĐẾN USER
        // ==========================================================

        public async Task UpdateUserAsync(string uid, string email, string displayName, bool isPremium)
        {
            string url = $"{_baseUrl}/users/{uid}?updateMask.fieldPaths=email&updateMask.fieldPaths=displayName&updateMask.fieldPaths=isPremium";
            var payload = new
            {
                fields = new
                {
                    email = new { stringValue = email },
                    displayName = new { stringValue = displayName },
                    isPremium = new { booleanValue = isPremium }
                }
            };
            await _httpClient.PatchAsync(url, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        }

        public async Task<UserFirestoreInfo> GetUserInfoAsync(string uid)
        {
            string url = $"{_baseUrl}/users/{uid}";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("fields", out var fields))
                    {
                        var info = new UserFirestoreInfo();
                        if (fields.TryGetProperty("isPremium", out var premProp) && premProp.TryGetProperty("booleanValue", out var boolVal))
                            info.IsPremium = boolVal.GetBoolean();
                        if (fields.TryGetProperty("displayName", out var nameProp) && nameProp.TryGetProperty("stringValue", out var nameVal))
                            info.DisplayName = nameVal.GetString();
                        return info;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi đọc User: {ex.Message}"); }
            return null;
        }

        // ==========================================================
        // 2. CÁC HÀM LIÊN QUAN ĐẾN PLAYLIST
        // ==========================================================

        public async Task CreatePlaylistAndAddSong(string uid, string playlistName, Song song)
        {
            string safeName = playlistName.Replace(" ", "_");
            string playlistId = $"{safeName}_{DateTime.Now.Ticks}";
            string playlistUrl = $"{_baseUrl}/users/{uid}/playlists/{playlistId}";

            try
            {
                var newPlaylist = new
                {
                    fields = new
                    {
                        name = new { stringValue = playlistName },
                        ownerId = new { stringValue = uid },
                        coverImage = new { stringValue = song.CoverImage },
                        isSystem = new { booleanValue = false },
                        songCount = new { integerValue = 0 }
                    }
                };
                await _httpClient.PatchAsync(playlistUrl, new StringContent(JsonSerializer.Serialize(newPlaylist), Encoding.UTF8, "application/json"));
                await AddSongToExistingPlaylist(playlistId, song);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Lỗi tạo playlist: " + ex.Message); }
        }

        public async Task AddSongToExistingPlaylist(string playlistId, Song song)
        {
            string uid = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(uid)) return;

            string playlistUrl = $"{_baseUrl}/users/{uid}/playlists/{playlistId}";

            try
            {
                // 1. Thêm bài hát vào Sub-collection "songs"
                string safeTitle = song.Title.Replace(" ", "_");
                string safeArtist = song.Artist.Replace(" ", "_");
                string songDocId = WebUtility.UrlEncode($"{safeTitle}_{safeArtist}");

                string songUrl = $"{playlistUrl}/songs/{songDocId}";

                var songData = new
                {
                    fields = new
                    {
                        title = new { stringValue = song.Title },
                        artist = new { stringValue = song.Artist },
                        audioUrl = new { stringValue = song.AudioUrl },
                        coverImage = new { stringValue = song.CoverImage },
                        album = new { stringValue = song.Album ?? "Unknown" },
                        duration = new { integerValue = (int)song.Duration }
                    }
                };

                await _httpClient.PatchAsync(songUrl, new StringContent(JsonSerializer.Serialize(songData), Encoding.UTF8, "application/json"));

                // 2. Cập nhật tăng bộ đếm SongCount (+1) cho Playlist cha
                // Lấy thông tin playlist hiện tại
                var response = await _httpClient.GetAsync(playlistUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    int currentCount = 0;

                    if (doc.RootElement.TryGetProperty("fields", out var fields) &&
                        fields.TryGetProperty("songCount", out var sc) &&
                        sc.TryGetProperty("integerValue", out var val))
                    {
                        if (val.ValueKind == JsonValueKind.String)
                            int.TryParse(val.GetString(), out currentCount);
                        else
                            currentCount = val.GetInt32();
                    }

                    int newCount = currentCount + 1;

                    // Gửi lệnh cập nhật lại số lượng
                    var updatePayload = new
                    {
                        fields = new
                        {
                            songCount = new { integerValue = newCount }
                        }
                    };

                    string updateUrl = $"{playlistUrl}?updateMask.fieldPaths=songCount";
                    await _httpClient.PatchAsync(updateUrl, new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json"));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi thêm bài: {ex.Message}"); }
        }

        // ==========================================================
        // 3. KIỂM TRA TIM (IS FAVORITE)
        // ==========================================================
        public async Task<bool> IsSongInUserLibrary(string userId, Song song)
        {
            // 🛡️ BƯỚC 1: KIỂM TRA DỮ LIỆU ĐẦU VÀO (QUAN TRỌNG)
            // Nếu object song bị null -> Thoát ngay
            if (song == null) return false;

            // Nếu Tên hoặc Ca sĩ bị null (Lỗi gây crash App lúc nãy) -> Thoát ngay
            if (string.IsNullOrEmpty(song.Title) || string.IsNullOrEmpty(song.Artist))
            {
                // In ra Log để bạn biết bài nào đang bị lỗi dữ liệu
                System.Diagnostics.Debug.WriteLine($"⚠️ Cảnh báo: Bài hát '{song.Title}' bị thiếu thông tin Ca sĩ hoặc Tên. Không thể kiểm tra Tim.");
                return false;
            }

            try
            {
                var playlists = await GetUserPlaylists(userId);
                if (playlists == null || playlists.Count == 0) return false;

                // 🛡️ BƯỚC 2: XỬ LÝ AN TOÀN
                // Bây giờ Artist chắc chắn không null, lệnh Replace sẽ chạy ngon lành
                string safeTitle = song.Title.Replace(" ", "_");
                string safeArtist = song.Artist.Replace(" ", "_");
                string songDocId = WebUtility.UrlEncode($"{safeTitle}_{safeArtist}");

                foreach (var playlist in playlists)
                {
                    string checkUrl = $"{_baseUrl}/users/{userId}/playlists/{playlist.Id}/songs/{songDocId}";
                    var response = await _httpClient.GetAsync(checkUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi kiểm tra tim: {ex.Message}");
                return false;
            }
        }

        // ==========================================================
        // 4. LẤY DANH SÁCH PLAYLIST & BÀI HÁT
        // ==========================================================

        public async Task<List<Playlist>> GetUserPlaylists(string uid)
        {
            string url = $"{_baseUrl}/users/{uid}/playlists";
            var playlists = new List<Playlist>();

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);

                    if (doc.RootElement.TryGetProperty("documents", out var docs))
                    {
                        foreach (var d in docs.EnumerateArray())
                        {
                            var p = new Playlist();
                            p.Id = d.GetProperty("name").GetString().Split('/').Last();

                            var fields = d.GetProperty("fields");
                            if (fields.TryGetProperty("name", out var n)) p.Name = n.GetProperty("stringValue").GetString();
                            if (fields.TryGetProperty("coverImage", out var c)) p.CoverImage = c.GetProperty("stringValue").GetString();
                            if (fields.TryGetProperty("ownerId", out var o)) p.OwnerId = o.GetProperty("stringValue").GetString();

                            if (fields.TryGetProperty("songCount", out var sc))
                            {
                                if (sc.TryGetProperty("integerValue", out var val))
                                {
                                    if (val.ValueKind == JsonValueKind.String)
                                        p.SongCount = int.Parse(val.GetString());
                                    else
                                        p.SongCount = val.GetInt32();
                                }
                            }
                            playlists.Add(p);
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi lấy Playlist: {ex.Message}"); }
            return playlists;
        }

        // 👇👇👇 HÀM MỚI BẠN CẦN THÊM ĐÂY 👇👇👇
        public async Task<List<Song>> GetSongsFromPlaylist(string playlistId)
        {
            string uid = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(uid)) return new List<Song>();

            string url = $"{_baseUrl}/users/{uid}/playlists/{playlistId}/songs";
            var songs = new List<Song>();

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    // Kiểm tra xem playlist có bài hát nào không (có key "documents" không)
                    if (doc.RootElement.TryGetProperty("documents", out var docs))
                    {
                        foreach (var d in docs.EnumerateArray())
                        {
                            var s = new Song();
                            var fields = d.GetProperty("fields");

                            if (fields.TryGetProperty("title", out var t)) s.Title = t.GetProperty("stringValue").GetString();
                            if (fields.TryGetProperty("artist", out var a)) s.Artist = a.GetProperty("stringValue").GetString();
                            if (fields.TryGetProperty("audioUrl", out var u)) s.AudioUrl = u.GetProperty("stringValue").GetString();
                            if (fields.TryGetProperty("coverImage", out var c)) s.CoverImage = c.GetProperty("stringValue").GetString();
                            //lyrics
                            if (fields.TryGetProperty("lyrics", out var ly) && ly.TryGetProperty("stringValue", out var lystr))
                            {
                                s.Lyrics = lystr.GetString();
                            }

                            if (fields.TryGetProperty("duration", out var dur))
                            {
                                if (dur.TryGetProperty("integerValue", out var val))
                                    s.Duration = val.ValueKind == JsonValueKind.String ? double.Parse(val.GetString()) : val.GetDouble();
                            }

                            songs.Add(s);
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi lấy nhạc: {ex.Message}"); }
            return songs;


        }
        // 👇 HÀM XÓA NHẠC KHỎI PLAYLIST VÀ GIẢM BỘ ĐẾM 👇
        public async Task RemoveSongFromPlaylist(string playlistId, Song song)
        {
            string uid = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(uid)) return;

            string playlistUrl = $"{_baseUrl}/users/{uid}/playlists/{playlistId}";

            try
            {
                // 1. TÁI TẠO LẠI ID BÀI HÁT (Phải khớp quy tắc lúc thêm)
                string safeTitle = song.Title.Replace(" ", "_");
                string safeArtist = song.Artist.Replace(" ", "_");
                string songDocId = WebUtility.UrlEncode($"{safeTitle}_{safeArtist}");

                string songUrl = $"{playlistUrl}/songs/{songDocId}";

                // 2. GỬI LỆNH XÓA
                await _httpClient.DeleteAsync(songUrl);

                // 3. CẬP NHẬT GIẢM BỘ ĐẾM (SongCount - 1)
                var response = await _httpClient.GetAsync(playlistUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    int currentCount = 0;

                    if (doc.RootElement.TryGetProperty("fields", out var fields) &&
                        fields.TryGetProperty("songCount", out var sc) &&
                        sc.TryGetProperty("integerValue", out var val))
                    {
                        if (val.ValueKind == JsonValueKind.String)
                            int.TryParse(val.GetString(), out currentCount);
                        else
                            currentCount = val.GetInt32();
                    }

                    // Trừ đi 1 (nhưng không được nhỏ hơn 0)
                    int newCount = Math.Max(0, currentCount - 1);

                    var updatePayload = new
                    {
                        fields = new { songCount = new { integerValue = newCount } }
                    };

                    string updateUrl = $"{playlistUrl}?updateMask.fieldPaths=songCount";
                    await _httpClient.PatchAsync(updateUrl, new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json"));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi xóa bài: {ex.Message}"); }
        }
        // 👇 HÀM XÓA PLAYLIST (Mới)
        public async Task DeletePlaylist(string playlistId)
        {
            string uid = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(uid)) return;

            // Đường dẫn tới playlist cần xóa
            string url = $"{_baseUrl}/users/{uid}/playlists/{playlistId}";

            try
            {
                // Gửi lệnh DELETE
                await _httpClient.DeleteAsync(url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi xóa Playlist: {ex.Message}");
            }
        }
        //  HÀM LẤY NHẠC TỪ FIRESTORE (Bản Full + SearchKeywords)
        public async Task<List<Song>> GetAllSongsAsync()
        {
            var songs = new List<Song>();
            string url = $"{_baseUrl}/songs"; // Trỏ vào collection 'songs' gốc

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("documents", out var docs))
                    {
                        foreach (var d in docs.EnumerateArray())
                        {
                            if (d.TryGetProperty("fields", out var fields))
                            {
                                var s = new Song();

                                // 1. Lấy thông tin cơ bản
                                if (fields.TryGetProperty("title", out var t)) s.Title = t.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("artist", out var a)) s.Artist = a.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("album", out var alb)) s.Album = alb.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("audioUrl", out var u)) s.AudioUrl = u.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("coverImage", out var c)) s.CoverImage = c.GetProperty("stringValue").GetString();
                                // Lấy lyrics
                                if (fields.TryGetProperty("lyrics", out var ly) && ly.TryGetProperty("stringValue", out var lystr))
                                {
                                    s.Lyrics = lystr.GetString();
                                }

                                // 2. Lấy thông tin Boolean
                                if (fields.TryGetProperty("isPremium", out var p) && p.TryGetProperty("booleanValue", out var bv))
                                    s.IsPremium = bv.GetBoolean();

                                if (fields.TryGetProperty("isFeatured", out var f) && f.TryGetProperty("booleanValue", out var fv))
                                    s.IsFeatured = fv.GetBoolean();

                                // 👇 3. LẤY SEARCH KEYWORDS (MẢNG) - MỚI BỔ SUNG
                                s.SearchKeywords = new List<string>();
                                if (fields.TryGetProperty("searchKeywords", out var sk) &&
                                    sk.TryGetProperty("arrayValue", out var av) &&
                                    av.TryGetProperty("values", out var vals))
                                {
                                    foreach (var v in vals.EnumerateArray())
                                    {
                                        if (v.TryGetProperty("stringValue", out var val))
                                        {
                                            s.SearchKeywords.Add(val.GetString());
                                        }
                                    }
                                }

                                // Chỉ lấy bài hợp lệ
                                if (!string.IsNullOrEmpty(s.AudioUrl) && !string.IsNullOrEmpty(s.Title))
                                {
                                    songs.Add(s);
                                }
                                //  4. LẤY LIKE COUNT (INTEGER) 
                                if (fields.TryGetProperty("likeCount", out var lc) && lc.TryGetProperty("integerValue", out var lv))
                                {
                                    // Firestore trả về số nguyên có thể là String hoặc Int tùy context
                                    if (lv.ValueKind == JsonValueKind.String)
                                        s.LikeCount = int.Parse(lv.GetString());
                                    else
                                        s.LikeCount = lv.GetInt32();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi lấy nhạc Firestore: {ex.Message}");
            }

            return songs;
        }
        // ==========================================================
        // 5. CÁC HÀM TÌM KIẾM (SEARCH) - MỚI BỔ SUNG
        // ==========================================================

        public async Task<List<Song>> SearchSongsByKeywordsAsync(string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return new List<Song>();

            // 1. Chuẩn hóa từ khóa tìm kiếm
            // Chuyển về chữ thường + Xóa khoảng trắng thừa
            string cleanKeyword = keyword.ToLower().Trim();

            // 2. Tạo Payload truy vấn Firestore
            // Logic: Tìm trong collection "songs", điều kiện: mảng "searchKeywords" CHỨA "cleanKeyword"
            var query = new
            {
                structuredQuery = new
                {
                    from = new[] { new { collectionId = "songs" } },
                    where = new
                    {
                        fieldFilter = new
                        {
                            field = new { fieldPath = "searchKeywords" },
                            op = "ARRAY_CONTAINS", // 👈 Quan trọng: Tìm trong mảng
                            value = new { stringValue = cleanKeyword }
                        }
                    },
                    limit = 20 // Giới hạn 20 kết quả để tối ưu tốc độ
                }
            };

            // 3. Gửi Request POST (Lưu ý: Truy vấn phức tạp phải dùng POST với URL :runQuery)
            string runQueryUrl = $"{_baseUrl}:runQuery";

            var songs = new List<Song>();

            try
            {
                var content = new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(runQueryUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();

                    // Firestore trả về một mảng JSON các documents
                    // Cấu trúc: [ { "document": { ... } }, { "document": { ... } } ]
                    // Lưu ý: Nếu không tìm thấy, nó có thể trả về mảng rỗng hoặc format khác một chút

                    using var doc = JsonDocument.Parse(json);

                    // Kiểm tra xem kết quả trả về có phải là mảng không
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            // Nếu có thuộc tính "document" thì mới là kết quả tìm thấy
                            if (item.TryGetProperty("document", out var d))
                            {
                                if (d.TryGetProperty("fields", out var fields))
                                {
                                    var s = new Song();

                                    // Map dữ liệu (Giống hệt hàm GetAllSongsAsync)
                                    if (fields.TryGetProperty("title", out var t)) s.Title = t.GetProperty("stringValue").GetString();
                                    if (fields.TryGetProperty("artist", out var a)) s.Artist = a.GetProperty("stringValue").GetString();
                                    if (fields.TryGetProperty("album", out var alb)) s.Album = alb.GetProperty("stringValue").GetString();
                                    if (fields.TryGetProperty("audioUrl", out var u)) s.AudioUrl = u.GetProperty("stringValue").GetString();
                                    if (fields.TryGetProperty("coverImage", out var c)) s.CoverImage = c.GetProperty("stringValue").GetString();

                                    if (fields.TryGetProperty("isPremium", out var p) && p.TryGetProperty("booleanValue", out var bv)) s.IsPremium = bv.GetBoolean();

                                    if (fields.TryGetProperty("lyrics", out var ly) && ly.TryGetProperty("stringValue", out var lystr))
                                    {
                                        s.Lyrics = lystr.GetString();
                                    }
                                    // Chỉ lấy bài hợp lệ
                                    if (!string.IsNullOrEmpty(s.AudioUrl) && !string.IsNullOrEmpty(s.Title))
                                    {
                                        songs.Add(s);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tìm kiếm Firestore: {ex.Message}");
            }

            return songs;
        }
        // 👇 HÀM ĐÃ SỬA: Dùng _httpClient thay vì _db (REST API)

        /// <summary>
        /// Thêm bài hát vào danh sách Yêu thích (REST API)
        /// </summary>
        public async Task AddToFavoritesAsync(Song song)
        {
            try
            {
                string userId = Preferences.Get("UserId", "");
                if (string.IsNullOrEmpty(userId)) return;

                // 1. Tạo ID an toàn từ tên bài hát (để làm Document ID)
                string safeTitle = song.Title.Replace(" ", "_");
                string safeArtist = song.Artist.Replace(" ", "_");
                string docId = WebUtility.UrlEncode($"{safeTitle}_{safeArtist}");

                // 2. URL tới document trong sub-collection "Favorites"
                string url = $"{_baseUrl}/users/{userId}/Favorites/{docId}";

                // 3. Tạo Payload JSON theo chuẩn Firestore REST
                var payload = new
                {
                    fields = new
                    {
                        title = new { stringValue = song.Title },
                        artist = new { stringValue = song.Artist },
                        audioUrl = new { stringValue = song.AudioUrl },
                        coverImage = new { stringValue = song.CoverImage },
                        album = new { stringValue = song.Album ?? "" },
                        duration = new { integerValue = (int)song.Duration },
                        isPremium = new { booleanValue = song.IsPremium }
                    }
                };

                // 4. Gửi lệnh PATCH (Tạo hoặc Ghi đè)
                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PatchAsync(url, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"❌ Lỗi API Thêm Yêu Thích: {error}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Exception Thêm Yêu Thích: {ex.Message}");
            }
        }

        /// <summary>
        /// Xóa bài hát khỏi danh sách Yêu thích (REST API)
        /// </summary>
        public async Task RemoveFromFavoritesAsync(Song song)
        {
            try
            {
                string userId = Preferences.Get("UserId", "");
                if (string.IsNullOrEmpty(userId)) return;

                // 1. Tái tạo ID Document giống lúc thêm
                string safeTitle = song.Title.Replace(" ", "_");
                string safeArtist = song.Artist.Replace(" ", "_");
                string docId = WebUtility.UrlEncode($"{safeTitle}_{safeArtist}");

                // 2. URL tới document cần xóa
                string url = $"{_baseUrl}/users/{userId}/Favorites/{docId}";

                // 3. Gửi lệnh DELETE
                await _httpClient.DeleteAsync(url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Exception Xóa Yêu Thích: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy toàn bộ danh sách bài hát yêu thích của User (REST API)
        /// </summary>
        public async Task<List<Song>> GetFavoritesAsync()
        {
            string userId = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(userId)) return new List<Song>();

            // Đường dẫn đến collection Favorites
            string url = $"{_baseUrl}/users/{userId}/Favorites";
            var songs = new List<Song>();

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    // Firestore trả về mảng "documents"
                    if (doc.RootElement.TryGetProperty("documents", out var docs))
                    {
                        foreach (var d in docs.EnumerateArray())
                        {
                            if (d.TryGetProperty("fields", out var fields))
                            {
                                var s = new Song();
                                // Map dữ liệu từ JSON Firestore sang Object Song
                                if (fields.TryGetProperty("title", out var t)) s.Title = t.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("artist", out var a)) s.Artist = a.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("audioUrl", out var u)) s.AudioUrl = u.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("coverImage", out var c)) s.CoverImage = c.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("isPremium", out var p) && p.TryGetProperty("booleanValue", out var bv)) s.IsPremium = bv.GetBoolean();

                                if (fields.TryGetProperty("lyrics", out var ly) && ly.TryGetProperty("stringValue", out var lystr))
                                {
                                    s.Lyrics = lystr.GetString();
                                }
                                // Bổ sung SearchKeywords nếu cần (tùy chọn)

                                songs.Add(s);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi lấy danh sách yêu thích: {ex.Message}");
            }

            return songs;
        }
        // 👇 THÊM HÀM NÀY VÀO Services/FirestoreService.cs

        /// <summary>
        /// Kiểm tra xem bài hát đã có trong danh sách Yêu thích chưa
        /// </summary>
        public async Task<bool> IsSongInFavoritesAsync(Song song)
        {
            try
            {
                string userId = Preferences.Get("UserId", "");
                if (string.IsNullOrEmpty(userId)) return false;

                // Tái tạo ID Document giống quy tắc lúc thêm
                string safeTitle = song.Title.Replace(" ", "_");
                string safeArtist = song.Artist.Replace(" ", "_");
                string docId = WebUtility.UrlEncode($"{safeTitle}_{safeArtist}");

                string url = $"{_baseUrl}/users/{userId}/Favorites/{docId}";

                // Gửi lệnh GET để kiểm tra
                var response = await _httpClient.GetAsync(url);

                // Nếu Server trả về 200 OK -> Có tồn tại
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        // 👇👇👇 HÀM MỚI: CẬP NHẬT SỐ LƯỢT THÍCH TOÀN CỤC 👇👇👇
        // Trong FirestoreService.cs
        public async Task UpdateGlobalLikeCount(Song song, int change)
        {
            try
            {
                // 1. Tìm ID bài hát trên Firebase (Vì Song Model chưa lưu ID)
                // Chúng ta dùng query search để tìm document ID
                string runQueryUrl = $"{_baseUrl}:runQuery";

                var query = new
                {
                    structuredQuery = new
                    {
                        from = new[] { new { collectionId = "songs" } },
                        where = new
                        {
                            compositeFilter = new
                            {
                                op = "AND",
                                filters = new object[]
                                {
                                    new { fieldFilter = new { field = new { fieldPath = "title" }, op = "EQUAL", value = new { stringValue = song.Title } } },
                                    new { fieldFilter = new { field = new { fieldPath = "artist" }, op = "EQUAL", value = new { stringValue = song.Artist } } }
                                }
                            }
                        },
                        limit = 1
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(runQueryUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            // Lấy được Document Name
                            if (item.TryGetProperty("document", out var d) && d.TryGetProperty("name", out var nameProp))
                            {
                                // Lấy giá trị likeCount hiện tại (để cộng dồn cho chính xác)
                                int currentLike = 0;
                                if (d.TryGetProperty("fields", out var f) && f.TryGetProperty("likeCount", out var lc))
                                {
                                    if (lc.TryGetProperty("integerValue", out var val))
                                        currentLike = val.ValueKind == JsonValueKind.String ? int.Parse(val.GetString()) : val.GetInt32();
                                }

                                // Tính số like mới (đảm bảo không âm)
                                int newLike = Math.Max(0, currentLike + change);

                                // Lấy ID Document
                                string fullPath = nameProp.GetString();
                                string docId = fullPath.Split('/').Last();

                                // Gửi lệnh Update
                                string updateUrl = $"{_baseUrl}/songs/{docId}?updateMask.fieldPaths=likeCount";
                                var updatePayload = new
                                {
                                    fields = new
                                    {
                                        likeCount = new { integerValue = newLike }
                                    }
                                };

                                await _httpClient.PatchAsync(updateUrl, new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json"));
                                System.Diagnostics.Debug.WriteLine($"✅ Đã cập nhật Like lên Server: {newLike}");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Lỗi update like: {ex.Message}");
            }
        }
        // 👇👇👇 HÀM MỚI: CẬP NHẬT LYRIC LÊN FIREBASE 👇👇👇
        public async Task UpdateSongLyricsAsync(Song song)
        {
            if (song == null || string.IsNullOrEmpty(song.Lyrics)) return;

            try
            {
                // 1. Tìm ID của bài hát trên Firebase (Dựa vào Tên và Ca sĩ)
                // Chúng ta dùng query search tương tự hàm Search nhưng tìm chính xác
                string runQueryUrl = $"{_baseUrl}:runQuery";

                var query = new
                {
                    structuredQuery = new
                    {
                        from = new[] { new { collectionId = "songs" } },
                        where = new
                        {
                            compositeFilter = new
                            {
                                op = "AND",
                                filters = new object[]
                                {
                                    new { fieldFilter = new { field = new { fieldPath = "title" }, op = "EQUAL", value = new { stringValue = song.Title } } },
                                    new { fieldFilter = new { field = new { fieldPath = "artist" }, op = "EQUAL", value = new { stringValue = song.Artist } } }
                                }
                            }
                        },
                        limit = 1
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(query), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(runQueryUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            // 2. Lấy được Document Name (chứa ID)
                            if (item.TryGetProperty("document", out var d) && d.TryGetProperty("name", out var nameProp))
                            {
                                string fullPath = nameProp.GetString(); // Dạng: projects/.../documents/songs/ID_BAI_HAT

                                // Tách lấy ID (hoặc dùng fullPath để update luôn cũng được nhưng base url của mình hơi khác)
                                // Cách an toàn nhất với REST API là dùng DocumentMask để chỉ update 1 trường

                                // URL để update trực tiếp document này
                                // Lưu ý: _baseUrl của chúng ta là .../documents. fullPath cũng bắt đầu từ documents.
                                // Chúng ta cần xử lý chuỗi URL một chút cho khớp.

                                // Cách đơn giản: Lấy phần cuối cùng làm ID
                                string docId = fullPath.Split('/').Last();
                                string updateUrl = $"{_baseUrl}/songs/{docId}?updateMask.fieldPaths=lyrics";

                                var updatePayload = new
                                {
                                    fields = new
                                    {
                                        lyrics = new { stringValue = song.Lyrics }
                                    }
                                };

                                await _httpClient.PatchAsync(updateUrl, new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json"));
                                System.Diagnostics.Debug.WriteLine($"✅ Đã lưu Lyric lên Firebase cho bài: {song.Title}");
                                return; // Xong việc thì thoát
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Lỗi lưu Lyric: {ex.Message}");
            }
        }
    }

    public class UserFirestoreInfo
    {
        public string DisplayName { get; set; }
        public bool IsPremium { get; set; }
    }
}