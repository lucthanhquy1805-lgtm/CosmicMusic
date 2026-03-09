using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CosmicMusic.ViewModels
{
    public partial class HomeViewModel : ObservableObject, IRecipient<SongPlayedMessage>
    {
        // 👇 ĐÃ ĐỔI: Sử dụng FirestoreService thay cho MusicApiService cũ
        private readonly FirestoreService _firestoreService;
        private readonly AudioViewModel _audioViewModel;

        // ==========================================================
        // 1. CÁC DANH SÁCH DỮ LIỆU HIỂN THỊ (ĐÃ MỞ RỘNG)
        // ==========================================================
        public ObservableCollection<Song> Playlist { get; set; } = new();           // Dùng hiển thị danh sách bài hát
        public ObservableCollection<Album> FeaturedAlbums { get; set; } = new();    // Danh sách Album
        public ObservableCollection<Artist> Artists { get; set; } = new();          // 👇 Danh sách Ca sĩ (MỚI)
        public ObservableCollection<Genre> Genres { get; set; } = new();            // 👇 Danh sách Thể loại (MỚI)

        // Giữ lại TopArtists kiểu Album cho giao diện cũ (để không lỗi XAML)
        public ObservableCollection<Album> TopArtists { get; set; } = new();

        // Danh sách bài hát vừa nghe
        public ObservableCollection<Song> RecentlyPlayedList { get; set; } = new();

        public AudioViewModel AudioPlayer => _audioViewModel;

        // ==========================================================
        // 2. CÁC BIẾN GIAO DIỆN
        // ==========================================================
        [ObservableProperty] private bool _isUserMenuVisible = false;
        [ObservableProperty] private string _userAvatarText;
        [ObservableProperty] private string _userName;
        [ObservableProperty] private bool _isPremiumUser;
        [ObservableProperty] private string _avatarBorderColor = "#6C63FF";

        [ObservableProperty] private bool _hasHistory;

        // 3. Khởi tạo
        // 👇 ĐÃ SỬA: Bơm (Inject) FirestoreService vào
        public HomeViewModel(FirestoreService firestoreService, AudioViewModel audioViewModel)
        {
            _firestoreService = firestoreService;
            _audioViewModel = audioViewModel;

            // Đăng ký nhận tin nhắn
            WeakReferenceMessenger.Default.Register<SongPlayedMessage>(this);

            LoadUserAvatar();
            LoadHistory();
            LoadDataFromFirebase(); // Đổi tên hàm cho rõ nghĩa
        }

        // ==========================================================
        // XỬ LÝ LỊCH SỬ NGHE NHẠC (RECENTLY PLAYED)
        // ==========================================================
        public void Receive(SongPlayedMessage message)
        {
            var song = message.PlayedSong;
            if (song == null) return;

            // Đảm bảo cập nhật UI trên Main Thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var existing = RecentlyPlayedList.FirstOrDefault(s => s.Title == song.Title);
                if (existing != null) RecentlyPlayedList.Remove(existing);

                RecentlyPlayedList.Insert(0, song);

                if (RecentlyPlayedList.Count > 10)
                {
                    RecentlyPlayedList.RemoveAt(RecentlyPlayedList.Count - 1);
                }

                HasHistory = RecentlyPlayedList.Count > 0;
                SaveHistory();
            });
        }

        private void SaveHistory()
        {
            try
            {
                string userId = Preferences.Get("UserId", "Guest");
                string key = $"History_{userId}";
                var json = JsonSerializer.Serialize(RecentlyPlayedList);
                Preferences.Set(key, json);
            }
            catch { }
        }

        private void LoadHistory()
        {
            try
            {
                string userId = Preferences.Get("UserId", "Guest");
                string key = $"History_{userId}";
                string json = Preferences.Get(key, "");

                RecentlyPlayedList.Clear();

                if (!string.IsNullOrEmpty(json))
                {
                    var list = JsonSerializer.Deserialize<List<Song>>(json);
                    if (list != null && list.Count > 0)
                    {
                        foreach (var item in list) RecentlyPlayedList.Add(item);
                    }
                }
                HasHistory = RecentlyPlayedList.Count > 0;
            }
            catch
            {
                HasHistory = false;
            }
        }

        // ==========================================================
        // 4. HÀM TẢI DỮ LIỆU TỪ FIREBASE (ĐÃ SỬA CHỮA HOÀN TOÀN)
        // ==========================================================
        private async void LoadDataFromFirebase()
        {
            try
            {
                // Gọi song song các tác vụ để tăng tốc độ tải
                var taskSongs = _firestoreService.GetAllSongsAsync();
                var taskAlbums = _firestoreService.GetAllAlbumsAsync();
                var taskArtists = _firestoreService.GetAllArtistsAsync();
                var taskGenres = _firestoreService.GetAllGenresAsync();

                // Chờ tất cả dữ liệu tải xong
                await Task.WhenAll(taskSongs, taskAlbums, taskArtists, taskGenres);

                // Cập nhật UI trên Main Thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // 1. Gán danh sách Bài hát
                    Playlist.Clear();
                    foreach (var song in taskSongs.Result)
                    {
                        Playlist.Add(song);
                    }

                    // 2. Gán danh sách Album
                    FeaturedAlbums.Clear();
                    foreach (var album in taskAlbums.Result)
                    {
                        FeaturedAlbums.Add(album);
                    }

                    // 3. Gán danh sách Ca sĩ (Model mới)
                    Artists.Clear();
                    foreach (var artist in taskArtists.Result)
                    {
                        Artists.Add(artist);
                    }

                    // 4. Gán danh sách Thể loại
                    Genres.Clear();
                    foreach (var genre in taskGenres.Result)
                    {
                        Genres.Add(genre);
                    }

                    // --- GIỮ LẠI ĐỂ TƯƠNG THÍCH XAML CŨ ---
                    // Vì giao diện Home của bạn đang bind biến TopArtists (dạng List<Album>)
                    // Nên tôi map dữ liệu Artist mới lấy được qua chuẩn cũ để giao diện hiển thị ngay lập tức
                    TopArtists.Clear();
                    foreach (var artist in taskArtists.Result)
                    {
                        TopArtists.Add(new Album
                        {
                            Id = artist.Id, // 👈 THÊM DÒNG NÀY ĐỂ KHI BẤM VÀO CA SĨ NÓ CÓ ID ĐỂ TÌM
                            Title = artist.Name,
                            Artist = "Nghệ sĩ",
                            CoverImage = artist.Avatar,
                            Description = "Artist"
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi HomeViewModel (LoadData): {ex.Message}");
            }
        }

        // ==========================================================
        // 5. CÁC HÀM CẬP NHẬT USER
        // ==========================================================
        public void LoadUserAvatar()
        {
            string email = Preferences.Get("UserEmail", "");
            UserAvatarText = !string.IsNullOrEmpty(email) ? email.Substring(0, 1).ToUpper() : "?";

            string savedFullName = Preferences.Get("UserName", "");
            if (!string.IsNullOrEmpty(savedFullName)) UserName = savedFullName;
            else if (!string.IsNullOrEmpty(email)) UserName = email;
            else UserName = "Khách";

            CheckPremiumStatus();
        }

        private void CheckPremiumStatus()
        {
            bool isSessionVip = Preferences.Get("IsPremium", false);
            string email = Preferences.Get("UserEmail", "");
            bool isHistoryVip = Preferences.Get($"VIP_{email}", false);
            IsPremiumUser = isSessionVip || isHistoryVip;

            if (IsPremiumUser)
            {
                AvatarBorderColor = "#FFD700";
                if (!isSessionVip) Preferences.Set("IsPremium", true);
            }
            else
            {
                AvatarBorderColor = "#6C63FF";
            }
        }

        // ==========================================================
        // 6. CÁC LỆNH ĐIỀU HƯỚNG
        // ==========================================================

        [RelayCommand]
        public async Task OpenAlbum(Album albumItem)
        {
            if (albumItem == null) return;
            var param = new Dictionary<string, object> { { "AlbumData", albumItem } };
            await Shell.Current.GoToAsync(nameof(AlbumDetailPage), param);
        }

        [RelayCommand]
        public async Task SelectSong(Song song)
        {
            if (song == null) return;
            bool isCurrentVip = Preferences.Get("IsPremium", false);
            if (song.IsPremium == true && isCurrentVip == false)
            {
                bool answer = await Shell.Current.DisplayAlert("Premium Content 👑", "Bài này dành cho VIP. Nâng cấp nhé?", "Xem gói VIP", "Để sau");
                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                return;
            }

            _audioViewModel.PlaySong(song, Playlist);
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }

        [RelayCommand]
        public async Task PerformLogout()
        {
            IsUserMenuVisible = false;
            bool answer = await Shell.Current.DisplayAlert("Đăng xuất", "Bạn muốn thoát?", "Có", "Không");
            if (answer)
            {
                _audioViewModel.Cleanup();

                Preferences.Remove("AuthToken");
                Preferences.Remove("UserEmail");
                Preferences.Remove("UserName");
                Preferences.Remove("UserId");
                Preferences.Remove("IsPremium");

                IsPremiumUser = false;
                AvatarBorderColor = "#6C63FF";
                UserAvatarText = "?";
                UserName = "Khách";

                RecentlyPlayedList.Clear();
                HasHistory = false;

                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
            }
        }

        [RelayCommand] public async Task NavigateToPlayer() { if (_audioViewModel.CurrentSong != null) await Shell.Current.GoToAsync(nameof(PlayerPage)); }
        [RelayCommand] public async Task NavigateToSearch() { await Shell.Current.GoToAsync("//SearchTab/SearchPage"); }
        [RelayCommand] public void TapUserAvatar() { IsUserMenuVisible = !IsUserMenuVisible; }
        [RelayCommand] public void CloseUserMenu() { IsUserMenuVisible = false; }
        [RelayCommand] public async Task OpenProfile() { IsUserMenuVisible = false; await Shell.Current.GoToAsync(nameof(ProfilePage)); }
        [RelayCommand] public async Task OpenSettings() { IsUserMenuVisible = false; await Shell.Current.GoToAsync(nameof(SettingsPage)); }
        [RelayCommand] public async Task AddAccount() { await Shell.Current.DisplayAlert("Thông báo", "Tính năng đang phát triển", "OK"); }
        [RelayCommand] public async Task OpenWhatsNew() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Mới", "Update tính năng Group Album!", "OK"); }
        [RelayCommand] public async Task OpenStats() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Thống kê", "Bạn đã nghe nhạc rất nhiều!", "OK"); }
        [RelayCommand] public async Task OpenHistory() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Gần đây", "Danh sách đã xem...", "OK"); }
    }
}