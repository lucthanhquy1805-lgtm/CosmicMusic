using CosmicMusic.ViewModels;

// 👇 DÒNG QUAN TRỌNG BỊ THIẾU ĐÂY RỒI 👇
namespace CosmicMusic.Views
{
    public partial class ProfilePage : ContentPage
    {
        public ProfilePage(ProfileViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        // Mỗi khi vào lại trang này, nó sẽ nạp lại thông tin mới nhất
        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is ProfileViewModel vm)
            {
                vm.LoadUserData();
            }
        }
    }
}