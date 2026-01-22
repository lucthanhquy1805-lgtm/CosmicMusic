using CosmicMusic.ViewModels;

namespace CosmicMusic.Views;

public partial class PremiumPage : ContentPage
{
    // Inject ViewModel vào Constructor
    public PremiumPage(PremiumViewModel vm)
    {
        InitializeComponent();

        // Kết nối Giao diện (View) với Logic (ViewModel)
        BindingContext = vm;
    }
}