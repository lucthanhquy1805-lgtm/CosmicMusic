using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Storage; // 👇 THÊM: Để gọi Preferences không bị lỗi
using Microsoft.Maui.ApplicationModel; // 👇 THÊM: Để gọi MainThread không bị lỗi
using System;

namespace CosmicMusic.ViewModels
{
    // 👇 BỔ SUNG: Khai báo thêm IRecipient<UserAvatarChangedMessage> ở cuối dòng này
    public partial class HomeViewModel : ObservableObject, IRecipient<SongPlayedMessage>, IRecipient<RefreshLibraryMessage>, IRecipient<UserAvatarChangedMessage>
    {
        private readonly FirestoreService _firestoreService;
        private readonly AudioViewModel _audioViewModel;
        private bool _isNavigating = false;

        // ==========================================================
        // 1. CÁC DANH SÁCH DỮ LIỆU HIỂN THỊ
        // ==========================================================
        public ObservableCollection<Song> Playlist { get; set; } = new();
        public ObservableCollection<Album> FeaturedAlbums { get; set; } = new();
        public ObservableCollection<Artist> Artists { get; set; } = new();
        public ObservableCollection<Genre> Genres { get; set; } = new();

        // Tương thích XAML cũ
        public ObservableCollection<Album> TopArtists { get; set; } = new();

        // 👇 CHỈ DÙNG 1 BIẾN DUY NHẤT CHO FIREBASE RECENTLY PLAYED 👇
        public ObservableCollection<Song> RecentlyPlayed { get; set; } = new();
        public AudioViewModel AudioPlayer => _audioViewModel;

        // ==========================================================
        // 2. CÁC BIẾN GIAO DIỆN
        // ==========================================================
        [ObservableProperty] private bool _isUserMenuVisible = false;
        [ObservableProperty] private string _userAvatarText;
        [ObservableProperty] private string _userName;
        [ObservableProperty] private bool _isPremiumUser;
        [ObservableProperty] private string _avatarBorderColor = "#6C63FF";
        [ObservableProperty] private ObservableCollection<Song> _recommendedSongs = new();
        [ObservableProperty] private string _recommendationTitle;
        [ObservableProperty] private bool _hasRecommendations = false;
        [ObservableProperty]
        private string _headerPhotoUrl;

        [ObservableProperty] private bool _hasRecentlyPlayed;

        // ==========================================================
        // 3. KHỞI TẠO VÀ LẮNG NGHE SỰ KIỆN
        // ==========================================================
        public HomeViewModel(FirestoreService firestoreService, AudioViewModel audioViewModel)
        {
            _firestoreService = firestoreService;
            _audioViewModel = audioViewModel;

            // Lắng nghe: Cứ bài nào phát là Load lại lịch sử từ Firebase!
            WeakReferenceMessenger.Default.Register<SongPlayedMessage>(this);

            // 👇 BỔ SUNG: Lắng nghe thư yêu cầu Tải lại dữ liệu (Khi có bài hát/ca sĩ mới)
            WeakReferenceMessenger.Default.Register<RefreshLibraryMessage>(this);

            // 👇 BỔ SUNG: Đăng ký nghe thư báo thay đổi Avatar
            WeakReferenceMessenger.Default.Register<UserAvatarChangedMessage>(this);

            LoadUserAvatar();
            LoadDataFromFirebase();

            // Lấy link ảnh từ máy tính bảng (nếu có)
            HeaderPhotoUrl = Preferences.Get("UserPhotoUrl", "");
        }

