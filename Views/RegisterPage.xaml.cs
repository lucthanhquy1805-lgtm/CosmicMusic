using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class RegisterPage : ContentPage
{
    // 👇 Thêm tham số RegisterViewModel vm vào trong ngoặc
    public RegisterPage(RegisterViewModel vm)
    {
        InitializeComponent();

        // 👇 DÒNG QUAN TRỌNG NHẤT: Kết nối Giao diện với Logic
        BindingContext = vm;
    }
}