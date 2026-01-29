using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives; // Cần thiết cho MediaElement events
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Models;
using CosmicMusic.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace CosmicMusic.ViewModels
{
    // Class tin nhắn để báo Library cập nhật lại
    public class RefreshLibraryMessage { }

    public partial class AudioViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private MediaElement _mediaElement;
        private bool _isDraggingSlider = false;

        // Danh sách phát toàn cục
        public ObservableCollection<Song> Playlist { get; set; } = new();

        public AudioViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            FavoriteColor = "#A569F7"; // Màu mặc định (Tím)
            IsFavorite = false;
        }

        // ==========================================================
        // 1. CÁC THUỘC TÍNH (OBSERVABLE PROPERTIES)
        // ==========================================================

        [ObservableProperty] private Song _currentSong;
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalDurationText))]
        private TimeSpan _duration;
        public string TotalDurationText => $"{Duration:mm\\:ss}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentPositionText))]
        private TimeSpan _currentPosition;
        public string CurrentPositionText => $"{CurrentPosition:mm\\:ss}";
        [ObservableProperty] private double _volume = 1.0;

        // Thuộc tính màu tim (Quan trọng để Binding)
        [ObservableProperty] private bool _isFavorite;
        [ObservableProperty] private string _favoriteColor;

        // Biến hỗ trợ Slider (Giây lẻ)
        public double CurrentPositionSeconds
        {
            get => CurrentPosition.TotalSeconds;
            set { if (_isDraggingSlider) CurrentPosition = TimeSpan.FromSeconds(value); }
        }

        // Shuffle & Repeat
        [ObservableProperty][NotifyPropertyChangedFor(nameof(ShuffleColor))] private bool _isShuffle;
        public string ShuffleColor => IsShuffle ? "#FF00CC" : "#FFFFFF"; // Hồng khi bật, Trắng khi tắt

        [ObservableProperty][NotifyPropertyChangedFor(nameof(RepeatColor))][NotifyPropertyChangedFor(nameof(RepeatIcon))] private int _repeatMode;
        public string RepeatColor => RepeatMode == 0 ? "#FFFFFF" : "#FF00CC";
        public string RepeatIcon => RepeatMode == 1 ? "🔂" : "🔁"; // 1: Lặp 1 bài, 2: Lặp cả list

        
        // ==========================================================
        // 1. KẾT NỐI MEDIA ELEMENT (ĐÃ SỬA LỖI NGẮT NHẠC)
        // ==========================================================
        public void SetMediaElement(MediaElement newMediaElement)
        {
            // Nếu MediaElement không thay đổi thì không làm gì cả
            if (_mediaElement == newMediaElement) return;

            // 1. Gỡ bỏ sự kiện ở cái cũ (Nhưng KHÔNG ĐƯỢC STOP nhạc)
            if (_mediaElement != null)
            {
                try
                {
                    _mediaElement.MediaOpened -= OnMediaOpened;
                    _mediaElement.PositionChanged -= OnPositionChanged;
                    _mediaElement.MediaEnded -= OnMediaEnded;

                    // ❌ ĐÃ XÓA DÒNG NÀY: _mediaElement.Stop(); 
                    // ❌ ĐÃ XÓA DÒNG NÀY: _mediaElement.Source = null;
                    // Để nhạc vẫn chạy trong tích tắc chuyển giao diện
                }
                catch { }
            }

            // 2. Gán cái mới (Ví dụ: MiniPlayer ở Home)
            _mediaElement = newMediaElement;

            if (_mediaElement != null)
            {
                _mediaElement.MediaOpened += OnMediaOpened;
                _mediaElement.PositionChanged += OnPositionChanged;
                _mediaElement.MediaEnded += OnMediaEnded;

                // 3. ĐỒNG BỘ TRẠNG THÁI NGAY LẬP TỨC (HAND-OFF)
                // Nếu đang có bài hát, nạp ngay vào cái mới để nó tiếp tục phát
                if (CurrentSong != null)
                {
                    // Nạp Source
                    _mediaElement.Source = MediaSource.FromUri(CurrentSong.AudioUrl);

                    // Tua tới đúng vị trí hiện tại (để không bị nghe lại từ đầu)
                    if (CurrentPosition.TotalSeconds > 0)
                    {
                        _mediaElement.SeekTo(CurrentPosition);
                    }

                    // Nếu đang hát dở thì lệnh cho cái mới hát tiếp luôn
                    if (IsPlaying)
                    {
                        _mediaElement.Play();
                    }
                }
            }
        }

        // ==========================================================
        // 3. SỰ KIỆN MEDIA (TỰ ĐỘNG CHẠY)
        // ==========================================================

        private void OnMediaOpened(object sender, EventArgs e)
        {
            if (_mediaElement != null)
            {
                // Khi tải xong, cập nhật lại Duration thật chính xác
                Duration = _mediaElement.Duration;
            }
        }

        private void OnPositionChanged(object sender, MediaPositionChangedEventArgs e)
        {
            if (_isDraggingSlider) return; // Đang kéo tay thì đừng cập nhật tự động

            CurrentPosition = e.Position;
            OnPropertyChanged(nameof(CurrentPositionSeconds)); // Báo cho Slider chạy
            if (_mediaElement != null &&
                _mediaElement.Duration > TimeSpan.Zero &&
                Duration != _mediaElement.Duration)
            {
                Duration = _mediaElement.Duration;
            }
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            // Hết bài -> Tự qua bài mới
            Next();
        }

        // ==========================================================
        // 4. HÀM PHÁT NHẠC (TRÁI TIM CỦA APP)
        // ==========================================================

        public void PlaySong(Song song, ObservableCollection<Song>? contextList = null)
        {
            if (song == null) return;

            // A. Kiểm tra nếu bài này đang hát rồi thì thôi (trừ khi đang Pause)
            bool isSameSong = (CurrentSong != null && CurrentSong.Title == song.Title);
            if (isSameSong && IsPlaying) return;

            // B. Cập nhật Playlist nếu có danh sách mới gửi sang
            if (contextList != null && contextList.Count > 0)
            {
                bool needUpdate = Playlist.Count != contextList.Count;
                if (!needUpdate && Playlist.Count > 0 && Playlist[0].Title != contextList[0].Title) needUpdate = true;

                if (needUpdate) { Playlist.Clear(); foreach (var item in contextList) Playlist.Add(item); }
            }
            else if (!Playlist.Contains(song)) { Playlist.Add(song); }

            // ======================================================
            // C. RESET TRẠNG THÁI NGAY LẬP TỨC (CHỐNG LAG)
            // ======================================================

            // 1. Reset thanh thời gian về 0 ngay
            CurrentPosition = TimeSpan.Zero;
            CurrentPositionSeconds = 0;

            // 2. Nếu Database có lưu Duration thì lấy hiển thị ngay (khỏi chờ tải)
            if (song.Duration > 0) Duration = TimeSpan.FromSeconds(song.Duration);
            else Duration = TimeSpan.Zero;

            // 3. Reset Tim về màu mặc định trước
            IsFavorite = false;
            FavoriteColor = "#A569F7";

            // 4. Cập nhật Bài hát hiện tại
            CurrentSong = song;

            // ======================================================

            // D. Ra lệnh cho MediaElement phát nhạc
            if (_mediaElement != null)
            {
                IsPlaying = true;
                if (!isSameSong)
                {
                    _mediaElement.Source = MediaSource.FromUri(song.AudioUrl);
                }
                _mediaElement.Play();
            }

            // E. Kiểm tra Tim trên Server (Chạy ngầm để không đơ ứng dụng)
            Task.Run(async () =>
            {
                try
                {
                    string uid = Preferences.Get("UserId", "");
                    if (!string.IsNullOrEmpty(uid))
                    {
                        bool isLiked = await _firestoreService.IsSongInUserLibrary(uid, song);

                        if (isLiked)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                // Check lại lần nữa xem có đúng bài đang hát không
                                if (CurrentSong != null && CurrentSong.Title == song.Title)
                                {
                                    IsFavorite = true;
                                    FavoriteColor = "Red";
                                }
                            });
                        }
                    }
                }
                catch { /* Bỏ qua lỗi mạng khi check tim */ }
            });
        }

        // ==========================================================
        // 5. CÁC NÚT ĐIỀU KHIỂN (PLAY, NEXT, PREV, TIM)
        // ==========================================================

        [RelayCommand]
        public void PlayPause()
        {
            if (_mediaElement == null) return;
            if (IsPlaying) { _mediaElement.Pause(); IsPlaying = false; }
            else { _mediaElement.Play(); IsPlaying = true; }
        }

        [RelayCommand] public void DragStarted() => _isDraggingSlider = true;

        [RelayCommand]
        public async Task DragCompleted()
        {
            if (_mediaElement != null) await _mediaElement.SeekTo(CurrentPosition);
            await Task.Delay(100);
            _isDraggingSlider = false;
        }

        [RelayCommand]
        public void ToggleShuffle() => IsShuffle = !IsShuffle;

        [RelayCommand]
        public void ToggleRepeat() => RepeatMode = (RepeatMode + 1) % 3;

        [RelayCommand]
        public async Task GoBack() => await Shell.Current.GoToAsync("..");

        [RelayCommand]
        public void Next()
        {
            if (CurrentSong == null || Playlist.Count == 0) return;

            // Nếu Repeat 1 bài -> Tua lại từ đầu
            if (RepeatMode == 1 && _mediaElement != null)
            {
                _mediaElement.SeekTo(TimeSpan.Zero);
                _mediaElement.Play();
                return;
            }

            // Tìm vị trí bài hiện tại
            int index = -1;
            for (int i = 0; i < Playlist.Count; i++)
            {
                if (Playlist[i].Title == CurrentSong.Title && Playlist[i].Artist == CurrentSong.Artist)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1) { PlaySong(Playlist[0]); return; }

            // Chế độ Shuffle
            if (IsShuffle && Playlist.Count > 1)
            {
                var r = new Random();
                int nextIndex;
                do { nextIndex = r.Next(Playlist.Count); } while (nextIndex == index);
                PlaySong(Playlist[nextIndex]);
                return;
            }

            // Chế độ Next thường
            if (index < Playlist.Count - 1)
            {
                PlaySong(Playlist[index + 1]);
            }
            else
            {
                // Hết danh sách -> Quay lại đầu (Loop All)
                PlaySong(Playlist[0]);
            }
        }

        [RelayCommand]
        public void Previous()
        {
            if (CurrentSong == null || Playlist.Count == 0) return;

            // Nếu nghe được > 3s thì replay lại bài này
            if (CurrentPosition.TotalSeconds > 3 && _mediaElement != null)
            {
                _mediaElement.SeekTo(TimeSpan.Zero);
                return;
            }

            int index = -1;
            for (int i = 0; i < Playlist.Count; i++)
            {
                if (Playlist[i].Title == CurrentSong.Title && Playlist[i].Artist == CurrentSong.Artist)
                {
                    index = i;
                    break;
                }
            }

            if (index > 0) PlaySong(Playlist[index - 1]);
            else PlaySong(Playlist[Playlist.Count - 1]); // Về bài cuối cùng
        }

        // --- XỬ LÝ THẢ TIM VÀ THÊM VÀO PLAYLIST ---
        [RelayCommand]
        public async Task ToggleFavorite()
        {
            if (CurrentSong == null) return;
            string uid = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(uid)) { await Shell.Current.DisplayAlert("Yêu cầu", "Vui lòng đăng nhập!", "OK"); return; }

            if (IsFavorite) { await Shell.Current.DisplayAlert("Thông báo", "Đã có trong thư viện.", "OK"); return; }

            string action = await Shell.Current.DisplayActionSheet("Thêm vào thư viện", "Hủy", null, "Thêm vào Playlist có sẵn", "Tạo Playlist mới");

            bool addedSuccess = false;

            if (action == "Tạo Playlist mới")
            {
                string name = await Shell.Current.DisplayPromptAsync("Tạo Mới", "Tên Playlist:");
                if (!string.IsNullOrEmpty(name))
                {
                    await _firestoreService.CreatePlaylistAndAddSong(uid, name, CurrentSong);
                    addedSuccess = true;
                    await Shell.Current.DisplayAlert("Thành công", $"Đã tạo '{name}'", "OK");
                }
            }
            else if (action == "Thêm vào Playlist có sẵn")
            {
                var playlists = await _firestoreService.GetUserPlaylists(uid);
                if (playlists != null && playlists.Count > 0)
                {
                    var names = playlists.Select(p => p.Name).ToArray();
                    string sel = await Shell.Current.DisplayActionSheet("Chọn Playlist", "Hủy", null, names);
                    if (!string.IsNullOrEmpty(sel) && sel != "Hủy")
                    {
                        var p = playlists.FirstOrDefault(x => x.Name == sel);
                        if (p != null)
                        {
                            await _firestoreService.AddSongToExistingPlaylist(p.Id, CurrentSong);
                            addedSuccess = true;
                            await Shell.Current.DisplayAlert("Thành công", $"Đã thêm vào '{sel}'", "OK");
                        }
                    }
                }
                else await Shell.Current.DisplayAlert("Thông báo", "Chưa có playlist nào.", "OK");
            }

            if (addedSuccess)
            {
                IsFavorite = true;
                FavoriteColor = "Red";
                // Gửi tin nhắn để màn hình Library tự reload
                WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());
            }
        }
    }
}