        // MAUI Tự động gọi hàm này khi có tin nhắn SongPlayedMessage từ Trình phát nhạc
        public void Receive(SongPlayedMessage message)
        {
            var song = message.PlayedSong;
            if (song == null) return;

            // Cập nhật giao diện NGAY LẬP TỨC mà không cần đợi Firebase tải lại
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Kiểm tra xem bài hát này đã có trong danh sách Nghe gần đây chưa
                var existingSong = RecentlyPlayed.FirstOrDefault(s => s.Id == song.Id);

                // Nếu có rồi thì xóa cái cũ đi
                if (existingSong != null)
                {
                    RecentlyPlayed.Remove(existingSong);
                }

                // Chèn bài vừa bấm nghe lên VỊ TRÍ ĐẦU TIÊN
                RecentlyPlayed.Insert(0, song);

                // Nếu danh sách dài quá 10 bài thì xóa bớt bài cuối cùng
                if (RecentlyPlayed.Count > 10)
                {
                    RecentlyPlayed.RemoveAt(RecentlyPlayed.Count - 1);
                }

                HasRecentlyPlayed = RecentlyPlayed.Count > 0;
            });
        }

        // 👇 BỔ SUNG: MAUI tự động gọi hàm này khi có người thêm Bài Hát / Ca sĩ mới
        public void Receive(RefreshLibraryMessage message)
        {
            // Ra lệnh tải lại toàn bộ dữ liệu từ Firebase
            LoadDataFromFirebase();
        }

        // Hàm bắt thư báo ảnh thay đổi (Bạn đã viết rất đúng ở dưới, tôi đưa lên đây cho gọn chung nhóm Receive)
        public void Receive(UserAvatarChangedMessage message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                HeaderPhotoUrl = message.NewAvatarUrl;
            });
        }

        // ==========================================================
        // 4. HÀM TẢI DỮ LIỆU TỪ FIREBASE
        // ==========================================================
        // ==========================================================
        // 4. HÀM TẢI DỮ LIỆU TỪ FIREBASE (ĐÃ TÍCH HỢP ĐỀ XUẤT NHẠC)
        // ==========================================================
        private async void LoadDataFromFirebase()
        {
            try
            {
                var taskSongs = _firestoreService.GetAllSongsAsync();
                var taskAlbums = _firestoreService.GetAllAlbumsAsync();
                var taskArtists = _firestoreService.GetAllArtistsAsync();
                var taskGenres = _firestoreService.GetAllGenresAsync();

                // Đồng thời tải luôn lịch sử nghe nhạc của User này
                await LoadRecentlyPlayedAsync();

                // Đợi tất cả các truy vấn cơ bản hoàn tất
                await Task.WhenAll(taskSongs, taskAlbums, taskArtists, taskGenres);

                // 👇 ĐÃ SỬA: Khai báo rõ ràng kiểu Tuple có tên (Songs, Title)
                string uid = Preferences.Get("UserId", "");

                // Tạo sẵn biến kết quả với giá trị rỗng mặc định
                (List<Song> Songs, string Title) suggestionResult = (new List<Song>(), "");

                if (!string.IsNullOrEmpty(uid))
                {
                    // Lấy đề xuất dựa trên ID của user và await trực tiếp luôn
                    suggestionResult = await _firestoreService.GetRecommendationsAsync(uid);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Playlist.Clear();
                    foreach (var song in taskSongs.Result) Playlist.Add(song);

                    FeaturedAlbums.Clear();
                    foreach (var album in taskAlbums.Result) FeaturedAlbums.Add(album);

                    Artists.Clear();
                    foreach (var artist in taskArtists.Result) Artists.Add(artist);

                    Genres.Clear();
                    foreach (var genre in taskGenres.Result) Genres.Add(genre);

                    // 👇 CẬP NHẬT GIAO DIỆN ĐỀ XUẤT 👇
                    RecommendedSongs.Clear();

                    // Đã kiểm tra null cẩn thận để tránh lỗi Crash App
                    if (suggestionResult.Songs != null && suggestionResult.Songs.Count > 0)
                    {
                        RecommendationTitle = suggestionResult.Title;
                        foreach (var song in suggestionResult.Songs)
                        {
                            RecommendedSongs.Add(song);
                        }
                        HasRecommendations = true;
                    }
                    else
                    {
                        HasRecommendations = false; // Tự động ẩn UI nếu không có bài nào
                    }

                    // Tương thích cho list XAML cũ
                    TopArtists.Clear();
                    foreach (var artist in taskArtists.Result)
                    {
                        TopArtists.Add(new Album
                        {
                            Id = artist.Id,
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

        // 👇 ĐÂY LÀ TRÁI TIM CỦA TÍNH NĂNG "NGHE GẦN ĐÂY" LẤY TỪ FIREBASE 👇
        private async Task LoadRecentlyPlayedAsync()
        {
            string uid = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(uid)) return;

            var recentSongs = await _firestoreService.GetRecentlyPlayedAsync(uid);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                RecentlyPlayed.Clear();
                foreach (var song in recentSongs.Take(10))
                {
                    RecentlyPlayed.Add(song);
                }
                HasRecentlyPlayed = RecentlyPlayed.Count > 0;
            });
        }

        // ==========================================================
        // 5. QUẢN LÝ NGƯỜI DÙNG
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
            if (albumItem == null || _isNavigating) return;
            try
            {
                _isNavigating = true;
                var param = new Dictionary<string, object> { { "AlbumData", albumItem } };
                await Shell.Current.GoToAsync(nameof(AlbumDetailPage), param);
            }
            finally { await Task.Delay(500); _isNavigating = false; }
        }


        [RelayCommand]
        public async Task SelectSong(Song song)
        {
            // Chặn ngay lập tức nếu dữ liệu rỗng hoặc App đang trong quá trình chuyển trang
            if (song == null || _isNavigating) return;

            try
            {
                _isNavigating = true; // 🔒 Bấm chốt khóa cửa lại

                bool isCurrentVip = Preferences.Get("IsPremium", false);
                if (song.IsPremium == true && isCurrentVip == false)
                {
                    bool answer = await Shell.Current.DisplayAlert("Premium Content 👑", "Bài này dành cho VIP. Nâng cấp nhé?", "Xem gói VIP", "Để sau");
                    if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                    return;
                }

                // Phát nhạc
                var contextList = RecentlyPlayed.Contains(song) ? RecentlyPlayed : Playlist;
                _audioViewModel.PlaySong(song, contextList);

                // Mở trang Player
                await Shell.Current.GoToAsync(nameof(PlayerPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Lỗi chuyển trang Player: {ex.Message}");
            }
            finally
            {
                // 🔓 Đợi 0.5 giây rồi mới mở khóa, đảm bảo miễn nhiễm 100% với trò Spam Click
                await Task.Delay(500);
                _isNavigating = false;
            }
        }

        // 👇 ĐÃ THÊM LẠI HÀM LOGOUT Ở ĐÂY 👇
        [RelayCommand]
        public async Task PerformLogout()
        {
            IsUserMenuVisible = false; // Đóng cái menu đen lại

            bool answer = await Shell.Current.DisplayAlert("Đăng xuất", "Bạn có chắc chắn muốn đăng xuất khỏi Cosmic Music?", "Có", "Không");

            if (answer)
            {
                try
                {
                    _isNavigating = true;

                    if (_audioViewModel != null)
                    {
                        _audioViewModel.Cleanup();
                    }

                    Preferences.Remove("AuthToken");
                    Preferences.Remove("UserEmail");
                    Preferences.Remove("UserName");
                    Preferences.Remove("UserId");
                    Preferences.Remove("IsPremium");

                    IsPremiumUser = false;
                    AvatarBorderColor = "#6C63FF";
                    UserAvatarText = "?";
                    UserName = "Khách";

                    RecentlyPlayed?.Clear();
                    HasRecentlyPlayed = false;

                    // Dùng 3 dấu /// để ra lệnh cho MAUI xóa sạch lịch sử trang và quay về Root
                    await Shell.Current.GoToAsync($"///{nameof(LoginPage)}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Lỗi Đăng xuất: {ex.Message}");
                }
                finally
                {
                    await Task.Delay(500);
                    _isNavigating = false;
                }
            }
        }


        [RelayCommand]
        public async Task NavigateToPlayer()
        {
            if (_audioViewModel.CurrentSong == null || _isNavigating) return;
            try { _isNavigating = true; await Shell.Current.GoToAsync(nameof(PlayerPage)); }
            finally { await Task.Delay(500); _isNavigating = false; }
        }

        [RelayCommand]
        public async Task NavigateToSearch()
        {
            if (_isNavigating) return;
            try { _isNavigating = true; await Shell.Current.GoToAsync("//SearchTab"); }
            finally { await Task.Delay(500); _isNavigating = false; }
        }

        [RelayCommand]
        public async Task OpenProfile()
        {
            if (_isNavigating) return;
            IsUserMenuVisible = false;
            try { _isNavigating = true; await Shell.Current.GoToAsync(nameof(ProfilePage)); }
            finally { await Task.Delay(500); _isNavigating = false; }
        }

        [RelayCommand]
        public async Task OpenSettings()
        {
            if (_isNavigating) return;
            IsUserMenuVisible = false;
            try { _isNavigating = true; await Shell.Current.GoToAsync(nameof(SettingsPage)); }
            finally { await Task.Delay(500); _isNavigating = false; }
        }

        // ==========================================================
        // CÁC LỆNH KHÔNG CHUYỂN TRANG (Chỉ hiện Popup/Menu thì không cần khóa)
        // ==========================================================
        [RelayCommand] public void TapUserAvatar() { IsUserMenuVisible = !IsUserMenuVisible; }
        [RelayCommand] public void CloseUserMenu() { IsUserMenuVisible = false; }
        [RelayCommand] public async Task AddAccount() { await Shell.Current.DisplayAlert("Thông báo", "Tính năng đang phát triển", "OK"); }
        [RelayCommand] public async Task OpenWhatsNew() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Mới", "Update tính năng Group Album!", "OK"); }
        [RelayCommand] public async Task OpenStats() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Thống kê", "Bạn đã nghe nhạc rất nhiều!", "OK"); }
        [RelayCommand] public async Task OpenHistory() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Gần đây", "Tính năng này hiện được hiển thị ở màn hình chính.", "OK"); }
    }
}