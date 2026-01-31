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

        // ==========================================================
        // 1. DANH SÁCH DỮ LIỆU
        // ==========================================================

        // Danh sách Playlist cá nhân (Giữ nguyên cũ)
        public ObservableCollection<Playlist> UserPlaylists { get; } = new();

        // 👇 MỚI: Danh sách bài hát yêu thích
        public ObservableCollection<Song> FavoriteSongs { get; set; } = new();

        // 👇 MỚI: Biến để ẩn/hiện giao diện nếu chưa có bài yêu thích nào
        [ObservableProperty]
        private bool _hasFavorites;

        public LibraryViewModel(FirestoreService firestoreService, AudioViewModel audioViewModel)
        {
            _firestoreService = firestoreService;
            AudioPlayer = audioViewModel;

            // Đăng ký nhận tin nhắn "RefreshLibraryMessage"
            WeakReferenceMessenger.Default.Register<RefreshLibraryMessage>(this, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await LoadLibrary();
                });
            });
        }

        // ==========================================================
        // 2. HÀM TẢI DỮ LIỆU (ĐÃ NÂNG CẤP)
        // ==========================================================
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

                // --- PHẦN 1: Tải Playlist (Code cũ của bạn) ---
                var playlists = await _firestoreService.GetUserPlaylists(uid);
                UserPlaylists.Clear();
                if (playlists.Count > 0)
                {
                    foreach (var p in playlists) UserPlaylists.Add(p);
                }

                // --- 👇 PHẦN 2: Tải Danh sách Yêu thích (MỚI) ---
                var favSongs = await _firestoreService.GetFavoritesAsync();
                FavoriteSongs.Clear();
                foreach (var s in favSongs)
                {
                    FavoriteSongs.Add(s);
                }

                // Cập nhật trạng thái hiển thị
                HasFavorites = FavoriteSongs.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG ERROR: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ==========================================================
        // 3. XỬ LÝ PLAYLIST (GIỮ NGUYÊN CODE CŨ)
        // ==========================================================
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

        // ==========================================================
        // 4. XỬ LÝ BÀI HÁT YÊU THÍCH (MỚI THÊM)
        // ==========================================================

        // 👇 Hàm phát nhạc khi chọn bài trong mục Yêu thích
        [RelayCommand]
        public async Task PlayFavoriteSong(Song song)
        {
            if (song == null) return;

            // Kiểm tra VIP
            bool isUserVip = Preferences.Get("IsPremium", false);
            if (song.IsPremium == true && isUserVip == false)
            {
                bool answer = await Shell.Current.DisplayAlert("Premium", "Bài hát này dành cho VIP. Nâng cấp nhé?", "Xem gói", "Để sau");
                if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                return;
            }

            // Phát nhạc với danh sách phát là FavoriteSongs
            AudioPlayer.PlaySong(song, FavoriteSongs);
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }

        // 👇 Hàm mở menu tùy chọn cho bài yêu thích (Xóa khỏi tim)
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
                    FavoriteSongs.Remove(song); // Xóa ngay trên giao diện
                    HasFavorites = FavoriteSongs.Count > 0;
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

            // Chuyển sang AlbumDetailPage với Type = Favorites
            var navParam = new Dictionary<string, object>
            {
                { "Type", "Favorites" } // Từ khóa để bên kia nhận diện
            };

            await Shell.Current.GoToAsync(nameof(AlbumDetailPage), navParam);
        }

        [RelayCommand]
        public async Task NavigateToPlayer()
        {
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }
    }
}