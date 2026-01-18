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

    }

}