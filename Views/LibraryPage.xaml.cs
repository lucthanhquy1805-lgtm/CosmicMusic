using CosmicMusic.ViewModels;
using Microsoft.Maui.Controls; // 👇 BỔ SUNG: Thư viện bắt buộc để chạy Animation

namespace CosmicMusic.Views;

public partial class LibraryPage : ContentPage
{
    private readonly LibraryViewModel _viewModel;

    public LibraryPage(LibraryViewModel viewModel)
    {
        InitializeComponent();

        // 1. Gán ViewModel vào BindingContext
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    // 2. Dùng hàm này để ép App tải lại dữ liệu mỗi khi mở màn hình Library
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1. Cho phép nhịp thở Neon chạy ngay lập tức để giao diện trông mượt mà
        StartNeonPulseAnimation();

        // 👇 CHIẾN THUẬT CHỐNG GIẬT NHẠC 👇
        // Đợi 200 mili-giây (0.2 giây) để hiệu ứng chuyển Tab của hệ điều hành hoàn tất, 
        // giúp CPU không bị sốc tải.
        await Task.Delay(200);

        if (_viewModel != null)
        {
            // 2. Chuyển trang xong xuôi rồi mới bắt đầu gọi API tải dữ liệu
            await _viewModel.LoadLibraryCommand.ExecuteAsync(null);
        }
    }

    // 👇 BỔ SUNG: Tắt hiệu ứng khi rời trang để tiết kiệm Pin và RAM 👇
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        MiniPlayerBorder?.AbortAnimation("NeonPulse");
    }

    // ==========================================================
    // 👇 BỔ SUNG: CỖ MÁY HIỆU ỨNG "NHỊP THỞ NEON" CHO MINIPLAYER
    // ==========================================================
    private void StartNeonPulseAnimation()
    {
        // Kiểm tra an toàn: Tránh lỗi Crash nếu giao diện XAML chưa load xong
        if (MiniPlayerShadow == null || MiniPlayerBorder == null) return;

        var pulseAnimation = new Animation();

        // 1. Nhịp tỏa sáng (Phình to bán kính Radius từ 5 lên 25, tăng Opacity)
        var glowOut = new Animation(v =>
        {
            MiniPlayerShadow.Radius = (float)v;
            MiniPlayerShadow.Opacity = (float)(v / 25); // Opacity linh động từ 0.2 đến 1.0
        }, 5, 25, Easing.CubicOut);

        // 2. Nhịp mờ dần (Thu nhỏ bán kính từ 25 về 5, giảm Opacity)
        var glowIn = new Animation(v =>
        {
            MiniPlayerShadow.Radius = (float)v;
            MiniPlayerShadow.Opacity = (float)(v / 25);
        }, 25, 5, Easing.CubicIn);

        // Ghép 2 nhịp lại: 50% thời gian đầu phình ra, 50% thời gian sau thu vào
        pulseAnimation.Add(0, 0.5, glowOut);
        pulseAnimation.Add(0.5, 1, glowIn);

        // Chạy Animation lặp đi lặp lại vô tận (Mỗi chu kỳ mất 1.5 giây = 1500ms)
        pulseAnimation.Commit(MiniPlayerBorder, "NeonPulse", length: 1500, easing: Easing.Linear, repeat: () => true);
    }
}