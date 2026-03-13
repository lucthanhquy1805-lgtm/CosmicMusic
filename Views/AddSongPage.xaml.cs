using CosmicMusic.ViewModels;

namespace CosmicMusic.Views
{
    public partial class AddSongPage : ContentPage
    {
        public AddSongPage(AddSongViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}