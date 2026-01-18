using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class AlbumDetailPage : ContentPage
{
    // 👇 Thêm tham số viewModel vào Constructor
    public AlbumDetailPage(AlbumDetailViewModel viewModel)
    {
        InitializeComponent();

        // 👇 Kết nối giao diện với logic
        BindingContext = viewModel;
    }
}