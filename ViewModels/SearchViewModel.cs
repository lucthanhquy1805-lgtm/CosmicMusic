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
        private readonly FirestoreService _firestoreService;

        private readonly AudioViewModel _audioViewModel;
        public AudioViewModel AudioPlayer => _audioViewModel;

        [ObservableProperty]
        private string _searchText;

        // 👇 Biến này cần thiết để ActivityIndicator trong XAML hoạt động
        [ObservableProperty]
        private bool _isLoading;

        public ObservableCollection<Song> SearchResults { get; set; } = new();
        public ObservableCollection<BrowseCategory> BrowseCategories { get; set; } = new();

        [ObservableProperty]
        private bool _isExploring = true;

        private CancellationTokenSource _searchCancellationTokenSource;
        private bool _isNavigating = false;

        public SearchViewModel(AudioViewModel audioViewModel, FirestoreService firestoreService)
        {
            _audioViewModel = audioViewModel;
            _firestoreService = firestoreService;

            LoadCategories();
        }

        private void LoadCategories()
        {
            BrowseCategories.Clear();
            // 👇 CHÚ Ý: Ô Hip-Hop được gán GenreId = "genre_rap" để khớp với Firebase của bạn 👇
            BrowseCategories.Add(new BrowseCategory { Title = "Pop", GenreId = "genre_Ballad", StartColor = "#FF0055", EndColor = "#FF00CC", Icon = "🎤" });
            BrowseCategories.Add(new BrowseCategory { Title = "Rock", GenreId = "genre_rock", StartColor = "#CC2B5E", EndColor = "#753A88", Icon = "🎸" });
            BrowseCategories.Add(new BrowseCategory { Title = "Hip-Hop", GenreId = "genre_rap", StartColor = "#FF9966", EndColor = "#FF5E62", Icon = "🎧" });
            BrowseCategories.Add(new BrowseCategory { Title = "Indie", GenreId = "genre_indie", StartColor = "#00F260", EndColor = "#0575E6", Icon = "🌵" });
            BrowseCategories.Add(new BrowseCategory { Title = "R&B", GenreId = "genre_rnb", StartColor = "#4568DC", EndColor = "#B06AB3", Icon = "🎷" });
            BrowseCategories.Add(new BrowseCategory { Title = "K-Pop", GenreId = "genre_kpop", StartColor = "#834d9b", EndColor = "#d04ed6", Icon = "💃" });
            BrowseCategories.Add(new BrowseCategory { Title = "Sleep", GenreId = "genre_sleep", StartColor = "#0F2027", EndColor = "#2C5364", Icon = "🌙" });
            BrowseCategories.Add(new BrowseCategory { Title = "Gaming", GenreId = "genre_gaming", StartColor = "#11998e", EndColor = "#38ef7d", Icon = "🎮" });
        }

        // 🟢 XỬ LÝ GÕ PHÍM (DEBOUNCE)
        partial void OnSearchTextChanged(string value)
        {
            // 1. Hủy lệnh tìm cũ
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();
            var token = _searchCancellationTokenSource.Token;

            if (string.IsNullOrWhiteSpace(value))
            {
                // Nếu xóa hết chữ -> Quay về màn hình khám phá
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsExploring = true;
                    SearchResults.Clear();
                    IsLoading = false;
                });
                return;
            }

            // 2. Chuyển trạng thái UI ngay lập tức
            IsExploring = false;
            IsLoading = true; // Hiện vòng xoay ngay khi gõ

            Task.Run(async () =>
            {
                try
                {
                    // Đợi 500ms (Chống spam request)
                    await Task.Delay(500, token);

                    if (!token.IsCancellationRequested)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            await PerformSearchInternal(value);
                        });
                    }
                }
                catch (TaskCanceledException) { }
            });
        }

        // 🟢 HÀM TÌM KIẾM THỰC SỰ
        private async Task PerformSearchInternal(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return;

            IsLoading = true; // Bật Loading

            try
            {
                // Gọi API tìm kiếm
                var songs = await _firestoreService.SearchSongsByKeywordsAsync(keyword);

                SearchResults.Clear();
                if (songs != null && songs.Count > 0)
                {
                    foreach (var song in songs)
                    {
                        SearchResults.Add(song);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tìm kiếm: {ex.Message}");
            }
            finally
            {
                IsLoading = false; // Tắt Loading dù thành công hay thất bại
            }
        }

        [RelayCommand]
        public async Task PerformSearch()
        {
            await PerformSearchInternal(SearchText);
        }

        [RelayCommand]
        public async Task GoBack()
        {
            if (!IsExploring)
            {
                SearchText = string.Empty;
                IsExploring = true;
                IsLoading = false;
                SearchResults.Clear();
                return;
            }
            await Shell.Current.GoToAsync("..");
        }

      
        [RelayCommand]
        public async Task SelectSong(Song song)
        {
            if (song == null || _isNavigating) return;
            _isNavigating = true;

            bool isUserVip = Preferences.Get("IsPremium", false);
            if (song.IsPremium == true && isUserVip == false)
            {
                bool answer = await Shell.Current.DisplayAlert("Premium Content 👑", "Bài hát này dành riêng cho thành viên VIP. Nâng cấp ngay?", "Xem gói VIP", "Để sau");
                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                _isNavigating = false;
                return;
            }

            var contextList = new ObservableCollection<Song>(SearchResults);
            _audioViewModel.PlaySong(song, contextList);
            await NavigateToPlayer();

            await Task.Delay(500);
            _isNavigating = false;
        }

        [RelayCommand]
        public async Task NavigateToPlayer()
        {
            if (_audioViewModel.CurrentSong != null)
                await Shell.Current.GoToAsync(nameof(PlayerPage));
        }

        [RelayCommand]
        public async Task SelectCategory(BrowseCategory category)
        {
            if (category == null || _isNavigating) return; // Nếu đang khóa thì bỏ qua

            _isNavigating = true; // Bật khóa
            await Shell.Current.GoToAsync($"{nameof(GenreDetailPage)}?GenreName={category.Title}&GenreId={category.GenreId}");

            await Task.Delay(500); // Đợi nửa giây
            _isNavigating = false; // Mở khóa lại
        }
    }

    public class BrowseCategory
    {
        public string Title { get; set; }
        public string GenreId { get; set; }
        public string StartColor { get; set; }
        public string EndColor { get; set; }
        public string Icon { get; set; }
    }
}