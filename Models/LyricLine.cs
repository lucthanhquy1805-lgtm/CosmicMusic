using CommunityToolkit.Mvvm.ComponentModel;

namespace CosmicMusic.Models
{
    // BẮT BUỘC phải có ObservableObject thì giao diện mới tự đổi màu được
    public partial class LyricLine : ObservableObject
    {
        public TimeSpan Time { get; set; }
        public string Text { get; set; }

        [ObservableProperty]
        private bool _isCurrent;
    }
}