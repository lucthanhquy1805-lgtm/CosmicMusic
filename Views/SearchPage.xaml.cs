using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class SearchPage : ContentPage
{
    // BẮT BUỘC PHẢI CÓ THAM SỐ viewModel Ở ĐÂY 👇
    public SearchPage(SearchViewModel viewModel)
    {
        InitializeComponent();

        // 👇 DÒNG NÀY LÀ QUAN TRỌNG NHẤT - NẾU THIẾU THÌ KHÔNG TÌM ĐƯỢC GÌ CẢ
        BindingContext = viewModel;
    }
}