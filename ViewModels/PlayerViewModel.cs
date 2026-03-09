using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CosmicMusic.Models;
using System.Collections.ObjectModel;

namespace CosmicMusic.ViewModels
{
    public partial class PlayerViewModel : ObservableObject, IQueryAttributable
    {
        private readonly AudioViewModel _audioViewModel;

        public PlayerViewModel(AudioViewModel audioViewModel)
        {
            _audioViewModel = audioViewModel;

            _audioViewModel.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(e.PropertyName);

                if (e.PropertyName == nameof(AudioViewModel.CurrentSong)) OnPropertyChanged(nameof(CurrentSong));
                if (e.PropertyName == nameof(AudioViewModel.IsPlaying)) OnPropertyChanged(nameof(IsPlaying));
                if (e.PropertyName == nameof(AudioViewModel.Duration)) OnPropertyChanged(nameof(Duration));
                if (e.PropertyName == nameof(AudioViewModel.CurrentPosition)) OnPropertyChanged(nameof(CurrentPosition));
                if (e.PropertyName == nameof(AudioViewModel.CurrentPositionSeconds)) OnPropertyChanged(nameof(CurrentPositionSeconds));
                if (e.PropertyName == nameof(AudioViewModel.IsShuffle)) OnPropertyChanged(nameof(IsShuffle));
                if (e.PropertyName == nameof(AudioViewModel.RepeatMode)) OnPropertyChanged(nameof(RepeatMode));
                if (e.PropertyName == nameof(AudioViewModel.IsLyricsVisible)) OnPropertyChanged(nameof(IsLyricsVisible));
                if (e.PropertyName == nameof(AudioViewModel.IsFavorite)) OnPropertyChanged(nameof(IsFavorite));

                // 👇 BỔ SUNG: Cầu nối lắng nghe trạng thái Đang sửa Lyric
                if (e.PropertyName == nameof(AudioViewModel.IsEditingLyrics)) OnPropertyChanged(nameof(IsEditingLyrics));
            };
        }

        public Song CurrentSong => _audioViewModel.CurrentSong;
        public bool IsPlaying => _audioViewModel.IsPlaying;
        public TimeSpan Duration => _audioViewModel.Duration;
        public TimeSpan CurrentPosition => _audioViewModel.CurrentPosition;
        public bool IsLyricsVisible => _audioViewModel.IsLyricsVisible;
        public bool IsFavorite => _audioViewModel.IsFavorite;
        // Thêm dòng này vào PlayerViewModel.cs
        public ObservableCollection<LyricLine> SyncedLyrics => _audioViewModel.SyncedLyrics;

        // 👇 BỔ SUNG: Biến trạng thái sửa Lyric
        public bool IsEditingLyrics => _audioViewModel.IsEditingLyrics;

        public double CurrentPositionSeconds
        {
            get => _audioViewModel.CurrentPositionSeconds;
            set => _audioViewModel.CurrentPositionSeconds = value;
        }

        public double Volume
        {
            get => _audioViewModel.Volume;
            set => _audioViewModel.Volume = value;
        }

        public bool IsShuffle => _audioViewModel.IsShuffle;
        public int RepeatMode => _audioViewModel.RepeatMode;

        public void ApplyQueryAttributes(IDictionary<string, object> query) { }

        [RelayCommand] public void PlayPause() => _audioViewModel.PlayPauseCommand.Execute(null);
        [RelayCommand] public void Previous() => _audioViewModel.PreviousCommand.Execute(null);
        [RelayCommand] public void Next() => _audioViewModel.NextCommand.Execute(null);
        [RelayCommand] public void ToggleShuffle() => _audioViewModel.ToggleShuffleCommand.Execute(null);
        [RelayCommand] public void ToggleRepeat() => _audioViewModel.ToggleRepeatCommand.Execute(null);
        [RelayCommand] public void ToggleLyrics() => _audioViewModel.ToggleLyricsCommand.Execute(null);
        [RelayCommand] public void DragStarted() => _audioViewModel.DragStartedCommand.Execute(null);
        [RelayCommand] public void DragCompleted() => _audioViewModel.DragCompletedCommand.Execute(null);
        [RelayCommand] public async Task GoBack() => await Shell.Current.GoToAsync("..");

        [RelayCommand] public void ToggleFavorite() { if (_audioViewModel.ToggleFavoriteCommand != null) _audioViewModel.ToggleFavoriteCommand.Execute(null); }
        [RelayCommand] public void AddToPlaylist() { if (_audioViewModel.AddToPlaylistCommand != null) _audioViewModel.AddToPlaylistCommand.Execute(null); }

        // 👇 BỔ SUNG: 2 lệnh để thao tác bật Edit và Save Lyric
        [RelayCommand] public void ToggleEditLyrics() { if (_audioViewModel.ToggleEditLyricsCommand != null) _audioViewModel.ToggleEditLyricsCommand.Execute(null); }
        [RelayCommand] public void SaveLyrics() { if (_audioViewModel.SaveLyricsCommand != null) _audioViewModel.SaveLyricsCommand.Execute(null); }
    }
}