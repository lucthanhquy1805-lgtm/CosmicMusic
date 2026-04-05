using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using CosmicMusic.Services;
using CosmicMusic.Models;
using CosmicMusic.Views;

namespace CosmicMusic.ViewModels
{
    public partial class LibraryViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        [ObservableProperty]
        private AudioViewModel _audioPlayer;

        [ObservableProperty]
        private bool _isBusy;

        public ObservableCollection<Playlist> UserPlaylists { get; } = new();
        public ObservableCollection<Song> FavoriteSongs { get; set; } = new();

        [ObservableProperty]
        private bool _hasFavorites;

        public LibraryViewModel(FirestoreService firestoreService, AudioViewModel audioViewModel)
        {
            _firestoreService = firestoreService;
            AudioPlayer = audioViewModel;

            WeakReferenceMessenger.Default.Register<RefreshLibraryMessage>(this, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await LoadLibrary();
                });
            });
        }

       
        [RelayCommand]
        public async Task LoadLibrary()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                string uid = Preferences.Get("UserId", "");
                if (string.IsNullOrEmpty(uid))
                {
                    UserPlaylists.Clear();
                    FavoriteSongs.Clear();
                    HasFavorites = false;
                    return;
                }

          
                var playlists = await _firestoreService.GetUserPlaylists(uid);
                UserPlaylists.Clear();
                if (playlists != null && playlists.Count > 0)
                {
                    foreach (var p in playlists) UserPlaylists.Add(p);
                }

          
                var favSongs = await _firestoreService.GetFavoritesAsync();
                FavoriteSongs.Clear();
                if (favSongs != null && favSongs.Count > 0)
                {
                    foreach (var s in favSongs) FavoriteSongs.Add(s);
                }

              
                HasFavorites = FavoriteSongs.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG ERROR LOAD LIBRARY: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

       
        [RelayCommand]
        public async Task TapPlaylist(Playlist playlist)
        {
            if (playlist == null) return;

            var navParam = new Dictionary<string, object>
            {
                { "Id", playlist.Id },
                { "Type", CollectionType.Playlist },
                { "Name", playlist.Name },
                { "Image", playlist.CoverImage },
                { "Description", playlist.SongCount }
            };

            await Shell.Current.GoToAsync(nameof(AlbumDetailPage), navParam);
        }

        [RelayCommand]
        public async Task OpenPlaylistOptions(Playlist playlist)
        {
            if (playlist == null) return;
            string action = await Shell.Current.DisplayActionSheet($"Tùy chọn: {playlist.Name}", "Hủy", "Xóa Playlist", "Sửa tên");

            if (action == "Xóa Playlist")
            {
                bool confirm = await Shell.Current.DisplayAlert("Xác nhận", $"Bạn có chắc muốn xóa '{playlist.Name}' không?", "Xóa", "Hủy");
                if (confirm) await DeletePlaylist(playlist);
            }
        }

        private async Task DeletePlaylist(Playlist playlist)
        {
            IsBusy = true;
            try
            {
                await _firestoreService.DeletePlaylist(playlist.Id);
                UserPlaylists.Remove(playlist);
            }
            catch (Exception ex) { await Shell.Current.DisplayAlert("Lỗi", ex.Message, "OK"); }
            finally { IsBusy = false; }
        }

       

        [RelayCommand]
        public async Task PlayFavoriteSong(Song song)
        {
            if (song == null) return;

            bool isUserVip = Preferences.Get("IsPremium", false);
            if (song.IsPremium == true && isUserVip == false)
            {
                bool answer = await Shell.Current.DisplayAlert("Premium", "Bài hát này dành cho VIP. Nâng cấp nhé?", "Xem gói", "Để sau");
                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                return;
            }

            AudioPlayer.PlaySong(song, FavoriteSongs);
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }

        [RelayCommand]
        public async Task OpenFavoriteOptionMenu(Song song)
        {
            if (song == null) return;

            string action = await Shell.Current.DisplayActionSheet(song.Title, "Hủy", "Xóa khỏi Yêu thích 💔", "Chia sẻ");

            if (action == "Xóa khỏi Yêu thích 💔")
            {
                bool confirm = await Shell.Current.DisplayAlert("Xác nhận", "Bỏ bài này khỏi thư viện?", "Đồng ý", "Hủy");
                if (confirm)
                {
                    await _firestoreService.RemoveFromFavoritesAsync(song);
                    FavoriteSongs.Remove(song);
                    HasFavorites = FavoriteSongs.Count > 0;

                 
                    song.LikeCount = Math.Max(0, song.LikeCount - 1);
                    _ = _firestoreService.UpdateGlobalLikeCount(song, -1);
                }
            }
        }

        [RelayCommand]
        public async Task OpenFavorites()
        {
            if (!HasFavorites)
            {
                await Shell.Current.DisplayAlert("Thư viện", "Bạn chưa có bài hát yêu thích nào.", "OK");
                return;
            }

            var navParam = new Dictionary<string, object>
            {
                { "Type", "Favorites" }
            };

            await Shell.Current.GoToAsync(nameof(AlbumDetailPage), navParam);
        }

        [RelayCommand]
        public async Task NavigateToPlayer()
        {
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }

        [RelayCommand]
        public async Task OpenAddSongPage()
        {
            await Shell.Current.GoToAsync(nameof(AddSongPage));
        }
    }
}