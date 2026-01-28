using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class LibraryPage : ContentPage
{
    private readonly LibraryViewModel _viewModel;

    public LibraryPage(LibraryViewModel viewModel)
    {
        InitializeComponent();

        // 1. Gán ViewModel vào BindingContext
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    // 2. Dùng hàm này để ép App tải lại dữ liệu mỗi khi mở màn hình Library
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel != null)
        {
            // Gọi trực tiếp hàm tải dữ liệu
            await _viewModel.LoadLibraryCommand.ExecuteAsync(null);
        }
    }
}