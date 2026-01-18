using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;
using System.Collections.ObjectModel;
using CosmicMusic.Views;

namespace CosmicMusic.ViewModels
{
    [QueryProperty(nameof(AlbumData), "AlbumData")]
    public partial class AlbumDetailViewModel : ObservableObject
    {
        private readonly AudioViewModel _audioViewModel;

        [ObservableProperty]
        private Album _albumData;

        public ObservableCollection<Song> AlbumSongs { get; set; } = new();

        public AudioViewModel AudioPlayer => _audioViewModel;

        public AlbumDetailViewModel(AudioViewModel audioViewModel)
        {
            _audioViewModel = audioViewModel;
        }

        partial void OnAlbumDataChanged(Album value)
        {
            if (value != null)
            {
                LoadAlbumSongs(value);
            }
        }

        private async void LoadAlbumSongs(Album album)
        {
            AlbumSongs.Clear();
            // ... (Code giả lập dữ liệu giữ nguyên như cũ)
            await Task.Delay(100);
            AlbumSongs.Add(new Song { Title = "Stardust Echoes", Artist = album.Artist, Duration = 225, AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-1.mp3", CoverImage = album.CoverImage });
            AlbumSongs.Add(new Song { Title = "Lunar Tides", Artist = album.Artist, Duration = 252, AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-2.mp3", CoverImage = album.CoverImage });
            AlbumSongs.Add(new Song { Title = "Meteor Shower", Artist = album.Artist, Duration = 178, AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-3.mp3", CoverImage = album.CoverImage });
            AlbumSongs.Add(new Song { Title = "Gravity's Pull", Artist = album.Artist, Duration = 210, AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-4.mp3", CoverImage = album.CoverImage });
            AlbumSongs.Add(new Song { Title = "Void Walker", Artist = album.Artist, Duration = 301, AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-5.mp3", CoverImage = album.CoverImage });
        }

        [RelayCommand]
        public async Task GoBack()
        {
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        public void PlayAlbum()
        {
            if (AlbumSongs.Count > 0)
            {
                _audioViewModel.PlaySong(AlbumSongs[0], AlbumSongs);
            }
        }

        [RelayCommand]
        public void ShuffleAlbum()
        {
            if (AlbumSongs.Count > 0)
            {
                _audioViewModel.IsShuffle = true;
                _audioViewModel.PlaySong(AlbumSongs[0], AlbumSongs);
            }
        }

        [RelayCommand]
        public async Task PlaySong(Song song)
        {
            _audioViewModel.PlaySong(song, AlbumSongs);
            await NavigateToPlayer(); // Gọi hàm chung bên dưới để đỡ lặp code
        }

        // 👇👇👇 THÊM HÀM MỚI NÀY ĐỂ SỬA LỖI MINI PLAYER 👇👇👇
        [RelayCommand]
        public async Task NavigateToPlayer()
        {
            // Nếu không có bài nào đang hát thì không làm gì cả
            if (_audioViewModel.CurrentSong == null) return;

            // Lấy bài đang hát hiện tại
            var currentSong = _audioViewModel.CurrentSong;

            // Đóng gói dữ liệu để gửi sang trang Player
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

            // Chuyển trang
            await Shell.Current.GoToAsync(nameof(PlayerPage), navigationParameter);
        }
    }
}