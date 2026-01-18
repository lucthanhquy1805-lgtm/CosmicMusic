namespace CosmicMusic;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Create AppShell directly (Simple and Safe)
        MainPage = new AppShell();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(MainPage);
    }
}