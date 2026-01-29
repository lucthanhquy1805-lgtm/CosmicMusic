using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;

namespace CosmicMusic.ViewModels
{
    public partial class AlbumDetailViewModel : ObservableObject, IQueryAttributable
    {
        private readonly FirestoreService _firestoreService;
        private readonly MusicApiService _musicApiService; // Cần thêm cái này để lấy toàn bộ nhạc

        // MiniPlayer cần cái này
        [ObservableProperty] private AudioViewModel _audioPlayer;
        [ObservableProperty] private bool _isBusy;

        // Dữ liệu hiển thị (Binding)
        [ObservableProperty] private string _coverImage;
        [ObservableProperty] private string _mainTitle;
        [ObservableProperty] private string _subTitle;
        [ObservableProperty] private bool _isAlbumType;

        // Dữ liệu nội bộ để biết đang xem cái gì
        private string _currentId;     // ID Playlist (nếu xem playlist)
        private string _currentType;   // "Playlist", "Album", hoặc "Artist"
        private Album _receivedAlbum;  // Dữ liệu Album nhận từ Home

        public ObservableCollection<Song> Songs { get; } = new();

        // Inject thêm MusicApiService
        public AlbumDetailViewModel(FirestoreService firestoreService, MusicApiService musicApiService, AudioViewModel audioPlayer)
        {
            _firestoreService = firestoreService;
            _musicApiService = musicApiService;
            AudioPlayer = audioPlayer;
        }

        // 👇 HÀM NHẬN DỮ LIỆU ĐÃ ĐƯỢC NÂNG CẤP (XỬ LÝ CẢ 2 TRƯỜNG HỢP)
        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            Songs.Clear(); // Xóa cũ trước

            // TRƯỜNG HỢP 1: Nhận từ trang Home (Album/Ca sĩ)
            if (query.ContainsKey("AlbumData"))
            {
                _receivedAlbum = query["AlbumData"] as Album;
                if (_receivedAlbum != null)
                {
                    MainTitle = _receivedAlbum.Title;
                    CoverImage = _receivedAlbum.CoverImage;

                    // Xác định xem đây là Album hay Ca sĩ dựa vào Description
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

                    IsAlbumType = true; // Không cho xóa bài
                    await LoadSongsFromGlobal(); // Gọi hàm lọc nhạc toàn cục
                }
            }
            // TRƯỜNG HỢP 2: Nhận từ trang Library (Playlist cá nhân)
            else if (query.ContainsKey("Id"))
            {
                _currentId = query["Id"].ToString();
                _currentType = "Playlist"; // Mặc định là Playlist cá nhân

                MainTitle = query.ContainsKey("Name") ? query["Name"].ToString() : "Playlist";
                CoverImage = query.ContainsKey("Image") ? query["Image"].ToString() : "";
                string count = query.ContainsKey("Description") ? query["Description"].ToString() : "0";
                SubTitle = $"Playlist • {count} songs";

                IsAlbumType = false; // Cho phép xóa bài
                await LoadSongsFromPlaylist(); // Gọi hàm lấy nhạc Playlist
            }
        }

        // LOGIC 1: Lấy nhạc Playlist cá nhân (Code cũ của bạn)
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

