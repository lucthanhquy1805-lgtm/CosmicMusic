using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace CosmicMusic.ViewModels
{
    public partial class HomeViewModel : ObservableObject, IRecipient<SongPlayedMessage>
    {
        private readonly FirestoreService _firestoreService;
        private readonly AudioViewModel _audioViewModel;

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

            LoadUserAvatar();
            LoadDataFromFirebase();
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


        // ==========================================================
        // 4. HÀM TẢI DỮ LIỆU TỪ FIREBASE
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

                await Task.WhenAll(taskSongs, taskAlbums, taskArtists, taskGenres);

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

            // 👇 Sử dụng list nhạc tương ứng làm danh sách chờ (ContextList)
            // Nếu bấm từ Recently Played -> Danh sách chờ là Recently Played
            // Nếu bấm từ Recommend -> Danh sách chờ là Playlist tổng
            var contextList = RecentlyPlayed.Contains(song) ? RecentlyPlayed : Playlist;
            _audioViewModel.PlaySong(song, contextList);

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

                // Xóa UI
                RecentlyPlayed.Clear();
                HasRecentlyPlayed = false;

                await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
            }
        }

        [RelayCommand] public async Task NavigateToPlayer() { if (_audioViewModel.CurrentSong != null) await Shell.Current.GoToAsync(nameof(PlayerPage)); }
        [RelayCommand] public async Task NavigateToSearch() { await Shell.Current.GoToAsync("//SearchTab"); }
        [RelayCommand] public void TapUserAvatar() { IsUserMenuVisible = !IsUserMenuVisible; }
        [RelayCommand] public void CloseUserMenu() { IsUserMenuVisible = false; }
        [RelayCommand] public async Task OpenProfile() { IsUserMenuVisible = false; await Shell.Current.GoToAsync(nameof(ProfilePage)); }
        [RelayCommand] public async Task OpenSettings() { IsUserMenuVisible = false; await Shell.Current.GoToAsync(nameof(SettingsPage)); }
        [RelayCommand] public async Task AddAccount() { await Shell.Current.DisplayAlert("Thông báo", "Tính năng đang phát triển", "OK"); }
        [RelayCommand] public async Task OpenWhatsNew() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Mới", "Update tính năng Group Album!", "OK"); }
        [RelayCommand] public async Task OpenStats() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Thống kê", "Bạn đã nghe nhạc rất nhiều!", "OK"); }
        [RelayCommand] public async Task OpenHistory() { IsUserMenuVisible = false; await Shell.Current.DisplayAlert("Gần đây", "Tính năng này hiện được hiển thị ở màn hình chính.", "OK"); }
    }
}