using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;
// 👇 1. ADD THESE USING STATEMENTS FOR TOAST
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace CosmicMusic.ViewModels
{
    public partial class AlbumDetailViewModel : ObservableObject, IQueryAttributable
    {
        private readonly FirestoreService _firestoreService;
        private readonly MusicApiService _musicApiService;

        // MiniPlayer needs this
        [ObservableProperty] private AudioViewModel _audioPlayer;
        [ObservableProperty] private bool _isBusy;

        // Display Data (Binding)
        [ObservableProperty] private string _coverImage;
        [ObservableProperty] private string _mainTitle;
        [ObservableProperty] private string _subTitle;
        [ObservableProperty] private bool _isAlbumType;

        // Internal data to know what is being viewed
        private string _currentId;     // Playlist ID (if viewing playlist)
        private string _currentType;   // "Playlist", "Album", "Artist", or "Favorites"
        private Album _receivedAlbum;  // Album data received from Home

        public ObservableCollection<Song> Songs { get; } = new();

        // Inject MusicApiService
        public AlbumDetailViewModel(FirestoreService firestoreService, MusicApiService musicApiService, AudioViewModel audioPlayer)
        {
            _firestoreService = firestoreService;
            _musicApiService = musicApiService;
            AudioPlayer = audioPlayer;
        }

        // 👇 DATA RECEIVING FUNCTION (HANDLES ALL CASES)
        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            Songs.Clear(); // Clear old data first

            // CASE 0: Received from Library (Favorites) - MỚI THÊM
            if (query.ContainsKey("Type") && query["Type"].ToString() == "Favorites")
            {
                _currentType = "Favorites";
                MainTitle = "Bài hát đã thích";
                // Ảnh đại diện cho mục yêu thích (bạn có thể thay link ảnh khác)
                CoverImage = "https://misc.scdn.co/liked-songs/liked-songs-300.png";
                SubTitle = "Danh sách yêu thích của bạn";
                IsAlbumType = false; // Cho phép xóa (bỏ tim)
                await LoadFavoriteSongs();
            }
            // CASE 1: Received from Home Page (Album/Artist)
            else if (query.ContainsKey("AlbumData"))
            {
                _receivedAlbum = query["AlbumData"] as Album;
                if (_receivedAlbum != null)
                {
                    MainTitle = _receivedAlbum.Title;
                    CoverImage = _receivedAlbum.CoverImage;

                    // Determine if this is an Album or Artist based on Description
                    if (_receivedAlbum.Description == "Artist")
                    {
                        _currentType = "Artist";
                        SubTitle = "Nghệ sĩ";
                    }
                    else
                    {
                        _currentType = "Album";
                        SubTitle = $"Album • {_receivedAlbum.Artist}";
                    }

                    IsAlbumType = true; // Do not allow song deletion
                    await LoadSongsFromGlobal(); // Call global song filter function
                }
            }
            // CASE 2: Received from Library Page (Personal Playlist)
            else if (query.ContainsKey("Id"))
            {
                _currentId = query["Id"].ToString();
                _currentType = "Playlist"; // Default is Personal Playlist

                MainTitle = query.ContainsKey("Name") ? query["Name"].ToString() : "Playlist";
                CoverImage = query.ContainsKey("Image") ? query["Image"].ToString() : "";
                string count = query.ContainsKey("Description") ? query["Description"].ToString() : "0";
                SubTitle = $"Playlist • {count} songs";

                IsAlbumType = false; // Allow song deletion
                await LoadSongsFromPlaylist(); // Call playlist song fetch function
            }
        }

        // LOGIC 0: Fetch Favorite Songs (MỚI THÊM)
        private async Task LoadFavoriteSongs()
        {
            IsBusy = true;
            try
            {
                var favSongs = await _firestoreService.GetFavoritesAsync();
                foreach (var s in favSongs) Songs.Add(s);
                SubTitle = $"Yêu thích • {Songs.Count} bài hát";
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            finally { IsBusy = false; }
        }

        // LOGIC 1: Fetch Personal Playlist Songs
        private async Task LoadSongsFromPlaylist()
        {
            if (string.IsNullOrEmpty(_currentId)) return;
            IsBusy = true;
            try
            {
                var fetchedSongs = await _firestoreService.GetSongsFromPlaylist(_currentId);
                foreach (var s in fetchedSongs) Songs.Add(s);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            finally { IsBusy = false; }
        }

        // LOGIC 2: Filter Album/Artist Songs
        private async Task LoadSongsFromGlobal()
        {
            IsBusy = true;
            try
            {
                // Get all songs
                var allSongs = await _musicApiService.GetSongsAsync();

                IEnumerable<Song> filteredSongs;

                if (_currentType == "Artist")
                {
                    // Filter by Artist name
                    filteredSongs = allSongs.Where(s =>
                        s.Artist != null &&
                        s.Artist.Trim().Equals(MainTitle.Trim(), StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    // Filter by Album name
                    filteredSongs = allSongs.Where(s =>
                        s.Album != null &&
                        s.Album.Trim().Equals(MainTitle.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                // 👇 1. KHỞI TẠO BIẾN ĐẾM TỔNG LIKE
                int totalLikes = 0;

                foreach (var s in filteredSongs)
                {
                    Songs.Add(s);
                    // 👇 2. CỘNG DỒN SỐ LIKE CỦA TỪNG BÀI
                    totalLikes += s.LikeCount;
                }

                // 👇 3. CẬP NHẬT SUBTITLE (HIỂN THỊ 2 DÒNG)
                if (_currentType == "Album")
                {
                    // Dòng 1: Thông tin Album
                    // Dòng 2: ❤️ Tổng số like
                    SubTitle = $"Album • {_receivedAlbum.Artist} • {Songs.Count} bài\n❤️ {totalLikes} lượt thích";
                }
                else if (_currentType == "Artist")
                {
                    SubTitle = $"Nghệ sĩ • {Songs.Count} bài hát\n❤️ {totalLikes} lượt thích";
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            finally { IsBusy = false; }
        }

        // --- CONTROL FUNCTIONS ---

        [RelayCommand]
        public async Task PlaySong(Song song)
        {
            if (song == null) return;

            // 👇 VIP CHECK IN ALBUM DETAIL PAGE
            bool isCurrentVip = Preferences.Get("IsPremium", false);
            if (song.IsPremium == true && isCurrentVip == false)
            {
                bool answer = await Shell.Current.DisplayAlert("Premium Content 🔒", "Bài này trong Album VIP. Nâng cấp nhé?", "Xem gói VIP", "Để sau");
                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                return;
            }
            // 👆 END VIP CHECK

            AudioPlayer.PlaySong(song, Songs);
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }

        // 👇 PLAY ALL FUNCTION (VIP FILTER UPGRADED)
        [RelayCommand]
        public async Task PlayAll()
        {
            if (Songs == null || Songs.Count == 0) return;

            // 1. Check VIP status
            bool isVip = Preferences.Get("IsPremium", false);

            // 2. Create playable list
            var playableSongs = new ObservableCollection<Song>();

            if (isVip)
            {
                // If VIP: Play all
                foreach (var s in Songs) playableSongs.Add(s);
            }
            else
            {
                // If Free: Only get non-Premium songs
                var freeSongs = Songs.Where(s => s.IsPremium != true).ToList();
                foreach (var s in freeSongs) playableSongs.Add(s);
            }

            // 3. Handle cases
            if (playableSongs.Count == 0)
            {
                // Case 1: Album is full VIP but user is Free
                bool answer = await Shell.Current.DisplayAlert("Premium Album 👑", "Toàn bộ bài hát trong Album này dành riêng cho VIP. Nâng cấp ngay?", "Nâng cấp", "Đóng");
                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                return;
            }

            if (!isVip && playableSongs.Count < Songs.Count)
            {
                // Case 2: Mixed VIP and normal songs -> Notify user
                await Shell.Current.DisplayAlert("Lưu ý", "Bạn đang dùng tài khoản thường. Hệ thống chỉ phát các bài miễn phí trong Album này.", "OK");
            }

            // 4. Play filtered list
            AudioPlayer.IsShuffle = false;
            AudioPlayer.PlaySong(playableSongs[0], playableSongs);
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }


        [RelayCommand]
        public async Task ShuffleAll()
        {
            if (Songs == null || Songs.Count == 0) return;

            // 1. Check VIP & Filter
            bool isVip = Preferences.Get("IsPremium", false);
            var playableSongs = new ObservableCollection<Song>();

            if (isVip)
            {
                foreach (var s in Songs) playableSongs.Add(s);
            }
            else
            {
                var freeSongs = Songs.Where(s => s.IsPremium != true).ToList();
                foreach (var s in freeSongs) playableSongs.Add(s);
            }

            // 2. Handle exceptions
            if (playableSongs.Count == 0)
            {
                bool answer = await Shell.Current.DisplayAlert("Premium Album 👑", "Album này chỉ dành cho VIP. Nâng cấp nhé?", "Nâng cấp", "Đóng");
                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                return;
            }

            if (!isVip && playableSongs.Count < Songs.Count)
            {
                await Shell.Current.DisplayAlert("Lưu ý", "Chỉ phát ngẫu nhiên các bài miễn phí.", "OK");
            }

            // 3. Random Logic
            var r = new Random();
            int index = r.Next(playableSongs.Count);

            AudioPlayer.IsShuffle = true;
            AudioPlayer.PlaySong(playableSongs[index], playableSongs);
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }

        [RelayCommand] public async Task NavigateToPlayer() => await Shell.Current.GoToAsync(nameof(PlayerPage));
        [RelayCommand] public async Task GoBack() => await Shell.Current.GoToAsync("..");

        // --- MENU OPTION (DELETE/SHARE/FAVORITE) ---
        // UPDATED: Added "Add to Favorites" option here
       

        [RelayCommand]
        public async Task OpenOptionMenu(Song song)
        {
            if (song == null) return;

            string action = "";

            // 1. Xác định Menu hiển thị
            if (_currentType == "Favorites")
            {
                action = await Shell.Current.DisplayActionSheet(song.Title, "Hủy", "Bỏ Yêu thích 💔", "Chia sẻ");
            }
            else if (_currentType == "Playlist")
            {
                action = await Shell.Current.DisplayActionSheet(song.Title, "Hủy", "Xóa khỏi Playlist", "Thêm vào Yêu thích ❤️", "Chia sẻ");
            }
            else
            {
                action = await Shell.Current.DisplayActionSheet(song.Title, "Hủy", null, "Thêm vào Yêu thích ❤️", "Chia sẻ");
            }

            // 2. Xử lý các hành động
            if (action == "Xóa khỏi Playlist")
            {
                bool confirm = await Shell.Current.DisplayAlert("Xác nhận", "Xóa bài này?", "Xóa", "Hủy");
                if (confirm) await DeleteSong(song);
            }
            else if (action == "Bỏ Yêu thích 💔")
            {
                // Xóa khỏi danh sách cá nhân
                await _firestoreService.RemoveFromFavoritesAsync(song);

                // 👇 QUAN TRỌNG: Giảm lượt thích toàn cục (Global)
                song.LikeCount = Math.Max(0, song.LikeCount - 1); // Trừ đi 1
                _ = _firestoreService.UpdateGlobalLikeCount(song, -1); // Cập nhật lên Server

                // Cập nhật giao diện
                if (_currentType == "Favorites") Songs.Remove(song);
                UpdateTotalLikesSubtitle(); // Tính lại tổng số like hiển thị

                WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());
                var toast = Toast.Make("Đã xóa khỏi Yêu thích", ToastDuration.Short);
                await toast.Show();
            }
            else if (action == "Thêm vào Yêu thích ❤️")
            {
                // 👇 1. Kiểm tra trùng lặp trước
                bool isExist = await _firestoreService.IsSongInFavoritesAsync(song);

                if (isExist)
                {
                    // Nếu đã có -> Chỉ báo thông báo, KHÔNG tăng like nữa
                    var toast = Toast.Make("Bài này đã có trong tim bạn rồi! 😎", ToastDuration.Short);
                    await toast.Show();
                }
                else
                {
                    // 👇 2. Nếu chưa có -> Thêm mới và Tăng like
                    await _firestoreService.AddToFavoritesAsync(song); // Thêm vào cá nhân

                    // --- CẬP NHẬT GLOBAL LIKE ---
                    song.LikeCount++; // Tăng số like lên 1 ngay lập tức
                    _ = _firestoreService.UpdateGlobalLikeCount(song, 1); // Gửi lệnh tăng lên Server
                    UpdateTotalLikesSubtitle(); // Cập nhật dòng chữ hiển thị tổng like

                    var toast = Toast.Make("Đã thêm vào mục Yêu thích! 💚", ToastDuration.Short);
                    await toast.Show();
                }
            }
            else if (action == "Chia sẻ")
            {
                await Share.RequestAsync(new ShareTextRequest
                {
                    Title = "Chia sẻ nhạc",
                    Text = $"Đang nghe bài {song.Title} của {song.Artist} trên CosmicMusic! 🎵"
                });
            }
        }

        // 👇 Đừng quên hàm phụ trợ này (nếu bạn chưa có thì copy vào luôn nhé)
        private void UpdateTotalLikesSubtitle()
        {
            int totalLikes = 0;
            foreach (var s in Songs) totalLikes += s.LikeCount;

            if (_currentType == "Album" && _receivedAlbum != null)
            {
                SubTitle = $"Album • {_receivedAlbum.Artist} • {Songs.Count} bài\n❤️ {totalLikes} lượt thích";
            }
            else if (_currentType == "Artist")
            {
                SubTitle = $"Nghệ sĩ • {Songs.Count} bài hát\n❤️ {totalLikes} lượt thích";
            }
            else if (_currentType == "Favorites")
            {
                SubTitle = $"Yêu thích • {Songs.Count} bài hát";
            }
            else if (_currentType == "Playlist")
            {
                SubTitle = $"Playlist • {Songs.Count} songs";
            }
        }

        private async Task DeleteSong(Song song)
        {
            IsBusy = true;
            await _firestoreService.RemoveSongFromPlaylist(_currentId, song);
            Songs.Remove(song);
            SubTitle = $"Playlist • {Songs.Count} songs";
            WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());
            IsBusy = false;
        }
    }
}