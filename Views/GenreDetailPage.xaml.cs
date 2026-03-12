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
    }
}