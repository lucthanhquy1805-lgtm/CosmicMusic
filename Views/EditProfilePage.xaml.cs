using CosmicMusic.ViewModels;
namespace CosmicMusic.Views;

public partial class EditProfilePage : ContentPage
{
    public EditProfilePage()
    {
        InitializeComponent();
        BindingContext = new EditProfileViewModel();
    }
}