using CosmicMusic.ViewModels;
using CommunityToolkit.Maui.Core.Primitives; // Cần cho MediaElement
using System.ComponentModel; // Cần cho PropertyChanged

namespace CosmicMusic.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _homeViewModel;
    private readonly AudioViewModel _audioViewModel;

    public HomePage(HomeViewModel homeViewModel, AudioViewModel audioViewModel)
    {
        InitializeComponent();

        _homeViewModel = homeViewModel;
        _audioViewModel = audioViewModel;

        BindingContext = _homeViewModel;

        // 1. Đăng ký nghe lệnh Play/Pause từ ViewModel
        _audioViewModel.PropertyChanged += OnAudioViewModelPropertyChanged;

        // 2. Đăng ký nghe lệnh TUA NHẠC (Seek) từ ViewModel
        _audioViewModel.RequestSeek += (s, e) =>
        {
            homeMediaElement.SeekTo(_audioViewModel.CurrentPosition);
        };
    }

    // 👇 [QUAN TRỌNG] THÊM ĐOẠN NÀY ĐỂ CẬP NHẬT AVATAR 👇
    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Gọi hàm tải chữ cái Avatar mỗi khi màn hình hiện lên
        _homeViewModel.LoadUserAvatar();
    }
    // 👆 HẾT PHẦN THÊM MỚI 👆

    // --- XỬ LÝ ĐỒNG BỘ PLAY/PAUSE ---
    private void OnAudioViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AudioViewModel.IsPlaying))
        {
            if (_audioViewModel.IsPlaying)
                homeMediaElement.Play();
            else
                homeMediaElement.Pause();
        }
    }

    // --- KHI NHẠC VỪA LOAD XONG ---
    private void OnMediaOpened(object sender, EventArgs e)
    {
        if (homeMediaElement.Duration != TimeSpan.Zero)
        {
            _audioViewModel.Duration = homeMediaElement.Duration;
        }
    }

    // --- KHI NHẠC ĐANG CHẠY ---
    private void OnPositionChanged(object sender, MediaPositionChangedEventArgs e)
    {
        // 1. NẾU ĐANG KÉO THANH TRƯỢT -> DỪNG CẬP NHẬT
        if (_audioViewModel.IsDragging) return;

        // 2. Cập nhật vị trí bình thường
        _audioViewModel.CurrentPosition = e.Position;

        // 3. Sửa lỗi "Bóng ma thời gian"
        if (homeMediaElement.Duration != TimeSpan.Zero &&
            _audioViewModel.Duration != homeMediaElement.Duration)
        {
            _audioViewModel.Duration = homeMediaElement.Duration;
        }
    }

    // --- KHI HẾT BÀI (AUTO NEXT) ---
    private void OnMediaEnded(object sender, EventArgs e)
    {
        if (_audioViewModel.NextCommand.CanExecute(null))
        {
            _audioViewModel.NextCommand.Execute(null);
        }
    }
}