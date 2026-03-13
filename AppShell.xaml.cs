using CosmicMusic.Views;

namespace CosmicMusic
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Đăng ký đường dẫn cho các trang con
            Routing.RegisterRoute(nameof(PlayerPage), typeof(PlayerPage));
            Routing.RegisterRoute(nameof(SearchPage), typeof(SearchPage));
            Routing.RegisterRoute(nameof(AlbumDetailPage), typeof(AlbumDetailPage));
            Routing.RegisterRoute(nameof(GenreDetailPage), typeof(GenreDetailPage));
            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
            Routing.RegisterRoute(nameof(PremiumPage), typeof(PremiumPage));
            Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
            Routing.RegisterRoute(nameof(EditProfilePage), typeof(EditProfilePage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(ChangePasswordPage), typeof(ChangePasswordPage));
            Routing.RegisterRoute(nameof(Views.AddSongPage), typeof(Views.AddSongPage));

        }

        
    }
}