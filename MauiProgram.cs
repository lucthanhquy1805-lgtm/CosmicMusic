using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Views;
using CosmicMusic.Services;
using Microsoft.Extensions.Logging;
using CosmicMusic.ViewModels;
using CosmicMusic.Views;

namespace CosmicMusic;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Montserrat-Bold.ttf", "MontserratBold");
                fonts.AddFont("Montserrat-Regular.ttf", "MontserratRegular");
            });

        // ==========================================
        // ĐĂNG KÝ SERVICE TẠI ĐÂY
        // ==========================================
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<MusicApiService>();
        builder.Services.AddSingleton<AudioViewModel>();

        // Lưu ý: AppShell thường không cần AddSingleton trừ khi bạn Inject nó, 
        // nhưng để đây cũng không sao.
        builder.Services.AddSingleton<AppShell>();

        // --- NHÓM SEARCH (Phải có đủ cả ViewModel và Page) ---
        builder.Services.AddTransient<SearchViewModel>();
        builder.Services.AddTransient<SearchPage>(); // 👈 BẠN BỊ THIẾU DÒNG NÀY

        // --- NHÓM HOME ---
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<HomePage>();

        // --- NHÓM PLAYER ---
        builder.Services.AddTransient<PlayerViewModel>();
        builder.Services.AddTransient<PlayerPage>();

        // --- NHÓM LIBRARY ---
        builder.Services.AddTransient<LibraryViewModel>();
        builder.Services.AddTransient<LibraryPage>();
        // --- NHÓM ALBUM DETAIL ---
        builder.Services.AddTransient<AlbumDetailViewModel>();
        builder.Services.AddTransient<AlbumDetailPage>();
        //---- --- NHÓM LOGIN ---
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<LoginViewModel>();
        //--- --- NHÓM REGISTER ---
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<RegisterViewModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}