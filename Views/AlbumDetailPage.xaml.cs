using CosmicMusic.ViewModels;

namespace CosmicMusic.Views
{
    public partial class AlbumDetailPage : ContentPage
    {
        // 👇 Quan trọng: Inject AlbumDetailViewModel vào đây
        public AlbumDetailPage(AlbumDetailViewModel viewModel)
        {
            InitializeComponent();

            // Gán ViewModel làm nguồn dữ liệu cho trang này
            BindingContext = viewModel;
        }
    }
}