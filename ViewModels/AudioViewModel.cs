using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;
using System.Collections.ObjectModel;

namespace CosmicMusic.ViewModels
{
    public partial class AudioViewModel : ObservableObject
    {
        public ObservableCollection<Song> Playlist { get; set; } = new();

        [ObservableProperty]
        private Song? _currentSong;

        [ObservableProperty]
        private bool _isPlaying;

        [ObservableProperty]
        private TimeSpan _duration;

        [ObservableProperty]
        private double _volume = 1.0;

        // ==========================================================
        // 1. LOGIC THỜI GIAN & TUA NHẠC (GIỮ NGUYÊN VÌ ĐÃ TỐI ƯU)
        // ==========================================================

        [ObservableProperty]
        private TimeSpan _currentPosition;

        [ObservableProperty]
        private bool _isDragging;

        public event EventHandler RequestSeek;

        // Tự động cập nhật Slider khi thời gian thay đổi (chỉ khi không kéo)
        partial void OnCurrentPositionChanged(TimeSpan value)
        {
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
                if (IsDragging || Math.Abs(CurrentPosition.TotalSeconds - value) > 1)
                {
                    CurrentPosition = TimeSpan.FromSeconds(value);
                }
            }
        }

        [RelayCommand]
        public void DragStarted() => IsDragging = true;

        [RelayCommand]
        public void DragCompleted()
        {
            IsDragging = false;
            OnPropertyChanged(nameof(CurrentPositionSeconds));
            RequestSeek?.Invoke(this, EventArgs.Empty);
        }

        // ==========================================================
        // 2. LOGIC SHUFFLE & REPEAT (ĐÃ TỐI ƯU CODE)
        // ==========================================================

        // Dùng Attribute để tự động thông báo thay đổi màu sắc
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShuffleColor))]
        private bool _isShuffle;

        public string ShuffleColor => IsShuffle ? "#FF00CC" : "#FFFFFF"; // Hồng / Trắng

        // 0: Off, 1: One, 2: All
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RepeatColor))]
        [NotifyPropertyChangedFor(nameof(RepeatIcon))]
        private int _repeatMode;

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
            if (CurrentSong != null) IsPlaying = !IsPlaying;
        }

        // --- HÀM PLAY SONG (ĐÃ THÊM THREAD SAFETY) ---
        public void PlaySong(Song song, ObservableCollection<Song>? contextList = null)
        {
            if (song == null) return;

            // Đảm bảo cập nhật UI trên luồng chính
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // BƯỚC 1: ĐỒNG BỘ DANH SÁCH PHÁT
                if (contextList != null && contextList.Count > 0)
                {
                    bool needUpdate = Playlist.Count != contextList.Count;
                    if (!needUpdate && Playlist.Count > 0)
                    {
                        if (Playlist[0].Title != contextList[0].Title) needUpdate = true;
                    }

                    if (needUpdate)
                    {
                        Playlist.Clear();
                        foreach (var item in contextList) Playlist.Add(item);
                    }
                }
                else if (!Playlist.Contains(song))
                {
                    Playlist.Add(song);
                }

                // BƯỚC 2: TÌM BÀI HÁT & PLAY
                // Tìm bài hát tương ứng trong Playlist hiện tại để đảm bảo object reference đúng
                var targetSong = Playlist.FirstOrDefault(s => s.Title == song.Title && s.Artist == song.Artist) ?? song;

                if (CurrentSong == targetSong)
                {
                    CurrentPosition = TimeSpan.Zero;
                    OnPropertyChanged(nameof(CurrentPositionSeconds));
                    IsPlaying = true;
                    return;
                }

                IsDragging = false;
                CurrentPosition = TimeSpan.Zero;
                Duration = TimeSpan.Zero;
                CurrentSong = targetSong;
                IsPlaying = true;
            });
        }

        [RelayCommand]
        public void Next()
        {
            if (CurrentSong == null || Playlist.Count == 0) return;

            // TH1: Repeat One -> Tua lại từ đầu
            if (RepeatMode == 1)
            {
                CurrentPosition = TimeSpan.Zero;
                RequestSeek?.Invoke(this, EventArgs.Empty);
                return;
            }

            // TH2: Shuffle -> Random (Tránh bài hiện tại)
            if (IsShuffle && Playlist.Count > 1)
            {
                var random = new Random();
                int currentIndex = Playlist.IndexOf(CurrentSong);
                int randomIndex;
                do
                {
                    randomIndex = random.Next(Playlist.Count);
                } while (randomIndex == currentIndex); // Lặp lại nếu random trúng bài đang nghe

                PlaySong(Playlist[randomIndex]);
                return;
            }

            // TH3: Normal / Repeat All
            int index = Playlist.IndexOf(CurrentSong);

            if (index < Playlist.Count - 1)
            {
                // Chưa hết danh sách -> Bài tiếp theo
                PlaySong(Playlist[index + 1]);
            }
            else
            {
                // Hết danh sách
                if (RepeatMode == 2) // Nếu Repeat All -> Quay về đầu
                {
                    PlaySong(Playlist[0]);
                }
                else // Repeat Off -> Dừng lại hoặc Pause (tùy logic app, ở đây mình cho dừng)
                {
                    IsPlaying = false;
                    CurrentPosition = TimeSpan.Zero;
                }
            }
        }
        [RelayCommand]
        public async Task GoBack()
        {
            // Quay lại trang trước (thường là Home)
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        public void Previous()
        {
            if (CurrentSong == null || Playlist.Count == 0) return;

            // Nếu nghe quá 3s -> Về đầu bài hiện tại
            if (CurrentPosition.TotalSeconds > 3)
            {
                CurrentPosition = TimeSpan.Zero;
                RequestSeek?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Shuffle bật thì Previous cũng nên random (hoặc logic stack lịch sử, nhưng random cho đơn giản)
            if (IsShuffle && Playlist.Count > 1)
            {
                var random = new Random();
                int randomIndex = random.Next(Playlist.Count);
                PlaySong(Playlist[randomIndex]);
                return;
            }

            int index = Playlist.IndexOf(CurrentSong);
            if (index > 0)
            {
                PlaySong(Playlist[index - 1]);
            }
            else
            {
                // Đang ở bài đầu -> Về bài cuối
                PlaySong(Playlist[Playlist.Count - 1]);
            }
        }
    }
}