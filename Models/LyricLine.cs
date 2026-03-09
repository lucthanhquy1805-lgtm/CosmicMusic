using CommunityToolkit.Mvvm.ComponentModel;

namespace CosmicMusic.Models
{
    public partial class LyricLine : ObservableObject
    {
        [ObservableProperty]
        private TimeSpan _time;

        [ObservableProperty]
        private string _text;

        [ObservableProperty]
        private bool _isCurrent; // Biến này = true thì chữ sẽ phóng to và đổi màu
    }
}