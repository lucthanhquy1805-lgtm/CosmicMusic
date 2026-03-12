using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;

namespace CosmicMusic.ViewModels
{
    [QueryProperty(nameof(GenreName), "GenreName")]
    [QueryProperty(nameof(GenreId), "GenreId")]
    public partial class GenreDetailViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        public AudioViewModel AudioPlayer { get; }
        private bool _isNavigating = false;

        [ObservableProperty]
        private string _genreName;

        [ObservableProperty]
        private string _genreId;

        [ObservableProperty]
        private bool _isLoading;

        public ObservableCollection<Song> GenreSongs { get; set; } = new();

        public GenreDetailViewModel(FirestoreService firestoreService, AudioViewModel audioViewModel)
        {
            _firestoreService = firestoreService;
            AudioPlayer = audioViewModel;
        }

        partial void OnGenreIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value)) LoadSongs();
        }

        private async void LoadSongs()
        {
            IsLoading = true;
            GenreSongs.Clear();

            var songs = await _firestoreService.GetSongsByGenreAsync(GenreId);
            foreach (var song in songs)
            {
                GenreSongs.Add(song);
            }

            IsLoading = false;
        }

        // ==========================================
        // 1. CHỨC NĂNG PHÁT NGAY (PLAY ALL)
        // ==========================================
        [RelayCommand]
        public async Task PlayAll()
        {
            if (GenreSongs.Count == 0) return;
            AudioPlayer.IsShuffle = false; // Tắt ngẫu nhiên
            await SelectSong(GenreSongs[0]); // Phát bài đầu tiên
        }

        // ==========================================
        // 2. CHỨC NĂNG PHÁT NGẪU NHIÊN (SHUFFLE)
        // ==========================================
        [RelayCommand]
        public async Task Shuffle()
        {
            if (GenreSongs.Count == 0) return;
            AudioPlayer.IsShuffle = true; // Bật ngẫu nhiên
            var random = new Random();
            int index = random.Next(GenreSongs.Count);
            await SelectSong(GenreSongs[index]); // Phát bài bất kỳ
        }

        // ==========================================
        // 3. SỬA LỖI CRASH KHI CHỌN BÀI HÁT
        // ==========================================
   
        [RelayCommand]
        public async Task SelectSong(Song song)
        {
            if (song == null || _isNavigating) return;
            _isNavigating = true;

            bool isUserVip = Preferences.Get("IsPremium", false);
            if (song.IsPremium == true && isUserVip == false)
            {
                bool answer = await Shell.Current.DisplayAlert("Premium Content 👑", "Nâng cấp VIP?", "Xem gói VIP", "Để sau");
                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                _isNavigating = false;
                return;
            }

            var contextList = new ObservableCollection<Song>(GenreSongs);
            AudioPlayer.PlaySong(song, contextList);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Shell.Current.GoToAsync(nameof(PlayerPage));
            });

            await Task.Delay(500);
            _isNavigating = false;
        }

        // ==========================================
        // 4. LỆNH CHO MINI PLAYER
        // ==========================================
        [RelayCommand]
        public async Task NavigateToPlayer()
        {
            if (AudioPlayer.CurrentSong != null)
                await Shell.Current.GoToAsync(nameof(PlayerPage));
        }

        // ==========================================================
        // LỆNH LÙI TRANG (ÉP RÚT TRANG VẬT LÝ)
        // ==========================================================
        [RelayCommand]
        public async Task GoBack()
        {
            if (_isNavigating) return;
            _isNavigating = true;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // Rút thẳng trang GenreDetail ra khỏi màn hình để về lại Search
                    await Shell.Current.Navigation.PopAsync();
                }
                catch
                {
                    try { await Shell.Current.GoToAsync(".."); } catch { }
                }
            });

            await Task.Delay(500);
            _isNavigating = false;
        }
    }
}