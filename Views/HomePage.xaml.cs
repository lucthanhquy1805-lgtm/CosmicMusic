using CosmicMusic.ViewModels;
using Microsoft.Maui.Dispatching;
using System;
using Microsoft.Maui.Controls; 
using System.Threading.Tasks; 
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
        if (RecentList != null) RecentList.Scrolled += OnListScrolled;
        if (AlbumList != null) AlbumList.Scrolled += OnListScrolled;
        if (ArtistList != null) ArtistList.Scrolled += OnListScrolled;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadUserAvatar();

      
        if (BindingContext is HomeViewModel vm)
        {
            
            Task.Run(async () => await vm.InitializeAsync());

            
        }
    

        if (_viewModel.AudioPlayer != null && appMediaElement != null)
        {
            _viewModel.AudioPlayer.SetMediaElement(appMediaElement);
        }

        _scrollTimer.Start();


        StartNeonPulseAnimation();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _scrollTimer.Stop();

     
        MiniPlayerBorder?.AbortAnimation("NeonPulse");
    }

   
    private void StartNeonPulseAnimation()
    {
        if (MiniPlayerShadow == null || MiniPlayerBorder == null) return;

        var pulseAnimation = new Animation();

       
        var glowOut = new Animation(v =>
        {
            MiniPlayerShadow.Radius = (float)v;
            MiniPlayerShadow.Opacity = (float)(v / 25);
        }, 5, 25, Easing.CubicOut);

       
        var glowIn = new Animation(v =>
        {
            MiniPlayerShadow.Radius = (float)v;
            MiniPlayerShadow.Opacity = (float)(v / 25);
        }, 25, 5, Easing.CubicIn);

       
        pulseAnimation.Add(0, 0.5, glowOut);
        pulseAnimation.Add(0.5, 1, glowIn);

       
        pulseAnimation.Commit(MiniPlayerBorder, "NeonPulse", length: 1500, easing: Easing.Linear, repeat: () => true);
    }

   
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
    private void OnListScrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        
        if (_scrollTimer != null && _scrollTimer.IsRunning)
        {
            _scrollTimer.Stop();
            _scrollTimer.Start();
        }
    }
}