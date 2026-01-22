using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class LoginPage : ContentPage
{
    // 👇 SỬA LẠI CONSTRUCTOR NHƯ SAU:
    // Nhận LoginViewModel từ bên ngoài vào (Hệ thống sẽ tự lo phần FirestoreService)
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();

        // Gán ViewModel đã được tạo sẵn vào BindingContext
        BindingContext = vm;
    }
}