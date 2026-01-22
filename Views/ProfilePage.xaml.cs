using CosmicMusic.ViewModels;
namespace CosmicMusic.Views;

public partial class ProfilePage : ContentPage
{
    public ProfilePage()
    {
        InitializeComponent();
        BindingContext = new ProfileViewModel();
    }

    // Refresh dữ liệu mỗi khi quay lại trang này (để cập nhật tên mới nếu vừa đổi)
    protected override void OnAppearing()
    {
        base.OnAppearing();
        (BindingContext as ProfileViewModel)?.LoadUserData();
    }
}