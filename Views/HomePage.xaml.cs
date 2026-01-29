using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    // Chỉ cần Inject HomeViewModel là đủ (vì trong HomeViewModel đã có AudioViewModel rồi)
    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // 1. Tải thông tin User
        _viewModel.LoadUserAvatar();

        // 2. KẾT NỐI MEDIA ELEMENT (Cơ chế Hand-off)
        // Chúng ta lấy AudioPlayer từ HomeViewModel ra dùng luôn
        if (_viewModel.AudioPlayer != null && appMediaElement != null)
        {
            _viewModel.AudioPlayer.SetMediaElement(appMediaElement);
        }
    }
}