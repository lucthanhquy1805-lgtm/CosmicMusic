using Microsoft.Maui.Controls;
using Microsoft.Maui;
using CosmicMusic.ViewModels;

namespace CosmicMusic.Views
{
    public partial class GenreDetailPage : ContentPage
    {
        public GenreDetailPage(GenreDetailViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        // 👇 KÍCH HOẠT KHI TRANG XUẤT HIỆN 👇
        protected override void OnAppearing()
        {
            base.OnAppearing();
            StartNeonPulseAnimation();
        }

        // 👇 TẮT KHI RỜI TRANG ĐỂ TRÁNH TỐN TÀI NGUYÊN 👇
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            MiniPlayerBorder?.AbortAnimation("NeonPulse");
        }

        // ==========================================================
        // CỖ MÁY HIỆU ỨNG "NHỊP THỞ NEON"
        // ==========================================================
        private void StartNeonPulseAnimation()
        {
            // Kiểm tra xem các phần tử có tồn tại (đã đặt x:Name trong XAML) hay không
            if (MiniPlayerShadow == null || MiniPlayerBorder == null) return;

            var pulseAnimation = new Animation();

            // 1. Nhịp tỏa sáng (Tăng bán kính từ 5 lên 25)
            var glowOut = new Animation(v =>
            {
                MiniPlayerShadow.Radius = (float)v;
                MiniPlayerShadow.Opacity = (float)(v / 25);
            }, 5, 25, Easing.CubicOut);

            // 2. Nhịp mờ dần (Giảm bán kính từ 25 về 5)
            var glowIn = new Animation(v =>
            {
                MiniPlayerShadow.Radius = (float)v;
                MiniPlayerShadow.Opacity = (float)(v / 25);
            }, 25, 5, Easing.CubicIn);

            // Ghép 2 nhịp lại thành 1 vòng tuần hoàn
            pulseAnimation.Add(0, 0.5, glowOut);
            pulseAnimation.Add(0.5, 1, glowIn);

            // Chạy lặp lại mãi mãi
            pulseAnimation.Commit(MiniPlayerBorder, "NeonPulse", length: 1500, easing: Easing.Linear, repeat: () => true);
        }
    }
}