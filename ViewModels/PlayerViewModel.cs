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

        // 2. CONSTRUCTOR (Quan trọng: Đăng ký nhận thông báo thay đổi)
        public PlayerViewModel(AudioViewModel audioViewModel)
        {
            _audioViewModel = audioViewModel;

            // Kỹ thuật "Event Forwarding":
            // Khi AudioViewModel thay đổi (ví dụ: đổi bài, hết giờ, pause),
            // PlayerViewModel cũng báo cho giao diện cập nhật theo.
            _audioViewModel.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(e.PropertyName);

                // Cập nhật các thuộc tính phụ thuộc
                if (e.PropertyName == nameof(AudioViewModel.CurrentSong)) OnPropertyChanged(nameof(CurrentSong));
                if (e.PropertyName == nameof(AudioViewModel.IsPlaying)) OnPropertyChanged(nameof(IsPlaying));
                if (e.PropertyName == nameof(AudioViewModel.Duration)) OnPropertyChanged(nameof(Duration));
                if (e.PropertyName == nameof(AudioViewModel.CurrentPosition)) OnPropertyChanged(nameof(CurrentPosition));
                if (e.PropertyName == nameof(AudioViewModel.CurrentPositionSeconds)) OnPropertyChanged(nameof(CurrentPositionSeconds));
                if (e.PropertyName == nameof(AudioViewModel.IsShuffle)) OnPropertyChanged(nameof(IsShuffle));
                if (e.PropertyName == nameof(AudioViewModel.RepeatMode)) OnPropertyChanged(nameof(RepeatMode));
            };
        }

        // 3. CÁC THUỘC TÍNH (Trỏ thẳng sang AudioViewModel)
        // Giao diện sẽ Binding vào các tên này

        public Song CurrentSong => _audioViewModel.CurrentSong;

        public bool IsPlaying => _audioViewModel.IsPlaying;

        public TimeSpan Duration => _audioViewModel.Duration;

        public TimeSpan CurrentPosition => _audioViewModel.CurrentPosition;

        // Thuộc tính quan trọng cho Slider
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

        // 4. NHẬN DỮ LIỆU TỪ TRANG KHÁC (Search/Home) GỬI SANG
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            // Kiểm tra xem có dữ liệu bài hát được gửi sang không
            if (query.ContainsKey("SongData"))
            {
                var songData = query["SongData"] as LibraryItem;
                if (songData != null)
                {
                    // Chuyển đổi LibraryItem sang Song (nếu cần thiết hoặc dùng trực tiếp để tìm trong AudioViewModel)
                    // Ở đây giả sử AudioViewModel đã xử lý việc phát nhạc rồi, 
                    // hàm này chủ yếu để đảm bảo UI đồng bộ nếu cần logic riêng.
                    // Nhưng với logic hiện tại, AudioViewModel thường đã được kích hoạt từ trang trước.
                }
            }
        }

        // 5. CÁC LỆNH ĐIỀU KHIỂN (Gọi sang AudioViewModel thực hiện)

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

        // Các lệnh xử lý kéo thanh Slider
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