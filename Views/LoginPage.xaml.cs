using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();

        // 👇 KẾT NỐI VIEW VỚI VIEWMODEL
        this.BindingContext = new LoginViewModel();
    }
}