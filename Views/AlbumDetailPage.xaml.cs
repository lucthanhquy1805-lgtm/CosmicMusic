using CosmicMusic.ViewModels;

namespace CosmicMusic.Views
{
    public partial class AlbumDetailPage : ContentPage
    {
        public AlbumDetailPage(AlbumDetailViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        
    }
}