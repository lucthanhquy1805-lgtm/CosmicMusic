using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;

namespace CosmicMusic.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        // 1. Khai báo các dịch vụ và biến riêng tư
        private readonly MusicApiService _musicService;
        private readonly AudioViewModel _audioViewModel;

        // 2. Danh sách bài hát hiển thị trên màn hình (Recently Played)
        public ObservableCollection<Song> Playlist { get; set; } = new();

        // 3. Biến Public để giao diện (XAML) có thể gọi tới AudioViewModel
        public AudioViewModel AudioPlayer => _audioViewModel;

        // 4. Hàm khởi tạo (Constructor)
        public HomeViewModel(MusicApiService musicService, AudioViewModel audioViewModel)
        {
            _musicService = musicService;
            _audioViewModel = audioViewModel;

            LoadSongs();
        }

        private async void LoadSongs()
        {
            try
            {
                // Gọi Service lấy tất cả bài hát
                var allSongs = await _musicService.GetSongsAsync();

                Playlist.Clear();

                // 👇 LOGIC MỚI: CHỈ LẤY 5 BÀI ĐẦU TIÊN (5 Playlist Gốc)
                // ---------------------------------------------------------
                // TODO: Sau này khi tích hợp AWS, đoạn này sẽ đổi thành gọi API:
                // var history = await _awsService.GetListeningHistory(userId);
                // ---------------------------------------------------------

                // Tạm thời lấy 5 item đầu tiên trong danh sách giả lập
                var recentSongs = allSongs.Skip(5).Take(5);

                foreach (var song in recentSongs)
                {
                    Playlist.Add(song);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tải nhạc trang Home: {ex.Message}");
            }
        }

        // --- LỆNH 1: CHỌN BÀI HÁT ---
        [RelayCommand]
        public async Task SelectSong(Song song)
        {
            if (song == null) return;

            // Đóng gói dữ liệu để gửi sang PlayerPage
            var libraryItem = new LibraryItem
            {
                Title = song.Title,
                Subtitle = song.Artist,
                CoverImage = song.CoverImage,
                Url = song.AudioUrl,
                ImageColor = "#120520"
            };

            var navigationParameter = new Dictionary<string, object>
            {
                { "SongData", libraryItem }
            };

            // Gọi AudioViewModel phát nhạc
            _audioViewModel.PlaySong(song, Playlist);

            // Chuyển trang
            await Shell.Current.GoToAsync(nameof(PlayerPage), navigationParameter);
        }

        // --- LỆNH 2: ẤN VÀO MINI PLAYER ---
        [RelayCommand]
        public async Task NavigateToPlayer()
        {
            if (_audioViewModel.CurrentSong != null)
            {
                var currentSong = _audioViewModel.CurrentSong;

                var libraryItem = new LibraryItem
                {
                    Title = currentSong.Title,
                    Subtitle = currentSong.Artist,
                    CoverImage = currentSong.CoverImage,
                    Url = currentSong.AudioUrl,
                    ImageColor = "#120520"
                };

                var navigationParameter = new Dictionary<string, object>
                {
                    { "SongData", libraryItem }
                };

                await Shell.Current.GoToAsync(nameof(PlayerPage), navigationParameter);
            }
        }

        [RelayCommand]
        public async Task NavigateToSearch()
        {
            await Shell.Current.GoToAsync(nameof(SearchPage));
        }
        // ... (Các hàm cũ giữ nguyên)

        // 👇 THÊM HÀM NÀY ĐỂ MỞ TRANG ALBUM DETAIL 👇
        [RelayCommand]
        public async Task OpenAlbum(Song song)
        {
            if (song == null) return;

            // 1. Chuyển đổi dữ liệu từ Song (Item hiển thị ở Home) sang Album (Model trang chi tiết)
            var album = new Album
            {
                Title = song.Title,
                Artist = song.Artist,
                CoverImage = song.CoverImage,
                // Giả lập mô tả hoặc lấy từ dữ liệu nếu có
                Description = $"Album by {song.Artist} • 2023"
            };

            // 2. Đóng gói dữ liệu
            var navigationParameter = new Dictionary<string, object>
            {
                { "AlbumData", album } // Khóa "AlbumData" phải khớp với [QueryProperty] bên AlbumDetailViewModel
            };

            // 3. Chuyển sang trang AlbumDetailPage
            await Shell.Current.GoToAsync(nameof(AlbumDetailPage), navigationParameter);
        }
    }
}