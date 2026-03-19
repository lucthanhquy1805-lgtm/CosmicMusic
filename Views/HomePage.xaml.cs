using CosmicMusic.ViewModels;
using Microsoft.Maui.Dispatching;
using System;
using Microsoft.Maui.Controls; // Thêm thư viện này để dùng Animation

namespace CosmicMusic.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    private IDispatcherTimer _scrollTimer;
    private int _recentIdx = 0;
    private int _albumIdx = 0;
    private int _artistIdx = 0;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        _scrollTimer = Dispatcher.CreateTimer();
        _scrollTimer.Interval = TimeSpan.FromSeconds(4);
        _scrollTimer.Tick += OnAutoScrollTick;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadUserAvatar();

        if (_viewModel.AudioPlayer != null && appMediaElement != null)
        {
            _viewModel.AudioPlayer.SetMediaElement(appMediaElement);
        }

        _scrollTimer.Start();

        // 👇 BẬT HIỆU ỨNG RỰC LỬA CHO MINIPLAYER KHI VÀO TRANG HOME 👇
        StartNeonPulseAnimation();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _scrollTimer.Stop();

        // 👇 TẮT HIỆU ỨNG KHI RỜI ĐI ĐỂ TIẾT KIỆM PIN 👇
        MiniPlayerBorder?.AbortAnimation("NeonPulse");
    }

    // ==========================================================
    // CỖ MÁY HIỆU ỨNG "NHỊP THỞ NEON" CHO MINIPLAYER
    // ==========================================================
    private void StartNeonPulseAnimation()
    {
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

        // Chạy Animation lặp đi lặp lại vô tận (Mỗi chu kỳ mất 1.5 giây)
        pulseAnimation.Commit(MiniPlayerBorder, "NeonPulse", length: 1500, easing: Easing.Linear, repeat: () => true);
    }

    // ==========================================================
    // LOGIC XỬ LÝ TỰ ĐỘNG CUỘN (Giữ nguyên của bạn)
    // ==========================================================
    private void OnAutoScrollTick(object sender, EventArgs e)
    {
        if (_viewModel == null) return;

        if (_viewModel.RecentlyPlayed != null && _viewModel.RecentlyPlayed.Count > 1 && RecentList != null)
        {
            _recentIdx++;
            if (_recentIdx >= _viewModel.RecentlyPlayed.Count) _recentIdx = 0;
            RecentList.ScrollTo(_recentIdx, position: ScrollToPosition.Center, animate: true);
        }

        if (_viewModel.FeaturedAlbums != null && _viewModel.FeaturedAlbums.Count > 1 && AlbumList != null)
        {
            _albumIdx++;
            if (_albumIdx >= _viewModel.FeaturedAlbums.Count) _albumIdx = 0;
            AlbumList.ScrollTo(_albumIdx, position: ScrollToPosition.Center, animate: true);
        }

        if (_viewModel.TopArtists != null && _viewModel.TopArtists.Count > 1 && ArtistList != null)
        {
            _artistIdx++;
            if (_artistIdx >= _viewModel.TopArtists.Count) _artistIdx = 0;
            ArtistList.ScrollTo(_artistIdx, position: ScrollToPosition.Center, animate: true);
        }
    }
}