        // LOGIC 2: Lọc nhạc Album/Ca sĩ (Mới thêm)
        private async Task LoadSongsFromGlobal()
        {
            IsBusy = true;
            try
            {
                // Lấy tất cả nhạc
                var allSongs = await _musicApiService.GetSongsAsync();

                IEnumerable<Song> filteredSongs;

                if (_currentType == "Artist")
                {
                    // Lọc theo tên Ca sĩ
                    filteredSongs = allSongs.Where(s =>
                        s.Artist != null &&
                        s.Artist.Trim().Equals(MainTitle.Trim(), StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    // Lọc theo tên Album
                    filteredSongs = allSongs.Where(s =>
                        s.Album != null &&
                        s.Album.Trim().Equals(MainTitle.Trim(), StringComparison.OrdinalIgnoreCase));
                }

                foreach (var s in filteredSongs) Songs.Add(s);

                // Cập nhật lại Subtitle cho chính xác số bài
                if (_currentType == "Album") SubTitle = $"Album • {_receivedAlbum.Artist} • {Songs.Count} bài";
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            finally { IsBusy = false; }
        }

        // --- CÁC HÀM ĐIỀU KHIỂN ---

        [RelayCommand]
        
        public async Task PlaySong(Song song)
        {
            if (song == null) return;

            // 👇 THÊM ĐOẠN KIỂM TRA NÀY VÀO TRANG ALBUM CHI TIẾT
            bool isCurrentVip = Preferences.Get("IsPremium", false);
            if (song.IsPremium == true && isCurrentVip == false)
            {
                bool answer = await Shell.Current.DisplayAlert("Premium Content 🔒", "Bài này trong Album VIP. Nâng cấp nhé?", "Xem gói VIP", "Để sau");
                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                return;
            }
            // 👆 HẾT PHẦN KIỂM TRA

            AudioPlayer.PlaySong(song, Songs);
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }

        // 👇 HÀM PHÁT TẤT CẢ (ĐÃ NÂNG CẤP LOGIC LỌC VIP)
        [RelayCommand]
        public async Task PlayAll()
        {
            if (Songs == null || Songs.Count == 0) return;

            // 1. Kiểm tra quyền VIP
            bool isVip = Preferences.Get("IsPremium", false);

            // 2. Tạo danh sách được phép phát
            var playableSongs = new ObservableCollection<Song>();

            if (isVip)
            {
                // Nếu là VIP: Chơi tất cả
                foreach (var s in Songs) playableSongs.Add(s);
            }
            else
            {
                // Nếu là Free: Chỉ lấy bài không phải Premium
                // (IsPremium == null hoặc IsPremium == false)
                var freeSongs = Songs.Where(s => s.IsPremium != true).ToList();
                foreach (var s in freeSongs) playableSongs.Add(s);
            }

            // 3. Xử lý các trường hợp
            if (playableSongs.Count == 0)
            {
                // Trường hợp 1: Album toàn bài VIP mà user lại là Free
                bool answer = await Shell.Current.DisplayAlert("Premium Album 👑", "Toàn bộ bài hát trong Album này dành riêng cho VIP. Nâng cấp ngay?", "Nâng cấp", "Đóng");
                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                return;
            }

            if (!isVip && playableSongs.Count < Songs.Count)
            {
                // Trường hợp 2: Có lẫn lộn bài VIP và bài thường -> Báo cho user biết
                await Shell.Current.DisplayAlert("Lưu ý", "Bạn đang dùng tài khoản thường. Hệ thống chỉ phát các bài miễn phí trong Album này.", "OK");
            }

            // 4. Phát danh sách đã lọc
            AudioPlayer.IsShuffle = false;
            AudioPlayer.PlaySong(playableSongs[0], playableSongs);
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }


        [RelayCommand]
        public async Task ShuffleAll()
        {
            if (Songs == null || Songs.Count == 0) return;

            // 1. Kiểm tra quyền VIP & Lọc
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

            // 2. Xử lý ngoại lệ
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

            // 3. Logic Random
            var r = new Random();
            int index = r.Next(playableSongs.Count);

            AudioPlayer.IsShuffle = true;
            AudioPlayer.PlaySong(playableSongs[index], playableSongs);
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }

        [RelayCommand] public async Task NavigateToPlayer() => await Shell.Current.GoToAsync(nameof(PlayerPage));
        [RelayCommand] public async Task GoBack() => await Shell.Current.GoToAsync("..");

        // --- MENU OPTION (XÓA/CHIA SẺ) ---
        [RelayCommand]
        public async Task OpenOptionMenu(Song song)
        {
            if (song == null) return;

            string action = "";

            // Chỉ cho phép xóa nếu là Playlist cá nhân
            if (_currentType == "Playlist")
            {
                action = await Shell.Current.DisplayActionSheet(song.Title, "Hủy", "Xóa khỏi Playlist", "Chia sẻ");
            }
            else
            {
                // Nếu là Album thì chỉ cho Chia sẻ
                action = await Shell.Current.DisplayActionSheet(song.Title, "Hủy", null, "Chia sẻ");
            }

            if (action == "Xóa khỏi Playlist")
            {
                bool confirm = await Shell.Current.DisplayAlert("Xác nhận", "Xóa bài này?", "Xóa", "Hủy");
                if (confirm) await DeleteSong(song);
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