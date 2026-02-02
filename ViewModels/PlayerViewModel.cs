using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;

namespace CosmicMusic.ViewModels
{
    // Sử dụng IQueryAttributable để nhận dữ liệu linh hoạt hơn
    public partial class PlayerViewModel : ObservableObject, IQueryAttributable
    {
        // 1. Khai báo AudioViewModel là "bộ não" chính
        private readonly AudioViewModel _audioViewModel;

        // 2. CONSTRUCTOR
        public PlayerViewModel(AudioViewModel audioViewModel)
        {
            _audioViewModel = audioViewModel;

            // Kỹ thuật "Event Forwarding" (Cầu nối sự kiện):
            _audioViewModel.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(e.PropertyName);

                // Cập nhật các thuộc tính cơ bản
                if (e.PropertyName == nameof(AudioViewModel.CurrentSong)) OnPropertyChanged(nameof(CurrentSong));
                if (e.PropertyName == nameof(AudioViewModel.IsPlaying)) OnPropertyChanged(nameof(IsPlaying));
                if (e.PropertyName == nameof(AudioViewModel.Duration)) OnPropertyChanged(nameof(Duration));
                if (e.PropertyName == nameof(AudioViewModel.CurrentPosition)) OnPropertyChanged(nameof(CurrentPosition));
                if (e.PropertyName == nameof(AudioViewModel.CurrentPositionSeconds)) OnPropertyChanged(nameof(CurrentPositionSeconds));
                if (e.PropertyName == nameof(AudioViewModel.IsShuffle)) OnPropertyChanged(nameof(IsShuffle));
                if (e.PropertyName == nameof(AudioViewModel.RepeatMode)) OnPropertyChanged(nameof(RepeatMode));

                // 👇 THÊM: Cập nhật thông báo cho Lyric và Tim
                if (e.PropertyName == nameof(AudioViewModel.IsLyricsVisible)) OnPropertyChanged(nameof(IsLyricsVisible));
                if (e.PropertyName == nameof(AudioViewModel.IsFavorite)) OnPropertyChanged(nameof(IsFavorite));
            };
        }

        // 3. CÁC THUỘC TÍNH (CẦU NỐI - CHỈ TRỎ SANG AUDIOVIEWMODEL)

        public Song CurrentSong => _audioViewModel.CurrentSong;
        public bool IsPlaying => _audioViewModel.IsPlaying;
        public TimeSpan Duration => _audioViewModel.Duration;
        public TimeSpan CurrentPosition => _audioViewModel.CurrentPosition;

        // 👇 THÊM: Cầu nối hiển thị Lyric
        public bool IsLyricsVisible => _audioViewModel.IsLyricsVisible;

        // 👇 THÊM: Cầu nối hiển thị Tim đỏ/xám
        public bool IsFavorite => _audioViewModel.IsFavorite;

        public double CurrentPositionSeconds
        {
            get => _audioViewModel.CurrentPositionSeconds;
            set => _audioViewModel.CurrentPositionSeconds = value;
        }

        public double Volume
        {
            get => _audioViewModel.Volume;
            set => _audioViewModel.Volume = value;
        }

        public bool IsShuffle => _audioViewModel.IsShuffle;
        public int RepeatMode => _audioViewModel.RepeatMode;

        // 4. NHẬN DỮ LIỆU TỪ TRANG KHÁC
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("SongData"))
            {
                var songData = query["SongData"] as LibraryItem;
                // Logic xử lý nếu cần
            }
        }

        // 5. CÁC LỆNH ĐIỀU KHIỂN (GỌI SANG AUDIOVIEWMODEL THỰC HIỆN)

        [RelayCommand]
        public void PlayPause() => _audioViewModel.PlayPauseCommand.Execute(null);

        [RelayCommand]
        public void Previous() => _audioViewModel.PreviousCommand.Execute(null);

        [RelayCommand]
        public void Next() => _audioViewModel.NextCommand.Execute(null);

        [RelayCommand]
        public void ToggleShuffle() => _audioViewModel.ToggleShuffleCommand.Execute(null);

        [RelayCommand]
        public void ToggleRepeat() => _audioViewModel.ToggleRepeatCommand.Execute(null);

        // 👇 THÊM: Lệnh bật Lyric (Gọi sang nhạc trưởng)
        [RelayCommand]
        public void ToggleLyrics() => _audioViewModel.ToggleLyricsCommand.Execute(null);

        // 👇 THÊM: Lệnh thả tim (Gọi sang nhạc trưởng)
        [RelayCommand]
        public void ToggleFavorite()
        {
            if (_audioViewModel.ToggleFavoriteCommand != null)
                _audioViewModel.ToggleFavoriteCommand.Execute(null);
        }

        [RelayCommand]
        public void DragStarted() => _audioViewModel.DragStartedCommand.Execute(null);

        [RelayCommand]
        public void DragCompleted() => _audioViewModel.DragCompletedCommand.Execute(null);

        [RelayCommand]
        public async Task GoBack()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}