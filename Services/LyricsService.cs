using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web; // Nếu báo lỗi thiếu cái này, bạn chỉ cần dùng Uri.EscapeDataString là đủ

namespace CosmicMusic.Services
{
    public class LyricsService
    {
        private readonly HttpClient _httpClient;

        public LyricsService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CosmicMusicApp/1.0");
        }

        public async Task<string> GetLyricsAsync(string title, string artist)
        {
            try
            {
                // Dọn dẹp tên bài hát và ca sĩ
                string cleanTitle = CleanSongTitle(title);
                string cleanArtist = CleanArtist(artist);

                // 🔥 CHIẾN THUẬT 1: Tra cứu trên LrcLib (Nguồn chính - Rất mạnh)
                string lyrics = await GetFromLrcLib(cleanTitle, cleanArtist);
                if (!string.IsNullOrEmpty(lyrics)) return lyrics;

                // 🔥 CHIẾN THUẬT 2: Tra cứu trên Lyrics.ovh (Nguồn dự phòng)
                // (Chỉ dùng khi LrcLib bó tay)
                lyrics = await GetFromLyricsOvh(cleanTitle, cleanArtist);
                if (!string.IsNullOrEmpty(lyrics)) return lyrics;

                return ""; // Chịu thua
            }
            catch (Exception ex)
            {
                return $"Lỗi tìm kiếm: {ex.Message}";
            }
        }

        // --- NGUỒN 1: LRCLIB ---
        private async Task<string> GetFromLrcLib(string title, string artist)
        {
            try
            {
                // Tìm kiếm linh hoạt: Tên bài + Tên ca sĩ
                string query = $"{title} {artist}";
                string url = $"https://lrclib.net/api/search?q={Uri.EscapeDataString(query)}";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonList = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (jsonList.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in jsonList.EnumerateArray())
                        {
                            // Lấy bài nào có lời (plainLyrics)
                            if (item.TryGetProperty("plainLyrics", out var l) && !string.IsNullOrEmpty(l.GetString()))
                            {
                                // (Tùy chọn) Kiểm tra xem tên bài hát có khớp tương đối không để tránh lấy nhầm
                                // Nhưng hiện tại cứ lấy kết quả đầu tiên cho dễ trúng
                                return l.GetString();
                            }
                        }
                    }
                }
            }
            catch { /* Lỗi thì bỏ qua để thử nguồn khác */ }
            return "";
        }

        // --- NGUỒN 2: LYRICS.OVH ---
        private async Task<string> GetFromLyricsOvh(string title, string artist)
        {
            try
            {
                // API này cần chính xác: /v1/CaSi/TenBai
                string url = $"https://api.lyrics.ovh/v1/{Uri.EscapeDataString(artist)}/{Uri.EscapeDataString(title)}";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (json.TryGetProperty("lyrics", out var l))
                    {
                        return l.GetString();
                    }
                }
            }
            catch { /* Lỗi thì bỏ qua */ }
            return "";
        }

        // --- HÀM DỌN DẸP DỮ LIỆU ---
        private string CleanSongTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            // Xóa ngoặc: "Em Của Ngày Hôm Qua (Remix)" -> "Em Của Ngày Hôm Qua"
            string clean = Regex.Replace(title, @"\(.*?\)", "").Trim();
            // Xóa các từ thừa: ft., feat., Official, MV...
            clean = Regex.Replace(clean, @"\b(ft\.|feat\.|Remix|Official|MV|Video)\b.*", "", RegexOptions.IgnoreCase).Trim();
            // Xóa dấu gạch ngang thừa: "An Thần - Low G" -> "An Thần"
            clean = Regex.Replace(clean, @"\s-\s.*", "").Trim();
            return clean;
        }

        private string CleanArtist(string artist)
        {
            if (string.IsNullOrEmpty(artist)) return "";
            // "Sơn Tùng M-TP, Thiều Bảo Trâm" -> "Sơn Tùng M-TP"
            return artist.Split(',')[0].Split('&')[0].Trim();
        }
    }
}