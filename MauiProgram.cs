using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using CosmicMusic.Services;
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
            // 👇 Bắt buộc có để dùng Behavior, Converter
            .UseMauiCommunityToolkit()
            // 👇 Bắt buộc có để dùng Video/Audio
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Montserrat-Bold.ttf", "MontserratBold");
                fonts.AddFont("Montserrat-Regular.ttf", "MontserratRegular");
            });

        // =================================================================
        // 1. ĐĂNG KÝ SERVICES (Singleton: Sống suốt vòng đời App)
        // =================================================================
        builder.Services.AddSingleton<FirestoreService>();
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<S3Service>();
        //builder.Services.AddSingleton<MusicApiService>();

        // AudioViewModel phải là Singleton
        builder.Services.AddSingleton<AudioViewModel>();

        builder.Services.AddSingleton<AppShell>();

        // =================================================================
        // 2. ĐĂNG KÝ PAGES & VIEWMODELS
        // =================================================================

        // --- NHÓM HOME ---
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<HomePage>();

        // --- NHÓM SEARCH ---
        builder.Services.AddTransient<SearchViewModel>();
        builder.Services.AddTransient<SearchPage>();

        // --- NHÓM PLAYER (QUAN TRỌNG: ĐÃ SỬA THÀNH SINGLETON) ---
        // 👇👇👇 SỬA DÒNG NÀY ĐỂ HẾT LỖI CHỒNG NHẠC 👇👇👇
        builder.Services.AddSingleton<PlayerPage>();

        builder.Services.AddTransient<PlayerViewModel>(); // ViewModel phụ này Transient cũng được

        // --- NHÓM LIBRARY ---
        builder.Services.AddTransient<LibraryViewModel>();
        builder.Services.AddTransient<LibraryPage>();

        // --- NHÓM ALBUM DETAIL ---
        builder.Services.AddTransient<AlbumDetailViewModel>();
        builder.Services.AddTransient<AlbumDetailPage>();

        // --- NHÓM TÀI KHOẢN ---
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();

        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<RegisterPage>();

        builder.Services.AddTransient<ChangePasswordViewModel>();
        builder.Services.AddTransient<ChangePasswordPage>();

        // --- NHÓM HỒ SƠ ---
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<ProfilePage>();

        builder.Services.AddTransient<EditProfileViewModel>();
        builder.Services.AddTransient<EditProfilePage>();

        // --- NHÓM KHÁC ---
        builder.Services.AddTransient<PremiumViewModel>();
        builder.Services.AddTransient<PremiumPage>();

        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<GenreDetailViewModel>();
        builder.Services.AddTransient<GenreDetailPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.Services.AddTransient<AddSongViewModel>();
        builder.Services.AddTransient<Views.AddSongPage>();
        return builder.Build();
    }
}