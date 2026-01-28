using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Models;
using CosmicMusic.Services;
using System.Collections.ObjectModel;

namespace CosmicMusic.ViewModels
{
    public partial class AlbumDetailViewModel : ObservableObject, IQueryAttributable
    {
        private readonly FirestoreService _firestoreService;

        // MiniPlayer cần cái này
        [ObservableProperty] private AudioViewModel _audioPlayer;
        [ObservableProperty] private bool _isBusy;

        // Dữ liệu hiển thị lên màn hình (Binding)
        [ObservableProperty] private string _coverImage;  // Ảnh to đùng
        [ObservableProperty] private string _mainTitle;   // Tên (VD: Cosmic Dream)
        [ObservableProperty] private string _subTitle;    // Dòng dưới (VD: Album by Orion • 2023)
        [ObservableProperty] private bool _isAlbumType;   // Để hiện nút Download/Add nếu cần

        // Dữ liệu nội bộ
        private string _currentId;
        private string _currentType;

        public ObservableCollection<Song> Songs { get; } = new();

        public AlbumDetailViewModel(FirestoreService firestoreService, AudioViewModel audioPlayer)
        {
            _firestoreService = firestoreService;
            AudioPlayer = audioPlayer;
        }

        // 👇 HÀM NHẬN DỮ LIỆU TỪ TRANG KHÁC GỬI SANG
        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            // 1. Nhận thông tin cơ bản
            if (query.ContainsKey("Name")) MainTitle = query["Name"].ToString();
            if (query.ContainsKey("Image")) CoverImage = query["Image"].ToString();

            // 2. Nhận ID và Loại để biết xử lý
            _currentId = query.ContainsKey("Id") ? query["Id"].ToString() : "";
            _currentType = query.ContainsKey("Type") ? query["Type"].ToString() : CollectionType.Playlist;

            // 3. Xử lý dòng phụ đề (Subtitle) cho chuyên nghiệp
            if (_currentType == CollectionType.Playlist)
            {
                // Nếu là Playlist thì hiện số bài
                string count = query.ContainsKey("Description") ? query["Description"].ToString() : "0 songs";
                SubTitle = $"Playlist • {count}";
                IsAlbumType = false;
            }
            else if (_currentType == CollectionType.Album)
            {
                // Nếu là Album thì hiện: Album by [Artist] • [Year]
                string artist = query.ContainsKey("Artist") ? query["Artist"].ToString() : "Unknown";
                string year = query.ContainsKey("Year") ? query["Year"].ToString() : "2023";
                SubTitle = $"Album by {artist} • {year}";
                IsAlbumType = true;
            }

            // 4. Tải nhạc
            await LoadSongs();
        }

        public async Task LoadSongs()
        {
            if (string.IsNullOrEmpty(_currentId)) return;
            IsBusy = true;
            Songs.Clear();
            List<Song> fetchedSongs = new();

            try
            {
                if (_currentType == CollectionType.Playlist)
                {
                    fetchedSongs = await _firestoreService.GetSongsFromPlaylist(_currentId);
                }
                // Sau này thêm logic lấy Album ở đây...

                foreach (var s in fetchedSongs) Songs.Add(s);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public void PlaySong(Song song)
        {
            AudioPlayer.PlaySong(song, Songs);
        }

        [RelayCommand]
        public async Task NavigateToPlayer() => await Shell.Current.GoToAsync("PlayerPage");

        [RelayCommand]
        public async Task GoBack() => await Shell.Current.GoToAsync("..");
        // ... (Các hàm cũ giữ nguyên)

        // 👇 THÊM HÀM NÀY ĐỂ NÚT "PLAY" HOẠT ĐỘNG
        [RelayCommand]
        public void PlayAll()
        {
            if (Songs.Count > 0)
            {
                // Phát bài đầu tiên và nạp cả danh sách vào hàng đợi
                AudioPlayer.PlaySong(Songs[0], Songs);
            }
        }

        // (Tùy chọn) Thêm luôn hàm Shuffle để dùng cho nút bên cạnh
        [RelayCommand]
        public void ShuffleAll()
        {
            if (Songs.Count > 0)
            {
                // Bật chế độ Shuffle trước
                AudioPlayer.IsShuffle = true;

                // Chọn ngẫu nhiên 1 bài để phát
                var r = new Random();
                var randomSong = Songs[r.Next(Songs.Count)];

                AudioPlayer.PlaySong(randomSong, Songs);
            }
        }
        // 👇 HÀM XỬ LÝ KHI BẤM VÀO 3 CHẤM 👇
        [RelayCommand]
        public async Task OpenOptionMenu(Song song)
        {
            if (song == null) return;

            // 1. Hiện Menu từ dưới lên (Giống Spotify)
            string action = "";

            // Chỉ hiện nút "Xóa" nếu đây là Playlist (Album thì không cho xóa nhạc gốc)
            if (_currentType == CollectionType.Playlist)
            {
                action = await Shell.Current.DisplayActionSheet(song.Title, "Hủy", "Xóa khỏi Playlist", "Chia sẻ", "Xem nghệ sĩ");
            }
            else
            {
                action = await Shell.Current.DisplayActionSheet(song.Title, "Hủy", null, "Chia sẻ", "Xem nghệ sĩ");
            }

            // 2. Xử lý các hành động
            if (action == "Chia sẻ")
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Text = $"Đang nghe {song.Title} - {song.Artist} trên Cosmic Music!",
                    Title = "Chia sẻ bài hát"
                });
            }
            else if (action == "Xóa khỏi Playlist")
            {
                bool confirm = await Shell.Current.DisplayAlert("Xác nhận", "Bạn có chắc muốn xóa bài này?", "Xóa", "Hủy");
                if (confirm)
                {
                    await DeleteSong(song);
                }
            }
        }

        private async Task DeleteSong(Song song)
        {
            IsBusy = true;

            // 1. Xóa trên Server (Firestore)
            await _firestoreService.RemoveSongFromPlaylist(_currentId, song);

            // 2. Xóa trên Giao diện (App) ngay lập tức
            Songs.Remove(song);

            // 3. Cập nhật lại dòng phụ đề (Số lượng bài hát)
            if (_currentType == CollectionType.Playlist)
            {
                SubTitle = $"Playlist • {Songs.Count} songs";
            }

            // 4. (Quan trọng) Gửi tin nhắn để trang Library bên ngoài cũng cập nhật lại số lượng
            // Dùng Messenger để báo: "Hey Library, reload đi!"
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());

            IsBusy = false;
        }
    }
}
    
