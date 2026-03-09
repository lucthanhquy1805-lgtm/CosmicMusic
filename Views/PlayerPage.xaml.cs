using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class PlayerPage : ContentPage
{
    public PlayerPage(AudioViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

       
        WeakReferenceMessenger.Default.Register<LyricScrolledMessage>(this, (r, m) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (m.CurrentLine != null && LyricsCollectionView != null)
                {
                    // Lệnh cuộn mượt mà (animate: true) và giữ câu hát ở giữa màn hình (Center)
                    LyricsCollectionView.ScrollTo(m.CurrentLine, position: ScrollToPosition.Center, animate: true);
                }
            });
        });
        
    }
}