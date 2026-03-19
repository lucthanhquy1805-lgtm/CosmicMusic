using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.ViewModels;

namespace CosmicMusic.Views
{
    public partial class PlayerPage : ContentPage
    {
        public PlayerPage(AudioViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;

            // Giữ nguyên logic cuộn lời bài hát của bạn
            WeakReferenceMessenger.Default.Register<LyricScrolledMessage>(this, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (m.CurrentLine != null && LyricsCollectionView != null)
                    {
                        // Lệnh cuộn mượt mà và giữ câu hát ở giữa màn hình
                        LyricsCollectionView.ScrollTo(m.CurrentLine, position: ScrollToPosition.Center, animate: true);
                    }
                });
            });
        }

        // 👇 KÍCH HOẠT NHỊP THỞ KHI MỞ TRANG
        protected override void OnAppearing()
        {
            base.OnAppearing();
            StartAlbumArtPulse();
        }

        // 👇 TẮT ANIMATION KHI ĐÓNG TRANG ĐỂ GIẢI PHÓNG RAM
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            AlbumArtBorder?.AbortAnimation("AlbumPulse");
        }

        // ==========================================================
        // HIỆU ỨNG NHỊP THỞ NEON CHO ẢNH BÌA
        // ==========================================================
        private void StartAlbumArtPulse()
        {
            // Kiểm tra xem các x:Name đã đặt trong XAML có tồn tại không
            if (AlbumArtShadow == null || AlbumArtBorder == null) return;

            var pulseAnimation = new Animation();

            // 1. Nhịp tỏa sáng (Radius từ 20 lên 60)
            var glowOut = new Animation(v =>
            {
                AlbumArtShadow.Radius = (float)v;
                // Opacity tỷ lệ thuận với độ tỏa sáng để tạo cảm giác thực
                AlbumArtShadow.Opacity = (float)(v / 60);
            }, 20, 60, Easing.CubicOut);

            // 2. Nhịp mờ dần (Radius từ 60 về 20)
            var glowIn = new Animation(v =>
            {
                AlbumArtShadow.Radius = (float)v;
                AlbumArtShadow.Opacity = (float)(v / 60);
            }, 60, 20, Easing.CubicIn);

            // Ghép 2 giai đoạn vào 1 chu kỳ (0 -> 0.5 và 0.5 -> 1.0)
            pulseAnimation.Add(0, 0.5, glowOut);
            pulseAnimation.Add(0.5, 1, glowIn);

            // Chạy lặp lại mãi mãi với độ dài 2000ms (2 giây) cho mỗi nhịp thở
            pulseAnimation.Commit(AlbumArtBorder, "AlbumPulse", length: 2000, easing: Easing.Linear, repeat: () => true);
        }
    }
}