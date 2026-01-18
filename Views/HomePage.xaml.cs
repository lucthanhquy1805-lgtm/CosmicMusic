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
        // Khi người dùng thả tay khỏi thanh trượt, ViewModel bắn sự kiện này -> App thực hiện tua
        _audioViewModel.RequestSeek += (s, e) =>
        {
            homeMediaElement.SeekTo(_audioViewModel.CurrentPosition);
        };
    }

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

    // --- KHI NHẠC ĐANG CHẠY (QUAN TRỌNG: LOGIC KÉO THẢ) ---
    private void OnPositionChanged(object sender, MediaPositionChangedEventArgs e)
    {
        // 1. NẾU ĐANG KÉO THANH TRƯỢT -> DỪNG CẬP NHẬT
        // Để tránh thanh trượt bị giật lại vị trí cũ khi tay chưa kịp thả ra
        if (_audioViewModel.IsDragging) return;

        // 2. Cập nhật vị trí bình thường
        _audioViewModel.CurrentPosition = e.Position;

        // 3. Sửa lỗi "Bóng ma thời gian" (Nếu độ dài sai thì sửa lại ngay)
        if (homeMediaElement.Duration != TimeSpan.Zero &&
            _audioViewModel.Duration != homeMediaElement.Duration)
        {
            _audioViewModel.Duration = homeMediaElement.Duration;
        }
    }

    // --- KHI HẾT BÀI (AUTO NEXT) ---
    private void OnMediaEnded(object sender, EventArgs e)
    {
        _audioViewModel.NextCommand.Execute(null);
    }
}