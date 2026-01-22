using CosmicMusic.Views;

namespace CosmicMusic;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Chỉ cần đăng ký đường dẫn là đủ
        Routing.RegisterRoute(nameof(PlayerPage), typeof(PlayerPage));
        Routing.RegisterRoute(nameof(SearchPage), typeof(SearchPage));
        Routing.RegisterRoute(nameof(Views.AlbumDetailPage), typeof(Views.AlbumDetailPage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(PremiumPage), typeof(PremiumPage));
        Routing.RegisterRoute(nameof(Views.ProfilePage), typeof(Views.ProfilePage));
        Routing.RegisterRoute(nameof(Views.EditProfilePage), typeof(Views.EditProfilePage));
        Routing.RegisterRoute(nameof(Views.SettingsPage), typeof(Views.SettingsPage));
        Routing.RegisterRoute(nameof(Views.ChangePasswordPage), typeof(Views.ChangePasswordPage));
    }

}