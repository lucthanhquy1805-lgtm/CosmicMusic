using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class PlayerPage : ContentPage
{
    // Chúng ta không cần biến _viewModel riêng nữa vì BindingContext đã lo rồi
    // Nhưng nếu bạn muốn giữ cũng không sao.

    public PlayerPage(AudioViewModel audioViewModel)
    {
        InitializeComponent();

        // Gán BindingContext để giao diện nhận dữ liệu
        BindingContext = audioViewModel;
    }

    // 👇 ĐÃ XÓA HÀM OnAppearing (Vì trang này không còn giữ Loa nữa)
    // Trang này giờ chỉ thuần túy là giao diện điều khiển (Remote)
}