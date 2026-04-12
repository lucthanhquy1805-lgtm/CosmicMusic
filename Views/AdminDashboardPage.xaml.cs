using CosmicMusic.ViewModels;
using Microsoft.Maui.Controls;

namespace CosmicMusic.Views
{
    public partial class AdminDashboardPage : ContentPage
    {
        public AdminDashboardPage(AdminDashboardViewModel viewModel)
        {
            InitializeComponent();

            // 👇 BƯỚC QUAN TRỌNG NHẤT: Trói buộc UI với ViewModel
            BindingContext = viewModel;
        }

        // 👇 GỌI DỮ LIỆU KHI TRANG VỪA HIỂN THỊ (Chuẩn vòng đời MAUI)
        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is AdminDashboardViewModel vm)
            {
                // Gọi lệnh tải dữ liệu ở đây để mỗi lần Admin mở trang là list tự làm mới
                vm.LoadDataCommand.Execute(null);
            }
        }
    }
}