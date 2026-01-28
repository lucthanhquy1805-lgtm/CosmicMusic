using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _homeViewModel;
    private readonly AudioViewModel _audioViewModel; // 👇 1. Thêm biến này để giữ tham chiếu

    // 👇 2. Thêm AudioViewModel vào hàm khởi tạo (Dependency Injection)
    public HomePage(HomeViewModel homeViewModel, AudioViewModel audioViewModel)
    {
        InitializeComponent();

        _homeViewModel = homeViewModel;
        _audioViewModel = audioViewModel; // Lưu lại để dùng sau

        BindingContext = _homeViewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Tải thông tin người dùng
        _homeViewModel.LoadUserAvatar();

        // 👇 3. KẾT NỐI QUAN TRỌNG: Gửi cái loa "appMediaElement" cho ViewModel điều khiển
        // "appMediaElement" là cái tên chúng ta vừa đặt bên file XAML lúc nãy
        if (appMediaElement != null)
        {
            _audioViewModel.SetMediaElement(appMediaElement);
        }
    }
}