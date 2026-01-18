using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;
using System.Collections.ObjectModel;

namespace CosmicMusic.ViewModels
{
    public partial class AudioViewModel : ObservableObject
    {
        public ObservableCollection<Song> Playlist { get; set; } = new();

        // [ĐÃ SỬA] Cho phép null để tránh lỗi khởi tạo
        [ObservableProperty]
        private Song? _currentSong;

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private TimeSpan _duration;

        [ObservableProperty]
        private double _volume = 1.0;

        // ==========================================================
        // 1. LOGIC THỜI GIAN & TUA NHẠC (ĐÃ TỐI ƯU SLIDER)
        // ==========================================================

        // [ĐÃ SỬA] Bỏ Attribute NotifyPropertyChangedFor ở đây để kiểm soát thủ công
        [ObservableProperty]
        private TimeSpan _currentPosition;

        // [MỚI] Hàm này được gọi tự động khi _currentPosition thay đổi
        // Chúng ta dùng nó để chặn cập nhật Slider khi đang kéo
        partial void OnCurrentPositionChanged(TimeSpan value)
        {
            // Nếu KHÔNG đang kéo chuột thì mới cập nhật Slider
            if (!IsDragging)
            {
                OnPropertyChanged(nameof(CurrentPositionSeconds));
            }
        }

        public double CurrentPositionSeconds
        {
            get => CurrentPosition.TotalSeconds;
            set
            {
                // Chỉ cho phép cập nhật ngược lại TimeSpan khi người dùng đang kéo
                // hoặc khi giá trị thay đổi đáng kể
                if (IsDragging || Math.Abs(CurrentPosition.TotalSeconds - value) > 1)
                {
                    CurrentPosition = TimeSpan.FromSeconds(value);
                }
            }
        }

        [ObservableProperty]
        private bool _isDragging;

        public event EventHandler RequestSeek;

        [RelayCommand]
        public void DragStarted()
        {
            IsDragging = true;
        }

        [RelayCommand]
        public void DragCompleted()
        {
            IsDragging = false;
            // Cập nhật lại vị trí hiển thị lần cuối cho chính xác
            OnPropertyChanged(nameof(CurrentPositionSeconds));
            RequestSeek?.Invoke(this, EventArgs.Empty);
        }

        // ==========================================================
        // 2. LOGIC SHUFFLE & REPEAT
        // ==========================================================

        private bool _isShuffle;
        public bool IsShuffle
        {
            get => _isShuffle;
            set
            {
                if (SetProperty(ref _isShuffle, value))
                {
                    OnPropertyChanged(nameof(ShuffleColor));
                }
            }
        }
        public string ShuffleColor => IsShuffle ? "#FF00CC" : "#FFFFFF"; // Hồng / Trắng

        private int _repeatMode;
        public int RepeatMode
        {
            get => _repeatMode;
            set
            {
                if (SetProperty(ref _repeatMode, value))
                {
                    OnPropertyChanged(nameof(RepeatColor));
                    OnPropertyChanged(nameof(RepeatIcon));
                }
            }
        }
        public string RepeatColor => RepeatMode == 0 ? "#FFFFFF" : "#FF00CC";
        public string RepeatIcon => RepeatMode == 1 ? "🔂" : "🔁";

        // ==========================================================
        // 3. CÁC HÀM ĐIỀU KHIỂN
        // ==========================================================

        [RelayCommand]
        public void ToggleShuffle() => IsShuffle = !IsShuffle;

        [RelayCommand]
        public void ToggleRepeat() => RepeatMode = (RepeatMode + 1) % 3;

        [RelayCommand]
        public void PlayPause()
        {
            if (CurrentSong != null) // Chỉ toggle nếu đã có bài hát
                IsPlaying = !IsPlaying;
        }

        // --- HÀM PLAY SONG ---
        public void PlaySong(Song song, ObservableCollection<Song>? contextList = null)
        {
            if (song == null) return;

            // BƯỚC 1: ĐỒNG BỘ DANH SÁCH PHÁT
            if (contextList != null && contextList.Count > 0)
            {
                // [TỐI ƯU] Chỉ clear và add lại nếu danh sách thực sự khác nhau về số lượng
                // hoặc bài đầu tiên khác nhau (đây là cách check nhanh)
                bool needUpdate = Playlist.Count != contextList.Count;

                if (!needUpdate && Playlist.Count > 0)
                {
                    // Check thêm bài đầu tiên để chắc chắn
                    if (Playlist[0].Title != contextList[0].Title) needUpdate = true;
                }

                if (needUpdate)
                {
                    Playlist.Clear();
                    foreach (var item in contextList)
                    {
                        Playlist.Add(item);
                    }
                }
            }
            else if (!Playlist.Contains(song))
            {
                Playlist.Add(song);
            }

            // BƯỚC 2: TÌM BÀI HÁT
            var songInPlaylist = Playlist.FirstOrDefault(s => s.Title == song.Title && s.Artist == song.Artist);
            var targetSong = songInPlaylist ?? song;

            // BƯỚC 3: XỬ LÝ PHÁT NHẠC
            if (CurrentSong == targetSong)
            {
                // Nếu đang phát bài này rồi -> Reset về 0
                CurrentPosition = TimeSpan.Zero;
                OnPropertyChanged(nameof(CurrentPositionSeconds)); // Cập nhật lại slider về 0
                IsPlaying = true;
                return;
            }

            // Bài mới hoàn toàn
            IsDragging = false; // Reset trạng thái kéo đề phòng bị kẹt
            CurrentPosition = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
            CurrentSong = targetSong;
            IsPlaying = true;
        }

        [RelayCommand]
        public void Next()
        {
            if (CurrentSong == null || Playlist.Count == 0) return;

            // Ưu tiên 1: Repeat One
            if (RepeatMode == 1)
            {
                CurrentPosition = TimeSpan.Zero;
                RequestSeek?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Ưu tiên 2: Shuffle
            if (IsShuffle)
            {
                var random = new Random();
                int randomIndex = random.Next(Playlist.Count);
                PlaySong(Playlist[randomIndex]);
                return;
            }

            // Ưu tiên 3: Normal
            int index = Playlist.IndexOf(CurrentSong);
            if (index < Playlist.Count - 1)
            {
                PlaySong(Playlist[index + 1]);
            }
            else
            {
                // Hết danh sách -> Quay về đầu
                PlaySong(Playlist[0]);
            }
        }

        [RelayCommand]
        public void Previous()
        {
            if (CurrentSong == null || Playlist.Count == 0) return;

            // Nếu nghe quá 3s -> Về đầu bài
            if (CurrentPosition.TotalSeconds > 3)
            {
                CurrentPosition = TimeSpan.Zero;
                RequestSeek?.Invoke(this, EventArgs.Empty);
                return;
            }

            int index = Playlist.IndexOf(CurrentSong);
            if (index > 0)
                PlaySong(Playlist[index - 1]);
            else
                PlaySong(Playlist[Playlist.Count - 1]);
        }
    }
}