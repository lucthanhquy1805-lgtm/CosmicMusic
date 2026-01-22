using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class PlayerPage : ContentPage
{
    // 👇 Nhận AudioViewModel (Singleton hoặc Transient tùy setup)
    public PlayerPage(AudioViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}