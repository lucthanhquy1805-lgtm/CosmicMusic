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
        // 2. CÁC HÀM LẤY DỮ LIỆU NỀN TẢNG (10 COLLECTIONS) - MỚI
        // ==========================================================

        public async Task<List<Artist>> GetAllArtistsAsync()
        {
            var artists = new List<Artist>();
            string url = $"{_baseUrl}/artists";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("documents", out var docs))
                    {
                        foreach (var d in docs.EnumerateArray())
                        {
                            // 👇 BỌC TRY-CATCH CHO TỪNG NGHỆ SĨ. Lỗi 1 người không ảnh hưởng người khác
                            try
                            {
                                var a = new Artist();
                                a.Id = d.GetProperty("name").GetString().Split('/').Last();

                                if (d.TryGetProperty("fields", out var fields))
                                {
                                    // 1. Lấy Tên
                                    if (fields.TryGetProperty("name", out var n)) a.Name = n.GetProperty("stringValue").GetString();
                                    else if (fields.TryGetProperty("Name", out var nCap)) a.Name = nCap.GetProperty("stringValue").GetString();

                                    if (string.IsNullOrEmpty(a.Name)) continue;

                                    // 2. Lấy Ảnh 
                                    if (fields.TryGetProperty("avatar", out var av)) a.Avatar = av.GetProperty("stringValue").GetString();
                                    else if (fields.TryGetProperty("coverImage", out var cImg)) a.Avatar = cImg.GetProperty("stringValue").GetString();

                                    if (string.IsNullOrEmpty(a.Avatar)) a.Avatar = "cover_chill.jpg";

                                    // 3. Lấy mô tả
                                    if (fields.TryGetProperty("bio", out var b)) a.Bio = b.GetProperty("stringValue").GetString();

                                    // 4. Lấy lượt theo dõi (CHỐNG TRÀN BỘ NHỚ)
                                    if (fields.TryGetProperty("followers", out var f) && f.TryGetProperty("integerValue", out var fVal))
                                    {
                                        try
                                        {
                                            // Thử parse sang số int
                                            a.Followers = fVal.ValueKind == JsonValueKind.String ? int.Parse(fVal.GetString()) : fVal.GetInt32();
                                        }
                                        catch
                                        {
                                            // Nếu số quá to (hơn 2 tỷ) gây lỗi, thì gán tạm bằng 0 để không chết App
                                            a.Followers = 0;
                                        }
                                    }

                                    artists.Add(a);
                                }
                            }
                            catch (Exception innerEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Lỗi tải 1 Nghệ sĩ: {innerEx.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi lấy Artists: {ex.Message}"); }
            return artists;
        }

        public async Task<List<Album>> GetAllAlbumsAsync()
        {
            var albums = new List<Album>();
            string url = $"{_baseUrl}/albums";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("documents", out var docs))
                    {
                        foreach (var d in docs.EnumerateArray())
                        {
                            var a = new Album();
                            a.Id = d.GetProperty("name").GetString().Split('/').Last();

                            if (d.TryGetProperty("fields", out var fields))
                            {
                                if (fields.TryGetProperty("title", out var t)) a.Title = t.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("artistId", out var aid)) a.ArtistId = aid.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("artistName", out var aname)) a.Artist = aname.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("coverImage", out var c)) a.CoverImage = c.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("releaseYear", out var y) && y.TryGetProperty("integerValue", out var yVal))
                                    a.Year = yVal.ValueKind == JsonValueKind.String ? yVal.GetString() : yVal.GetInt32().ToString();
                            }
                            albums.Add(a);
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi lấy Albums: {ex.Message}"); }
            return albums;
        }

        public async Task<List<Genre>> GetAllGenresAsync()
        {
            var genres = new List<Genre>();
            string url = $"{_baseUrl}/genres";
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("documents", out var docs))
                    {
                        foreach (var d in docs.EnumerateArray())
                        {
                            var g = new Genre();
                            g.Id = d.GetProperty("name").GetString().Split('/').Last();
                            if (d.TryGetProperty("fields", out var fields))
                            {
                                if (fields.TryGetProperty("name", out var n)) g.Name = n.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("coverImage", out var c)) g.CoverImage = c.GetProperty("stringValue").GetString();
                            }
                            genres.Add(g);
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi lấy Genres: {ex.Message}"); }
            return genres;
        }

        // ==========================================================
        // 3. HÀM LẤY BÀI HÁT (ĐÃ SỬA CHUẨN ĐỂ LẤY ID)
        // ==========================================================

        public async Task<List<Song>> GetAllSongsAsync()
        {
            var songs = new List<Song>();
            string url = $"{_baseUrl}/songs";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("documents", out var docs))
                    {
                        foreach (var d in docs.EnumerateArray())
                        {
                            if (d.TryGetProperty("fields", out var fields))
                            {
                                var s = new Song();

                                // 👇 QUAN TRỌNG: Bắt buộc phải lấy Id từ Document Name
                                s.Id = d.GetProperty("name").GetString().Split('/').Last();

                                // Lấy thông tin cơ bản
                                if (fields.TryGetProperty("title", out var t)) s.Title = t.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("artist", out var a)) s.Artist = a.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("album", out var alb)) s.Album = alb.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("audioUrl", out var u)) s.AudioUrl = u.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("coverImage", out var c)) s.CoverImage = c.GetProperty("stringValue").GetString();

                                // 👇 CÁC KHÓA NGOẠI (MỚI)
                                if (fields.TryGetProperty("artistId", out var artId)) s.ArtistId = artId.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("albumId", out var albId)) s.AlbumId = albId.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("genreId", out var genId)) s.GenreId = genId.GetProperty("stringValue").GetString();

                                // Lấy lyrics
                                if (fields.TryGetProperty("lyrics", out var ly) && ly.TryGetProperty("stringValue", out var lystr))
                                    s.Lyrics = lystr.GetString();

                                // Lấy thông tin Boolean
                                if (fields.TryGetProperty("isPremium", out var p) && p.TryGetProperty("booleanValue", out var bv)) s.IsPremium = bv.GetBoolean();
                                if (fields.TryGetProperty("isFeatured", out var f) && f.TryGetProperty("booleanValue", out var fv)) s.IsFeatured = fv.GetBoolean();

                                // Lấy SEARCH KEYWORDS
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

                                // Lấy LIKE COUNT
                                if (fields.TryGetProperty("likeCount", out var lc) && lc.TryGetProperty("integerValue", out var lv))
                                {
                                    if (lv.ValueKind == JsonValueKind.String)
                                        s.LikeCount = int.Parse(lv.GetString());
                                    else
                                        s.LikeCount = lv.GetInt32();
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi lấy nhạc Firestore: {ex.Message}");
            }
            return songs;
        }

        // ==========================================================
        // 4. CÁC HÀM TÌM KIẾM (SEARCH)
        // ==========================================================

        // ==========================================================
        // HÀM TÌM KIẾM THÔNG MINH - HỖ TRỢ TIẾNG VIỆT KHÔNG DẤU
        // ==========================================================
        public async Task<List<Song>> SearchSongsByKeywordsAsync(string keyword)
        {
            var resultList = new List<Song>();
            if (string.IsNullOrWhiteSpace(keyword)) return resultList;

            // Xử lý từ khóa: Đưa về chữ thường và cắt bỏ khoảng trắng thừa
            string searchWord = RemoveDiacritics(keyword.Trim().ToLower());

            string url = $"{_baseUrl}/songs";

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
                                // Lấy Tên và Nghệ sĩ để so sánh
                                string title = "";
                                string artist = "";

                                if (fields.TryGetProperty("title", out var t)) title = t.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("artist", out var a)) artist = a.GetProperty("stringValue").GetString();

                                // Chuyển đổi tên và nghệ sĩ về dạng không dấu, chữ thường
                                string normalizedTitle = RemoveDiacritics(title.ToLower());
                                string normalizedArtist = RemoveDiacritics(artist.ToLower());

                                // TÌM KIẾM THEO KIỂU CHỨA TỪ KHÓA (Ví dụ gõ "mua", ra "mùa xuân")
                                if (normalizedTitle.Contains(searchWord) || normalizedArtist.Contains(searchWord))
                                {
                                    var s = new Song();
                                    s.Id = d.GetProperty("name").GetString().Split('/').Last();
                                    s.Title = title;
                                    s.Artist = artist;

                                    if (fields.TryGetProperty("audioUrl", out var au)) s.AudioUrl = au.GetProperty("stringValue").GetString();
                                    if (fields.TryGetProperty("coverImage", out var cImg)) s.CoverImage = cImg.GetProperty("stringValue").GetString();
                                    if (fields.TryGetProperty("album", out var al)) s.Album = al.GetProperty("stringValue").GetString();

                                    if (fields.TryGetProperty("duration", out var dur) && dur.TryGetProperty("integerValue", out var durVal))
                                        s.Duration = durVal.ValueKind == JsonValueKind.String ? double.Parse(durVal.GetString()) : durVal.GetDouble();

                                    if (fields.TryGetProperty("isPremium", out var prem) && prem.TryGetProperty("booleanValue", out var pVal))
                                        s.IsPremium = pVal.GetBoolean();

                                    resultList.Add(s);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Lỗi hàm Tìm kiếm: {ex.Message}");
            }

            return resultList;
        }

        // Hàm phụ trợ: Loại bỏ dấu Tiếng Việt (ể ê ề -> e)
        private string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            // Loại bỏ chữ Đ đặc biệt của tiếng Việt
            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC).Replace("đ", "d").Replace("Đ", "D");
        }


        // ==========================================================
        // 5. CÁC HÀM PLAYLIST (ĐÃ CHUYỂN RA ROOT COLLECTION)
        // ==========================================================

        public async Task CreatePlaylistAndAddSong(string uid, string playlistName, Song song)
        {
            // 1. Tạo ID Playlist (VD: pl_123456789)
            string playlistId = $"pl_{DateTime.Now.Ticks}";

            // 2. Trỏ thẳng ra ROOT Collection 'playlists'
            string playlistUrl = $"{_baseUrl}/playlists/{playlistId}";

            try
            {
                var newPlaylist = new
                {
                    fields = new
                    {
                        name = new { stringValue = playlistName },
                        userId = new { stringValue = uid }, // Lưu ai là chủ sở hữu
                        coverImage = new { stringValue = song.CoverImage ?? "" },
                        isPublic = new { booleanValue = true },
                        songCount = new { integerValue = 0 }
                    }
                };

                // Tạo Playlist rỗng trước
                await _httpClient.PatchAsync(playlistUrl, new StringContent(JsonSerializer.Serialize(newPlaylist), Encoding.UTF8, "application/json"));

                // Thêm bài hát đầu tiên vào Playlist này
                await AddSongToExistingPlaylist(playlistId, song);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Lỗi tạo playlist: " + ex.Message); }
        }

        public async Task AddSongToExistingPlaylist(string playlistId, Song song)
        {
            if (string.IsNullOrEmpty(playlistId) || string.IsNullOrEmpty(song.Id)) return;

            // 👇 1. LƯU BÀI HÁT VÀO ROOT COLLECTION 'playlist_songs'
            // Dùng ID ghép: PlaylistId_SongId
            string docId = $"{playlistId}_{song.Id}";
            string songUrl = $"{_baseUrl}/playlist_songs/{docId}";

            try
            {
                var songData = new
                {
                    fields = new
                    {
                        playlistId = new { stringValue = playlistId },
                        songId = new { stringValue = song.Id },
                        title = new { stringValue = song.Title },
                        artist = new { stringValue = song.Artist },
                        audioUrl = new { stringValue = song.AudioUrl },
                        coverImage = new { stringValue = song.CoverImage },
                        album = new { stringValue = song.Album ?? "Unknown" },
                        duration = new { integerValue = (int)song.Duration }
                    }
                };

                await _httpClient.PatchAsync(songUrl, new StringContent(JsonSerializer.Serialize(songData), Encoding.UTF8, "application/json"));

                // 👇 2. Cập nhật tăng bộ đếm (songCount) cho Playlist cha
                string playlistUrl = $"{_baseUrl}/playlists/{playlistId}";
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
                        currentCount = val.ValueKind == JsonValueKind.String ? int.Parse(val.GetString()) : val.GetInt32();
                    }

                    var updatePayload = new { fields = new { songCount = new { integerValue = currentCount + 1 } } };
                    string updateUrl = $"{playlistUrl}?updateMask.fieldPaths=songCount";
                    await _httpClient.PatchAsync(updateUrl, new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json"));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi thêm bài: {ex.Message}"); }
        }

        public async Task RemoveSongFromPlaylist(string playlistId, Song song)
        {
            if (string.IsNullOrEmpty(playlistId) || string.IsNullOrEmpty(song.Id)) return;

            try
            {
                // 👇 XÓA KHỎI ROOT COLLECTION 'playlist_songs'
                string docId = $"{playlistId}_{song.Id}";
                string songUrl = $"{_baseUrl}/playlist_songs/{docId}";
                await _httpClient.DeleteAsync(songUrl);

                // 👇 Giảm bộ đếm (songCount) của Playlist cha
                string playlistUrl = $"{_baseUrl}/playlists/{playlistId}";
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
                        currentCount = val.ValueKind == JsonValueKind.String ? int.Parse(val.GetString()) : val.GetInt32();
                    }

                    int newCount = Math.Max(0, currentCount - 1);
                    var updatePayload = new { fields = new { songCount = new { integerValue = newCount } } };
                    string updateUrl = $"{playlistUrl}?updateMask.fieldPaths=songCount";
                    await _httpClient.PatchAsync(updateUrl, new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json"));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi xóa bài: {ex.Message}"); }
        }

        public async Task DeletePlaylist(string playlistId)
        {
            if (string.IsNullOrEmpty(playlistId)) return;

            // Xóa ở Root collection
            string url = $"{_baseUrl}/playlists/{playlistId}";
            try { await _httpClient.DeleteAsync(url); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi xóa Playlist: {ex.Message}"); }
        }

        // 👇 ĐÃ SỬA: Dùng Query để tìm trong Root Collection 'playlists'
        public async Task<List<Playlist>> GetUserPlaylists(string uid)
        {
            var playlists = new List<Playlist>();
            if (string.IsNullOrEmpty(uid)) return playlists;

            string runQueryUrl = $"{_baseUrl}:runQuery";
            var query = new
            {
                structuredQuery = new
                {
                    from = new[] { new { collectionId = "playlists" } },
                    where = new { fieldFilter = new { field = new { fieldPath = "userId" }, op = "EQUAL", value = new { stringValue = uid } } }
                }
            };

            try
            {
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
                            if (item.TryGetProperty("document", out var d) && d.TryGetProperty("fields", out var fields))
                            {
                                var p = new Playlist();
                                p.Id = d.GetProperty("name").GetString().Split('/').Last();
                                if (fields.TryGetProperty("name", out var n)) p.Name = n.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("coverImage", out var c)) p.CoverImage = c.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("userId", out var o)) p.OwnerId = o.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("songCount", out var sc) && sc.TryGetProperty("integerValue", out var val))
                                {
                                    p.SongCount = val.ValueKind == JsonValueKind.String ? int.Parse(val.GetString()) : val.GetInt32();
                                }
                                playlists.Add(p);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi lấy Playlist: {ex.Message}"); }
            return playlists;
        }

        // 👇 ĐÃ SỬA: Dùng Query để tìm trong Root Collection 'playlist_songs'
        public async Task<List<Song>> GetSongsFromPlaylist(string playlistId)
        {
            var songs = new List<Song>();
            if (string.IsNullOrEmpty(playlistId)) return songs;

            string runQueryUrl = $"{_baseUrl}:runQuery";
            var query = new
            {
                structuredQuery = new
                {
                    from = new[] { new { collectionId = "playlist_songs" } },
                    where = new { fieldFilter = new { field = new { fieldPath = "playlistId" }, op = "EQUAL", value = new { stringValue = playlistId } } }
                }
            };

            try
            {
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
                            if (item.TryGetProperty("document", out var d) && d.TryGetProperty("fields", out var fields))
                            {
                                var s = new Song();

                                // Lưu ý: Lấy ID thật của bài hát chứ không phải ID ghép
                                if (fields.TryGetProperty("songId", out var sid)) s.Id = sid.GetProperty("stringValue").GetString();

                                if (fields.TryGetProperty("title", out var t)) s.Title = t.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("artist", out var a)) s.Artist = a.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("audioUrl", out var u)) s.AudioUrl = u.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("coverImage", out var c)) s.CoverImage = c.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("duration", out var dur) && dur.TryGetProperty("integerValue", out var val))
                                    s.Duration = val.ValueKind == JsonValueKind.String ? double.Parse(val.GetString()) : val.GetDouble();

                                songs.Add(s);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi lấy nhạc từ Playlist: {ex.Message}"); }
            return songs;
        }

        // ==========================================================
        // 6. CÁC HÀM TƯƠNG TÁC (TIM / CẬP NHẬT) - ROOT COLLECTION
        // ==========================================================

        public async Task AddToFavoritesAsync(Song song)
        {
            try
            {
                string userId = Preferences.Get("UserId", "");
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(song.Id)) return;

                // 👇 1. TẠO ID GHÉP (COMPOSITE ID) ĐÚNG NHƯ BẠN ĐÃ LÀM TRÊN FIREBASE
                string docId = $"{userId}_{song.Id}";

                // 👇 2. Trỏ URL ra ROOT Collection 'favorites'
                string url = $"{_baseUrl}/favorites/{docId}";

                var payload = new
                {
                    fields = new
                    {
                        userId = new { stringValue = userId }, // Thêm userId để truy vấn
                        songId = new { stringValue = song.Id },
                        title = new { stringValue = song.Title },
                        artist = new { stringValue = song.Artist },
                        audioUrl = new { stringValue = song.AudioUrl },
                        coverImage = new { stringValue = song.CoverImage },
                        album = new { stringValue = song.Album ?? "" },
                        duration = new { integerValue = (int)song.Duration },
                        isPremium = new { booleanValue = song.IsPremium }
                    }
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                await _httpClient.PatchAsync(url, jsonContent);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Exception Thêm Yêu Thích: {ex.Message}"); }
        }

        public async Task RemoveFromFavoritesAsync(Song song)
        {
            try
            {
                string userId = Preferences.Get("UserId", "");
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(song.Id)) return;

                // 👇 Trỏ URL ra ROOT Collection 'favorites' với ID ghép
                string docId = $"{userId}_{song.Id}";
                string url = $"{_baseUrl}/favorites/{docId}";

                await _httpClient.DeleteAsync(url);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Exception Xóa Yêu Thích: {ex.Message}"); }
        }

        // ĐÃ SỬA: Hàm Get dùng Query để lấy ra các bài do User hiện tại thả tim
        public async Task<List<Song>> GetFavoritesAsync()
        {
            var songs = new List<Song>();
            string userId = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(userId)) return songs;

            string runQueryUrl = $"{_baseUrl}:runQuery";
            var query = new
            {
                structuredQuery = new
                {
                    from = new[] { new { collectionId = "favorites" } },
                    where = new
                    {
                        fieldFilter = new
                        {
                            field = new { fieldPath = "userId" }, // 👈 Chỉ lấy những bài do User này thả tim
                            op = "EQUAL",
                            value = new { stringValue = userId }
                        }
                    }
                }
            };

            try
            {
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
                            if (item.TryGetProperty("document", out var d) && d.TryGetProperty("fields", out var fields))
                            {
                                var s = new Song();

                                // Gán lại SongId chuẩn để khi mở UI nó không bị lỗi
                                if (fields.TryGetProperty("songId", out var sid)) s.Id = sid.GetProperty("stringValue").GetString();

                                if (fields.TryGetProperty("title", out var t)) s.Title = t.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("artist", out var a)) s.Artist = a.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("audioUrl", out var u)) s.AudioUrl = u.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("coverImage", out var c)) s.CoverImage = c.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("isPremium", out var p) && p.TryGetProperty("booleanValue", out var bv)) s.IsPremium = bv.GetBoolean();
                                if (fields.TryGetProperty("lyrics", out var ly) && ly.TryGetProperty("stringValue", out var lystr)) s.Lyrics = lystr.GetString();
                                if (fields.TryGetProperty("duration", out var dur) && dur.TryGetProperty("integerValue", out var val))
                                    s.Duration = val.ValueKind == JsonValueKind.String ? double.Parse(val.GetString()) : val.GetDouble();

                                if (!string.IsNullOrEmpty(s.AudioUrl)) songs.Add(s);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi lấy ds yêu thích: {ex.Message}"); }
            return songs;
        }

        public async Task<bool> IsSongInFavoritesAsync(Song song)
        {
            try
            {
                string userId = Preferences.Get("UserId", "");
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(song.Id)) return false;

                // 👇 Kiểm tra nhanh bằng ID ghép
                string docId = $"{userId}_{song.Id}";
                string url = $"{_baseUrl}/favorites/{docId}";

                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> IsSongInUserLibrary(string userId, Song song)
        {
            if (song == null || string.IsNullOrEmpty(song.Id)) return false;

            try
            {
                var playlists = await GetUserPlaylists(userId);
                if (playlists == null || playlists.Count == 0) return false;

                foreach (var playlist in playlists)
                {
                    string checkUrl = $"{_baseUrl}/users/{userId}/playlists/{playlist.Id}/songs/{song.Id}";
                    var response = await _httpClient.GetAsync(checkUrl);
                    if (response.IsSuccessStatusCode) return true;
                }
                return false;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi kiểm tra thư viện: {ex.Message}"); return false; }
        }

        // ĐÃ TỐI ƯU: Update thẳng vào Document ID, không cần Query tốn tài nguyên
        public async Task UpdateGlobalLikeCount(Song song, int change)
        {
            try
            {
                if (string.IsNullOrEmpty(song.Id)) return;

                // 1. Lấy Like hiện tại
                string getUrl = $"{_baseUrl}/songs/{song.Id}";
                var getResponse = await _httpClient.GetAsync(getUrl);
                int currentLike = 0;

                if (getResponse.IsSuccessStatusCode)
                {
                    var json = await getResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("fields", out var f) && f.TryGetProperty("likeCount", out var lc))
                    {
                        if (lc.TryGetProperty("integerValue", out var val))
                            currentLike = val.ValueKind == JsonValueKind.String ? int.Parse(val.GetString()) : val.GetInt32();
                    }
                }

                // 2. Gửi lệnh Update
                int newLike = Math.Max(0, currentLike + change);
                string updateUrl = $"{_baseUrl}/songs/{song.Id}?updateMask.fieldPaths=likeCount";
                var updatePayload = new { fields = new { likeCount = new { integerValue = newLike } } };

                await _httpClient.PatchAsync(updateUrl, new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json"));
                System.Diagnostics.Debug.WriteLine($"✅ Đã cập nhật Like lên Server: {newLike}");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"❌ Lỗi update like: {ex.Message}"); }
        }

        // ĐÃ TỐI ƯU: Update thẳng vào Document ID
        // ĐÃ NÂNG CẤP: Báo cáo kết quả Lưu thành công hay thất bại
        public async Task<bool> UpdateSongLyricsAsync(Song song)
        {
            if (song == null || string.IsNullOrEmpty(song.Lyrics) || string.IsNullOrEmpty(song.Id)) return false;

            try
            {
                string updateUrl = $"{_baseUrl}/songs/{song.Id}?updateMask.fieldPaths=lyrics";
                var updatePayload = new { fields = new { lyrics = new { stringValue = song.Lyrics } } };

                var response = await _httpClient.PatchAsync(updateUrl, new StringContent(System.Text.Json.JsonSerializer.Serialize(updatePayload), System.Text.Encoding.UTF8, "application/json"));

                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Đã lưu Lyric lên Firebase cho bài: {song.Title}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Lỗi lưu Lyric: {ex.Message}");
                return false;
            }
        }

        // ==========================================================
        // 7. LẤY BÀI HÁT THEO ALBUM HOẶC CA SĨ (TÌM BẰNG ID CHUẨN)
        // ==========================================================
        public async Task<List<Song>> GetSongsByAlbumIdAsync(string albumId)
        {
            var songs = new List<Song>();
            if (string.IsNullOrEmpty(albumId)) return songs;

            string runQueryUrl = $"{_baseUrl}:runQuery";
            var query = new
            {
                structuredQuery = new
                {
                    from = new[] { new { collectionId = "songs" } },
                    where = new
                    {
                        fieldFilter = new
                        {
                            field = new { fieldPath = "albumId" }, // 👈 TÌM BẰNG ID
                            op = "EQUAL",
                            value = new { stringValue = albumId }
                        }
                    }
                }
            };

            try
            {
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
                            if (item.TryGetProperty("document", out var d) && d.TryGetProperty("fields", out var fields))
                            {
                                var s = new Song();
                                s.Id = d.GetProperty("name").GetString().Split('/').Last();
                                if (fields.TryGetProperty("title", out var t)) s.Title = t.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("artist", out var a)) s.Artist = a.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("album", out var alb)) s.Album = alb.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("audioUrl", out var u)) s.AudioUrl = u.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("coverImage", out var c)) s.CoverImage = c.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("duration", out var dur) && dur.TryGetProperty("integerValue", out var val))
                                    s.Duration = val.ValueKind == JsonValueKind.String ? double.Parse(val.GetString()) : val.GetDouble();

                                if (!string.IsNullOrEmpty(s.AudioUrl)) songs.Add(s);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi lấy nhạc theo Album: {ex.Message}"); }
            return songs;
        }

        public async Task<List<Song>> GetSongsByArtistIdAsync(string artistId)
        {
            var songs = new List<Song>();
            if (string.IsNullOrEmpty(artistId)) return songs;

            string runQueryUrl = $"{_baseUrl}:runQuery";
            var query = new
            {
                structuredQuery = new
                {
                    from = new[] { new { collectionId = "songs" } },
                    where = new
                    {
                        fieldFilter = new
                        {
                            field = new { fieldPath = "artistId" }, // 👈 TÌM BẰNG ID
                            op = "EQUAL",
                            value = new { stringValue = artistId }
                        }
                    }
                }
            };

            try
            {
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
                            if (item.TryGetProperty("document", out var d) && d.TryGetProperty("fields", out var fields))
                            {
                                var s = new Song();
                                s.Id = d.GetProperty("name").GetString().Split('/').Last();
                                if (fields.TryGetProperty("title", out var t)) s.Title = t.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("artist", out var a)) s.Artist = a.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("album", out var alb)) s.Album = alb.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("audioUrl", out var u)) s.AudioUrl = u.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("coverImage", out var c)) s.CoverImage = c.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("duration", out var dur) && dur.TryGetProperty("integerValue", out var val))
                                    s.Duration = val.ValueKind == JsonValueKind.String ? double.Parse(val.GetString()) : val.GetDouble();

                                if (!string.IsNullOrEmpty(s.AudioUrl)) songs.Add(s);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi lấy nhạc theo Ca sĩ: {ex.Message}"); }
            return songs;
        }
        // BỔ SUNG: Hàm lấy riêng Lyric từ database để tránh bị mất dữ liệu
        public async Task<string> GetLyricsFromDatabaseAsync(string songId)
        {
            if (string.IsNullOrEmpty(songId)) return null;
            try
            {
                string url = $"{_baseUrl}/songs/{songId}";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("fields", out var fields) &&
                        fields.TryGetProperty("lyrics", out var ly))
                    {
                        return ly.GetProperty("stringValue").GetString();
                    }
                }
            }
            catch { }
            return null;
        }
        // ==========================================================
        // LẤY DANH SÁCH BÀI HÁT THEO THỂ LOẠI (DÙNG CHO TRANG GENRE DETAIL)
        // ==========================================================
        
        public async Task<List<Song>> GetSongsByGenreAsync(string targetGenreId)
        {
            var resultList = new List<Song>();
            if (string.IsNullOrWhiteSpace(targetGenreId)) return resultList;

            string url = $"{_baseUrl}/songs";

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
                                string dbGenreId = "";
                                // 👇 Đọc đúng trường genreId từ Firebase của bạn 👇
                                if (fields.TryGetProperty("genreId", out var g)) dbGenreId = g.GetProperty("stringValue").GetString();

                                // 👇 So sánh chính xác genreId 👇
                                if (string.Equals(dbGenreId, targetGenreId, StringComparison.OrdinalIgnoreCase))
                                {
                                    var s = new Song();
                                    s.Id = d.GetProperty("name").GetString().Split('/').Last();

                                    if (fields.TryGetProperty("title", out var t)) s.Title = t.GetProperty("stringValue").GetString();
                                    if (fields.TryGetProperty("artist", out var a)) s.Artist = a.GetProperty("stringValue").GetString();
                                    if (fields.TryGetProperty("audioUrl", out var au)) s.AudioUrl = au.GetProperty("stringValue").GetString();
                                    if (fields.TryGetProperty("coverImage", out var cImg)) s.CoverImage = cImg.GetProperty("stringValue").GetString();
                                    if (fields.TryGetProperty("album", out var al)) s.Album = al.GetProperty("stringValue").GetString();

                                    if (fields.TryGetProperty("duration", out var dur) && dur.TryGetProperty("integerValue", out var durVal))
                                        s.Duration = durVal.ValueKind == JsonValueKind.String ? double.Parse(durVal.GetString()) : durVal.GetDouble();

                                    if (fields.TryGetProperty("isPremium", out var prem) && prem.TryGetProperty("booleanValue", out var pVal))
                                        s.IsPremium = pVal.GetBoolean();

                                    resultList.Add(s);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi lấy thể loại: {ex.Message}"); }

            return resultList;
        }
        // ==========================================================
        // [COLLECTION 9/10]: NGHE GẦN ĐÂY (RECENTLY PLAYED)
        // ==========================================================
        public async Task AddToRecentlyPlayedAsync(string userId, Song song)
        {
            if (string.IsNullOrEmpty(userId) || song == null || string.IsNullOrEmpty(song.Id)) return;

            try
            {
                // Tạo ID duy nhất ghép từ User và Song để tránh trùng lặp
                string docId = $"{userId}_{song.Id}";
                string patchUrl = $"{_baseUrl}/recently_played/{docId}";

                var payload = new
                {
                    fields = new
                    {
                        userId = new { stringValue = userId },
                        songId = new { stringValue = song.Id },
                        title = new { stringValue = song.Title ?? "" },
                        artist = new { stringValue = song.Artist ?? "" },
                        coverImage = new { stringValue = song.CoverImage ?? "" },
                        audioUrl = new { stringValue = song.AudioUrl ?? "" },
                        isPremium = new { booleanValue = song.IsPremium },
                        // Lưu thời gian hiện tại để biết bài nào nghe mới nhất
                        playedAt = new { timestampValue = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") }
                    }
                };

                var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                await _httpClient.PatchAsync(patchUrl, content);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi thêm Recently Played: {ex.Message}"); }
        }

        public async Task<List<Song>> GetRecentlyPlayedAsync(string userId)
        {
            var list = new List<Song>();
            if (string.IsNullOrEmpty(userId)) return list;

            // Dùng query API của Firebase để lấy tất cả bài hát thuộc về userId này
            string queryUrl = $"https://firestore.googleapis.com/v1/projects/cosmicmusic-50df6/databases/(default)/documents:runQuery";

            var queryPayload = new
            {
                structuredQuery = new
                {
                    from = new[] { new { collectionId = "recently_played" } },
                    where = new
                    {
                        fieldFilter = new
                        {
                            field = new { fieldPath = "userId" },
                            op = "EQUAL",
                            value = new { stringValue = userId }
                        }
                    }
                }
            };

            try
            {
                var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(queryPayload), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(queryUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);

                    var tempSongs = new List<(Song song, DateTime playedAt)>();

                    foreach (var element in doc.RootElement.EnumerateArray())
                    {
                        if (element.TryGetProperty("document", out var d) && d.TryGetProperty("fields", out var fields))
                        {
                            var s = new Song();
                            if (fields.TryGetProperty("songId", out var idVal)) s.Id = idVal.GetProperty("stringValue").GetString();
                            if (fields.TryGetProperty("title", out var t)) s.Title = t.GetProperty("stringValue").GetString();
                            if (fields.TryGetProperty("artist", out var a)) s.Artist = a.GetProperty("stringValue").GetString();
                            if (fields.TryGetProperty("coverImage", out var c)) s.CoverImage = c.GetProperty("stringValue").GetString();
                            if (fields.TryGetProperty("audioUrl", out var au)) s.AudioUrl = au.GetProperty("stringValue").GetString();
                            if (fields.TryGetProperty("isPremium", out var prem) && prem.TryGetProperty("booleanValue", out var pVal)) s.IsPremium = pVal.GetBoolean();

                            DateTime playedAt = DateTime.MinValue;
                            if (fields.TryGetProperty("playedAt", out var p) && p.ValueKind != System.Text.Json.JsonValueKind.Null)
                            {
                                DateTime.TryParse(p.GetProperty("timestampValue").GetString(), out playedAt);
                            }
                            tempSongs.Add((s, playedAt));
                        }
                    }

                    // Sắp xếp bài hát mới nghe lên đầu tiên (giảm dần theo thời gian)
                    list = tempSongs.OrderByDescending(x => x.playedAt).Select(x => x.song).ToList();
                }
            }
            catch { }
            return list;
        }
        // ==========================================================
        // [COLLECTION 10/10]: CÁC GÓI PREMIUM (SUBSCRIPTIONS)
        // ==========================================================
        public async Task<List<Subscription>> GetSubscriptionsAsync()
        {
            var list = new List<Subscription>();
            string url = $"{_baseUrl}/subscriptions";

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
                                var sub = new Subscription();
                                sub.Id = d.GetProperty("name").GetString().Split('/').Last();

                                if (fields.TryGetProperty("name", out var n)) sub.Name = n.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("price", out var p)) sub.Price = p.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("description", out var desc)) sub.Description = desc.GetProperty("stringValue").GetString();
                                if (fields.TryGetProperty("backgroundColor", out var bg)) sub.BackgroundColor = bg.GetProperty("stringValue").GetString();

                                if (fields.TryGetProperty("durationInMonths", out var dur))
                                    sub.DurationInMonths = int.Parse(dur.GetProperty("integerValue").GetString());

                                // Đọc mảng các quyền lợi (Features)
                                if (fields.TryGetProperty("features", out var f) && f.TryGetProperty("arrayValue", out var arr) && arr.TryGetProperty("values", out var vals))
                                {
                                    foreach (var val in vals.EnumerateArray())
                                    {
                                        sub.Features.Add(val.GetProperty("stringValue").GetString());
                                    }
                                }
                                list.Add(sub);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi lấy gói Premium: {ex.Message}"); }

            return list;
        }
        // ==========================================================
        // THÊM BÀI HÁT MỚI VÀO DATABASE
        // ==========================================================
        public async Task<bool> AddSongAsync(Song song)
        {
            try
            {
                string url = $"{_baseUrl}/songs";
                var payload = new
                {
                    fields = new
                    {
                        title = new { stringValue = song.Title ?? "Unknown" },
                        artist = new { stringValue = song.Artist ?? "Unknown" },
                        coverImage = new { stringValue = song.CoverImage ?? "" },
                        audioUrl = new { stringValue = song.AudioUrl ?? "" },
                        genreId = new { stringValue = "genre_pop" }, // Mặc định gán vào mục Pop
                        isPremium = new { booleanValue = song.IsPremium },
                        isFeatured = new { booleanValue = false }
                    }
                };

                var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi thêm bài hát: {ex.Message}");
                return false;
            }
        }
    }


    public class UserFirestoreInfo
    {
        public string DisplayName { get; set; }
        public bool IsPremium { get; set; }
    }
}