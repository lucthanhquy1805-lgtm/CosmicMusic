using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // Required for WeakReferenceMessenger
using CosmicMusic.Models;
using CosmicMusic.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace CosmicMusic.ViewModels
{
    // Define a simple message class for the messenger
    public class RefreshLibraryMessage { }

    public partial class AudioViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private MediaElement _mediaElement;
        private bool _isDraggingSlider = false;

        public ObservableCollection<Song> Playlist { get; set; } = new();

        public AudioViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            FavoriteColor = "#A569F7";
            IsFavorite = false;
        }

        // ==========================================================
        // 1. CONNECT MEDIA ELEMENT
        // ==========================================================
        public void SetMediaElement(MediaElement newMediaElement)
        {
            if (_mediaElement != null && _mediaElement != newMediaElement)
            {
                try
                {
                    _mediaElement.Stop();
                    _mediaElement.Source = null;
                    _mediaElement.MediaOpened -= OnMediaOpened;
                    _mediaElement.PositionChanged -= OnPositionChanged;
                    _mediaElement.MediaEnded -= OnMediaEnded;
                }
                catch { }
            }

            _mediaElement = newMediaElement;

            if (_mediaElement != null)
            {
                _mediaElement.MediaOpened += OnMediaOpened;
                _mediaElement.PositionChanged += OnPositionChanged;
                _mediaElement.MediaEnded += OnMediaEnded;

                if (CurrentSong != null && IsPlaying)
                {
                    if (_mediaElement.Source == null)
                        _mediaElement.Source = MediaSource.FromUri(CurrentSong.AudioUrl);
                }
            }
        }

        [ObservableProperty] private Song _currentSong;
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty] private TimeSpan _duration;
        [ObservableProperty] private TimeSpan _currentPosition;
        [ObservableProperty] private double _volume = 1.0;

        public double CurrentPositionSeconds
        {
            get => CurrentPosition.TotalSeconds;
            set { if (_isDraggingSlider) CurrentPosition = TimeSpan.FromSeconds(value); }
        }

        // ==========================================================
        // 2. PLAYBACK EVENTS
        // ==========================================================

        private void OnMediaOpened(object sender, EventArgs e)
        {
            if (_mediaElement != null)
            {
                Duration = _mediaElement.Duration;
                if (IsPlaying)
                {
                    MainThread.BeginInvokeOnMainThread(() => { _mediaElement.Play(); });
                }
            }
        }

        private void OnPositionChanged(object sender, CommunityToolkit.Maui.Core.Primitives.MediaPositionChangedEventArgs e)
        {
            if (_isDraggingSlider) return;
            CurrentPosition = e.Position;
            OnPropertyChanged(nameof(CurrentPositionSeconds));
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            IsPlaying = false;
            Next();
        }

        // ==========================================================
        // 3. PLAY SONG FUNCTION (WITH HEART CHECK LOGIC)
        // ==========================================================

        // 👇 HÀM PlaySong ĐÃ ĐƯỢC TỐI ƯU TỐC ĐỘ (INSTANT PLAY) 👇
        public void PlaySong(Song song, ObservableCollection<Song>? contextList = null)
        {
            if (song == null) return;

            // 1. Logic Playlist (Xử lý ngay lập tức)
            bool isSameSong = (CurrentSong != null && CurrentSong.Title == song.Title);
            if (isSameSong && IsPlaying) return;

            if (contextList != null && contextList.Count > 0)
            {
                bool needUpdate = Playlist.Count != contextList.Count;
                if (!needUpdate && Playlist.Count > 0 && Playlist[0].Title != contextList[0].Title) needUpdate = true;
                if (needUpdate) { Playlist.Clear(); foreach (var item in contextList) Playlist.Add(item); }
            }
            else if (!Playlist.Contains(song)) { Playlist.Add(song); }

            // 2. CẬP NHẬT GIAO DIỆN NGAY LẬP TỨC (Ảnh & Chữ)
            CurrentSong = song;

            // Reset tim về màu tím tạm thời để người dùng thấy phản hồi ngay
            IsFavorite = false;
            FavoriteColor = "#A569F7";

            // 3. PHÁT NHẠC NGAY LẬP TỨC (Không chờ mạng)
            if (_mediaElement != null)
            {
                IsPlaying = true;
                if (!isSameSong)
                {
                    _mediaElement.Source = MediaSource.FromUri(song.AudioUrl);
                }
                _mediaElement.Play();
            }

            // 4. KIỂM TRA TIM (CHẠY NGẦM - KHÔNG GÂY LAG)
            // Dùng Task.Run để đẩy việc này sang luồng phụ, không chặn giao diện
            Task.Run(async () =>
            {
                string uid = Preferences.Get("UserId", "");
                if (!string.IsNullOrEmpty(uid))
                {
                    bool isLiked = await _firestoreService.IsSongInUserLibrary(uid, song);

                    // Nếu đã thích, cập nhật lại giao diện (cần gọi MainThread vì đang ở luồng phụ)
                    if (isLiked)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            // Chỉ cập nhật nếu bài hát vẫn là bài đang phát (tránh trường hợp user bấm next quá nhanh)
                            if (CurrentSong.Title == song.Title)
                            {
                                IsFavorite = true;
                                FavoriteColor = "Red";
                            }
                        });
                    }
                }
            });
        }
        // ==========================================================
        // 4. CONTROLS
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

        [ObservableProperty] private bool _isFavorite;
        [ObservableProperty] private string _favoriteColor;

        // --- FAVORITE LOGIC (UPDATED WITH MESSENGER) ---
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

            // 👇👇👇 2. NEW LOGIC: UPDATE UI AFTER SUCCESS 👇👇👇
            if (addedSuccess)
            {
                MarkAsLiked(); // Turn heart red immediately

                // Use WeakReferenceMessenger to notify LibraryViewModel
                WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());
            }
        }

        private void MarkAsLiked() { IsFavorite = true; FavoriteColor = "Red"; }

        [RelayCommand]
        public void Next()
        {
            if (CurrentSong == null || Playlist.Count == 0) return;

            // 1. Xử lý Repeat 1 bài (Lặp lại chính nó)
            if (RepeatMode == 1 && _mediaElement != null)
            {
                _mediaElement.SeekTo(TimeSpan.Zero);
                return;
            }

            // 2. Tìm vị trí thực sự của bài hát hiện tại trong Playlist
            // (Dùng vòng lặp tìm theo Tên để tránh lỗi không tìm thấy đối tượng)
            int index = -1;
            for (int i = 0; i < Playlist.Count; i++)
            {
                // So sánh cả Tên và Ca sĩ để chắc chắn đúng bài
                if (Playlist[i].Title == CurrentSong.Title && Playlist[i].Artist == CurrentSong.Artist)
                {
                    index = i;
                    break;
                }
            }

            // Nếu không tìm thấy (lỗi lạ) -> Reset về bài đầu tiên
            if (index == -1)
            {
                PlaySong(Playlist[0]);
                return;
            }

            // 3. Xử lý Shuffle (Phát ngẫu nhiên)
            if (IsShuffle && Playlist.Count > 1)
            {
                var r = new Random();
                int nextIndex;
                do
                {
                    nextIndex = r.Next(Playlist.Count);
                } while (nextIndex == index); // Đảm bảo không trùng bài đang nghe

                PlaySong(Playlist[nextIndex]);
                return;
            }

            // 4. Chuyển bài bình thường
            if (index < Playlist.Count - 1)
            {
                // Chưa đến cuối danh sách -> Phát bài tiếp theo
                PlaySong(Playlist[index + 1]);
            }
            else
            {
                // Đã đến cuối danh sách -> Quay lại bài đầu tiên (Loop All)
                // (Thay đổi này giúp nút Next luôn hoạt động, không bị tắt nhạc)
                PlaySong(Playlist[0]);
            }
        }
        [RelayCommand]
        public void Previous()
        {
            if (CurrentSong == null || Playlist.Count == 0) return;

            // Nếu đang nghe được quá 3 giây -> Replay lại từ đầu bài này (giống Spotify/Youtube)
            if (CurrentPosition.TotalSeconds > 3 && _mediaElement != null)
            {
                _mediaElement.SeekTo(TimeSpan.Zero);
                return;
            }

            // Tìm index an toàn bằng Tên
            int index = -1;
            for (int i = 0; i < Playlist.Count; i++)
            {
                if (Playlist[i].Title == CurrentSong.Title && Playlist[i].Artist == CurrentSong.Artist)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1)
            {
                PlaySong(Playlist[0]);
                return;
            }

            // Logic lùi bài
            if (index > 0)
            {
                // Chưa phải bài đầu -> Lùi lại 1 bài
                PlaySong(Playlist[index - 1]);
            }
            else
            {
                // Đang ở bài đầu tiên -> Nhảy xuống bài cuối cùng
                PlaySong(Playlist[Playlist.Count - 1]);
            }
        }


        [ObservableProperty][NotifyPropertyChangedFor(nameof(ShuffleColor))] private bool _isShuffle;
        public string ShuffleColor => IsShuffle ? "#FF00CC" : "#FFFFFF";
        [ObservableProperty][NotifyPropertyChangedFor(nameof(RepeatColor))][NotifyPropertyChangedFor(nameof(RepeatIcon))] private int _repeatMode;
        public string RepeatColor => RepeatMode == 0 ? "#FFFFFF" : "#FF00CC";
        public string RepeatIcon => RepeatMode == 1 ? "🔂" : "🔁";
        [RelayCommand] public void ToggleShuffle() => IsShuffle = !IsShuffle;
        [RelayCommand] public void ToggleRepeat() => RepeatMode = (RepeatMode + 1) % 3;
        [RelayCommand] public async Task GoBack() => await Shell.Current.GoToAsync("..");
    }
}