using CosmicMusic.ViewModels;
using Microsoft.Maui.Controls; // 👇 BỔ SUNG: Thư viện bắt buộc để chạy Animation

namespace CosmicMusic.Views
{
    public partial class AlbumDetailPage : ContentPage
    {
        public AlbumDetailPage(AlbumDetailViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        // 👇 BỔ SUNG: BẬT HIỆU ỨNG KHI MỞ TRANG ALBUM 👇
        protected override void OnAppearing()
        {
            base.OnAppearing();
            StartNeonPulseAnimation();
        }

        // 👇 BỔ SUNG: TẮT HIỆU ỨNG KHI RỜI TRANG ĐỂ TIẾT KIỆM PIN 👇
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
            // Kiểm tra an toàn để tránh lỗi Crash
            if (MiniPlayerShadow == null || MiniPlayerBorder == null) return;

            var pulseAnimation = new Animation();

            // 1. Nhịp tỏa sáng (Phình to bán kính Radius từ 5 lên 25, tăng Opacity)
            var glowOut = new Animation(v =>
            {
                MiniPlayerShadow.Radius = (float)v;
                MiniPlayerShadow.Opacity = (float)(v / 25);
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
}