using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // 👈 1. QUAN TRỌNG: Thêm thư viện này
using System.Collections.ObjectModel;
using CosmicMusic.Services;
using CosmicMusic.Models;
using CosmicMusic.Views;

namespace CosmicMusic.ViewModels
{
    public partial class LibraryViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        // Biến này để MiniPlayer Binding dữ liệu (Tên bài, Ảnh, Play/Pause...)
        [ObservableProperty]
        private AudioViewModel _audioPlayer;

        // Danh sách Playlist hiển thị lên màn hình
        public ObservableCollection<Playlist> UserPlaylists { get; } = new();

        [ObservableProperty]
        private bool _isBusy;

        public LibraryViewModel(FirestoreService firestoreService, AudioViewModel audioViewModel)
        {
            _firestoreService = firestoreService;
            AudioPlayer = audioViewModel; // Lưu lại AudioViewModel để dùng cho MiniPlayer

            // 👇 2. SỬA QUAN TRỌNG: Đăng ký nhận tin nhắn "RefreshLibraryMessage"
            // Khi AudioViewModel gửi tin nhắn, hàm này sẽ bắt được và tải lại danh sách
            WeakReferenceMessenger.Default.Register<RefreshLibraryMessage>(this, (r, m) =>
            {
                // Chạy trên luồng chính để đảm bảo an toàn cho giao diện
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
                    System.Diagnostics.Debug.WriteLine("DEBUG: Chưa đăng nhập hoặc không tìm thấy UserID.");
                    UserPlaylists.Clear();
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"DEBUG: Đang tải Library của User ID: {uid}");

                var playlists = await _firestoreService.GetUserPlaylists(uid);

                UserPlaylists.Clear();
                if (playlists.Count > 0)
                {
                    foreach (var p in playlists)
                    {
                        UserPlaylists.Add(p);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"DEBUG: User {uid} chưa có playlist nào.");
                }
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
        [RelayCommand]
        public async Task TapPlaylist(Playlist playlist)
        {
            if (playlist == null) return;

            var navParam = new Dictionary<string, object>
            {
                { "Id", playlist.Id },
                { "Type", CollectionType.Playlist },
                
                // Gửi dữ liệu hiển thị
                { "Name", playlist.Name },
                { "Image", playlist.CoverImage },
                { "Description", playlist.SongCount } // Gửi số bài hát
            };

            await Shell.Current.GoToAsync(nameof(AlbumDetailPage), navParam);
        }
        // 👇 HÀM XỬ LÝ MENU TÙY CHỌN CHO PLAYLIST
        [RelayCommand]
        public async Task OpenPlaylistOptions(Playlist playlist)
        {
            if (playlist == null) return;

            // 1. Hiện bảng chọn
            string action = await Shell.Current.DisplayActionSheet($"Tùy chọn: {playlist.Name}", "Hủy", "Xóa Playlist", "Sửa tên");

            // 2. Xử lý xóa
            if (action == "Xóa Playlist")
            {
                bool confirm = await Shell.Current.DisplayAlert("Xác nhận", $"Bạn có chắc muốn xóa vĩnh viễn '{playlist.Name}' không?", "Xóa", "Hủy");
                if (confirm)
                {
                    await DeletePlaylist(playlist);
                }
            }
            else if (action == "Sửa tên")
            {
                // (Optional) Bạn có thể làm thêm tính năng sửa tên ở đây sau này
            }
        }

        private async Task DeletePlaylist(Playlist playlist)
        {
            IsBusy = true;
            try
            {
                // 1. Xóa trên Server
                await _firestoreService.DeletePlaylist(playlist.Id);

                // 2. Xóa ngay lập tức trên giao diện (Không cần load lại)
                UserPlaylists.Remove(playlist);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Lỗi", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }


        [RelayCommand]
        public async Task NavigateToPlayer()
        {
            await Shell.Current.GoToAsync(nameof(PlayerPage));
        }
    }
}