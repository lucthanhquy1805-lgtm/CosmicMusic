namespace CosmicMusic.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private async void OnChangePasswordTapped(object sender, EventArgs e)
    {
        // Chuyển sang trang Đổi mật khẩu
        await Shell.Current.GoToAsync(nameof(ChangePasswordPage));
    }
}