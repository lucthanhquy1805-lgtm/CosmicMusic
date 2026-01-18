using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class PlayerPage : ContentPage
{
    // 👇 Phải có tham số viewModel ở đây
    public PlayerPage(PlayerViewModel viewModel)
    {
        InitializeComponent();

       
        BindingContext = viewModel;
    }
}