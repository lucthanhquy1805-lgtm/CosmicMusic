using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class LibraryPage : ContentPage
{
    // Bơm ViewModel vào đây
    public LibraryPage(LibraryViewModel vm)
    {
        InitializeComponent();

        // KẾT NỐI DỮ LIỆU TẠI ĐÂY
        BindingContext = vm;
    }
}