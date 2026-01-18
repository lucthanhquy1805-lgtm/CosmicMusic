using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;

namespace CosmicMusic.ViewModels
{
    public partial class SearchViewModel : ObservableObject
    {
        private readonly MusicApiService _musicService;

        // AudioViewModel để điều khiển MiniPlayer
        private readonly AudioViewModel _audioViewModel;
        public AudioViewModel AudioPlayer => _audioViewModel;

        [ObservableProperty]
        private string _searchText;

        public ObservableCollection<Song> SearchResults { get; set; } = new();

        public ObservableCollection<BrowseCategory> BrowseCategories { get; set; } = new();

        [ObservableProperty]
        private bool _isExploring = true;

        public SearchViewModel(MusicApiService musicService, AudioViewModel audioViewModel)
        {
            _musicService = musicService;
            _audioViewModel = audioViewModel;

            LoadCategories();
        }

        private void LoadCategories()
        {
            BrowseCategories.Clear();
            // Bảng màu Cosmic
            BrowseCategories.Add(new BrowseCategory { Title = "Pop", StartColor = "#FF0055", EndColor = "#FF00CC", Icon = "🎤" });
            BrowseCategories.Add(new BrowseCategory { Title = "Rock", StartColor = "#CC2B5E", EndColor = "#753A88", Icon = "🎸" });
            BrowseCategories.Add(new BrowseCategory { Title = "Hip-Hop", StartColor = "#FF9966", EndColor = "#FF5E62", Icon = "🎧" });
            BrowseCategories.Add(new BrowseCategory { Title = "Indie", StartColor = "#00F260", EndColor = "#0575E6", Icon = "🌵" });
            BrowseCategories.Add(new BrowseCategory { Title = "R&B", StartColor = "#4568DC", EndColor = "#B06AB3", Icon = "🎷" });
            BrowseCategories.Add(new BrowseCategory { Title = "K-Pop", StartColor = "#834d9b", EndColor = "#d04ed6", Icon = "💃" });
            BrowseCategories.Add(new BrowseCategory { Title = "Sleep", StartColor = "#0F2027", EndColor = "#2C5364", Icon = "🌙" });
            BrowseCategories.Add(new BrowseCategory { Title = "Gaming", StartColor = "#11998e", EndColor = "#38ef7d", Icon = "🎮" });
        }

        partial void OnSearchTextChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                IsExploring = false;
                PerformSearch();
            }
            else
            {
                StartSearching();
            }
        }

        [RelayCommand]
        public async Task StartSearching()
        {
            IsExploring = false;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                var songs = await _musicService.GetSongsAsync();
                SearchResults.Clear();
                foreach (var song in songs.Take(5))
                {
                    SearchResults.Add(song);
                }
            }
        }

        [RelayCommand]
        public async Task PerformSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;

            var songs = await _musicService.GetSongsAsync();
            if (songs == null) return;

            SearchResults.Clear();

            var filtered = songs.Where(s =>
                (s.Title != null && s.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                (s.Artist != null && s.Artist.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            );

            foreach (var song in filtered)
            {
                SearchResults.Add(song);
            }
        }

        [RelayCommand]
        public async Task GoBack()
        {
            if (!IsExploring)
            {
                SearchText = string.Empty;
                IsExploring = true;
                return;
            }
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        public async Task SelectSong(Song song)
        {
            if (song == null) return;

            // Phát nhạc
            var contextList = new ObservableCollection<Song>(SearchResults);
            _audioViewModel.PlaySong(song, contextList);

            // Chuyển trang (Gọi hàm NavigateToPlayer để tái sử dụng logic)
            await NavigateToPlayer();
        }

        // 👇👇👇 ĐÂY LÀ HÀM QUAN TRỌNG VỪA ĐƯỢC THÊM 👇👇👇
        // Hàm này xử lý khi bấm vào Mini Player
        [RelayCommand]
        public async Task NavigateToPlayer()
        {
            // Nếu chưa có bài hát nào đang phát thì không làm gì cả
            if (_audioViewModel.CurrentSong == null) return;

            var currentSong = _audioViewModel.CurrentSong;

            // Đóng gói dữ liệu để gửi sang trang PlayerPage
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

            // Chuyển sang màn hình Player
            await Shell.Current.GoToAsync(nameof(PlayerPage), navigationParameter);
        }
    }

    public class BrowseCategory
    {
        public string Title { get; set; }
        public string StartColor { get; set; }
        public string EndColor { get; set; }
        public string Icon { get; set; }
    }
}