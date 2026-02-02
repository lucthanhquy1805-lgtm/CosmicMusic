using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Models;
using CosmicMusic.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace CosmicMusic.ViewModels
{
    public class RefreshLibraryMessage { }

    public class SongPlayedMessage
    {
        public Song PlayedSong { get; set; }
        public SongPlayedMessage(Song song) => PlayedSong = song;
    }

    public partial class AudioViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        // 👇 1. THÊM KHAI BÁO SERVICE TÌM LYRIC
        private readonly LyricsService _lyricsService;

        private MediaElement _mediaElement;
        private bool _isDraggingSlider = false;

        public ObservableCollection<Song> Playlist { get; set; } = new();

        public AudioViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;

            // 👇 2. KHỞI TẠO SERVICE
            _lyricsService = new LyricsService();

            FavoriteColor = "#A569F7";
            IsFavorite = false;
        }

        // ==========================================================
        // CÁC THUỘC TÍNH (GIỮ NGUYÊN)
        // ==========================================================

        [ObservableProperty] private Song _currentSong;
        [ObservableProperty] private bool _isPlaying;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TotalDurationText))]
        private TimeSpan _duration;

        [ObservableProperty] private bool _isRepeat;

        public string TotalDurationText => $"{Duration:mm\\:ss}";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentPositionText))]
        private TimeSpan _currentPosition;
        public string CurrentPositionText => $"{CurrentPosition:mm\\:ss}";

        [ObservableProperty] private double _volume = 1.0;

        [ObservableProperty] private bool _isFavorite;
        [ObservableProperty] private string _favoriteColor;

        // Biến điều khiển hiển thị Lyric
        [ObservableProperty] private bool _isLyricsVisible = false;

        public double CurrentPositionSeconds
        {
            get => CurrentPosition.TotalSeconds;
            set { if (_isDraggingSlider) CurrentPosition = TimeSpan.FromSeconds(value); }
        }

        [ObservableProperty][NotifyPropertyChangedFor(nameof(ShuffleColor))] private bool _isShuffle;
        public string ShuffleColor => IsShuffle ? "#D946EF" : "#FFFFFF";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RepeatColor))]
        [NotifyPropertyChangedFor(nameof(RepeatIcon))]
        private int _repeatMode;

        public string RepeatColor => RepeatMode == 0 ? "#FFFFFF" : "#D946EF";
        public string RepeatIcon => RepeatMode == 1 ? "🔂" : "🔁";

        partial void OnRepeatModeChanged(int value)
        {
            IsRepeat = value > 0;
        }

        // ==========================================================
        // MEDIA ELEMENT (GIỮ NGUYÊN)
        // ==========================================================
        public void SetMediaElement(MediaElement newMediaElement)
        {
            if (_mediaElement == newMediaElement) return;

            if (_mediaElement != null)
            {
                try
                {
                    _mediaElement.MediaOpened -= OnMediaOpened;
                    _mediaElement.PositionChanged -= OnPositionChanged;
                    _mediaElement.MediaEnded -= OnMediaEnded;
                }
                catch { }
            }

            _mediaElement = newMediaElement;

            if (_mediaElement != null)
            {
                _mediaElement.MediaOpened += OnMediaOpened;
                _mediaElement.PositionChanged += OnPositionChanged;
                _mediaElement.MediaEnded += OnMediaEnded;

                if (CurrentSong != null)
                {
                    _mediaElement.Source = MediaSource.FromUri(CurrentSong.AudioUrl);

                    if (CurrentPosition.TotalSeconds > 0)
                    {
                        _mediaElement.SeekTo(CurrentPosition);
                    }

                    if (IsPlaying)
                    {
                        _mediaElement.Play();
                    }
                }
            }
        }

        private void OnMediaOpened(object sender, EventArgs e)
        {
            if (_mediaElement != null) Duration = _mediaElement.Duration;
        }

        private void OnPositionChanged(object sender, MediaPositionChangedEventArgs e)
        {
            if (_isDraggingSlider) return;
            CurrentPosition = e.Position;
            OnPropertyChanged(nameof(CurrentPositionSeconds));

            if (_mediaElement != null && _mediaElement.Duration > TimeSpan.Zero && Duration != _mediaElement.Duration)
            {
                Duration = _mediaElement.Duration;
            }
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            Next();
        }

        // ==========================================================
        // 4. HÀM PHÁT NHẠC (ĐÃ BỔ SUNG LOGIC TÌM LYRIC)
        // ==========================================================
        public void PlaySong(Song song, ObservableCollection<Song>? contextList = null)
        {
            if (song == null) return;

            bool isSameSong = (CurrentSong != null && CurrentSong.Title == song.Title);
            if (isSameSong && IsPlaying) return;

            if (contextList != null && contextList.Count > 0)
            {
                bool needUpdate = Playlist.Count != contextList.Count;
                if (!needUpdate && Playlist.Count > 0 && Playlist[0].Title != contextList[0].Title) needUpdate = true;

                if (needUpdate) { Playlist.Clear(); foreach (var item in contextList) Playlist.Add(item); }
            }
            else if (!Playlist.Contains(song)) { Playlist.Add(song); }

            // --- RESET TRẠNG THÁI ---
            CurrentPosition = TimeSpan.Zero;
            CurrentPositionSeconds = 0;
            if (song.Duration > 0) Duration = TimeSpan.FromSeconds(song.Duration);
            else Duration = TimeSpan.Zero;

            IsFavorite = false;
            FavoriteColor = "#A569F7";
            CurrentSong = song;

            // --- PHÁT NHẠC ---
            if (_mediaElement != null)
            {
                IsPlaying = true;
                if (!isSameSong)
                {
                    _mediaElement.Source = MediaSource.FromUri(song.AudioUrl);
                }
                _mediaElement.Play();
                WeakReferenceMessenger.Default.Send(new SongPlayedMessage(song));
            }

            // 👇👇👇 3. BỔ SUNG: LOGIC TỰ ĐỘNG TÌM LỜI BÀI HÁT 👇👇👇
            if (string.IsNullOrEmpty(CurrentSong.Lyrics))
            {
                // Báo đang tìm
                CurrentSong.Lyrics = "Đang tìm lời bài hát... ⏳";
                OnPropertyChanged(nameof(CurrentSong));

                // Chạy ngầm đi tìm
                Task.Run(async () =>
                {
                    string foundLyrics = await _lyricsService.GetLyricsAsync(CurrentSong.Title, CurrentSong.Artist);
                  
                    
                    if (!string.IsNullOrEmpty(foundLyrics))
                    {
                        CurrentSong.Lyrics = foundLyrics;
                        _ = Task.Run(() => _firestoreService.UpdateSongLyricsAsync(CurrentSong));
                        // Nếu muốn lưu lại vào DB thì gọi FirestoreService ở đây
                    }
                    else
                    {
                        CurrentSong.Lyrics = "Chưa tìm thấy lời bài hát 😔";
                    }
                    // Cập nhật giao diện
                    OnPropertyChanged(nameof(CurrentSong));
                });
            }
            // 👆👆👆 HẾT PHẦN BỔ SUNG 👆👆👆

            // --- CHECK TIM TRÊN SERVER (GIỮ NGUYÊN) ---
            Task.Run(async () =>
            {
                try
                {
                    string uid = Preferences.Get("UserId", "");
                    if (!string.IsNullOrEmpty(uid))
                    {
                        bool isLiked = await _firestoreService.IsSongInUserLibrary(uid, song);

                        if (isLiked)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                if (CurrentSong != null && CurrentSong.Title == song.Title)
                                {
                                    IsFavorite = true;
                                    FavoriteColor = "Red";
                                }
                            });
                        }
                    }
                }
                catch { }
            });
        }
        // ... (Các code cũ) ...

        // 👇👇👇 1. BIẾN ĐIỀU KHIỂN CHẾ ĐỘ SỬA LYRIC 👇👇👇
        [ObservableProperty]
        private bool _isEditingLyrics = false; // Mặc định là False (Chế độ xem)

        // 👇👇👇 2. LỆNH BẬT/TẮT CHẾ ĐỘ SỬA 👇👇👇
        [RelayCommand]
        public void ToggleEditLyrics()
        {
            IsEditingLyrics = !IsEditingLyrics;
        }

        // 👇👇👇 3. LỆNH LƯU LYRIC SAU KHI SỬA 👇👇👇
        [RelayCommand]
        public async Task SaveLyrics()
        {
            if (CurrentSong == null) return;

            // Tắt chế độ sửa, quay về chế độ xem
            IsEditingLyrics = false;

            // Gửi dữ liệu mới lên Firebase
            await _firestoreService.UpdateSongLyricsAsync(CurrentSong);

            // Thông báo nhẹ cho người dùng vui
            await Shell.Current.DisplayAlert("Thành công", "Đã cập nhật lời bài hát! 💖", "OK");

            OnPropertyChanged(nameof(CurrentSong));
        }
        // ==========================================================
        // CÁC NÚT ĐIỀU KHIỂN (GIỮ NGUYÊN)
        // ==========================================================

        [RelayCommand]
        public void PlayPause()
        {
            if (_mediaElement == null) return;
            if (IsPlaying) { _mediaElement.Pause(); IsPlaying = false; }
            else { _mediaElement.Play(); IsPlaying = true; }
        }

        [RelayCommand] public void DragStarted() => _isDraggingSlider = true;

        [RelayCommand]
        public async Task DragCompleted()
        {
            if (_mediaElement != null) await _mediaElement.SeekTo(CurrentPosition);
            await Task.Delay(100);
            _isDraggingSlider = false;
        }

        [RelayCommand] public void ToggleShuffle() => IsShuffle = !IsShuffle;

        [RelayCommand]
        public void ToggleRepeat()
        {
            RepeatMode = (RepeatMode + 1) % 3;
        }

        [RelayCommand] public async Task GoBack() => await Shell.Current.GoToAsync("..");

        [RelayCommand]
        public void Next()
        {
            if (CurrentSong == null || Playlist.Count == 0) return;

            if (RepeatMode == 1 && _mediaElement != null)
            {
                _mediaElement.SeekTo(TimeSpan.Zero);
                _mediaElement.Play();
                return;
            }

            int index = -1;
            for (int i = 0; i < Playlist.Count; i++)
            {
                if (Playlist[i].Title == CurrentSong.Title && Playlist[i].Artist == CurrentSong.Artist)
                {
                    index = i; break;
                }
            }

            if (index == -1) { PlaySong(Playlist[0]); return; }

            if (IsShuffle && Playlist.Count > 1)
            {
                var r = new Random();
                int nextIndex;
                do { nextIndex = r.Next(Playlist.Count); } while (nextIndex == index);
                PlaySong(Playlist[nextIndex]);
                return;
            }

            if (index < Playlist.Count - 1) PlaySong(Playlist[index + 1]);
            else PlaySong(Playlist[0]);
        }

        [RelayCommand]
        public void Previous()
        {
            if (CurrentSong == null || Playlist.Count == 0) return;

            if (CurrentPosition.TotalSeconds > 3 && _mediaElement != null)
            {
                _mediaElement.SeekTo(TimeSpan.Zero);
                return;
            }

            int index = -1;
            for (int i = 0; i < Playlist.Count; i++)
            {
                if (Playlist[i].Title == CurrentSong.Title && Playlist[i].Artist == CurrentSong.Artist)
                {
                    index = i; break;
                }
            }

            if (index > 0) PlaySong(Playlist[index - 1]);
            else PlaySong(Playlist[Playlist.Count - 1]);
        }

        // Logic ToggleFavorite cũ của bạn (Giữ nguyên)
        [RelayCommand]
        public async Task ToggleFavorite()
        {
            if (CurrentSong == null) return;
            string uid = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(uid)) { await Shell.Current.DisplayAlert("Yêu cầu", "Vui lòng đăng nhập!", "OK"); return; }

            if (IsFavorite) { await Shell.Current.DisplayAlert("Thông báo", "Đã có trong thư viện.", "OK"); return; }

            string action = await Shell.Current.DisplayActionSheet("Thêm vào thư viện", "Hủy", null, "Thêm vào Playlist có sẵn", "Tạo Playlist mới");

            bool addedSuccess = false;

            if (action == "Tạo Playlist mới")
            {
                string name = await Shell.Current.DisplayPromptAsync("Tạo Mới", "Tên Playlist:");
                if (!string.IsNullOrEmpty(name))
                {
                    await _firestoreService.CreatePlaylistAndAddSong(uid, name, CurrentSong);
                    addedSuccess = true;
                    await Shell.Current.DisplayAlert("Thành công", $"Đã tạo '{name}'", "OK");
                }
            }
            else if (action == "Thêm vào Playlist có sẵn")
            {
                var playlists = await _firestoreService.GetUserPlaylists(uid);
                if (playlists != null && playlists.Count > 0)
                {
                    var names = playlists.Select(p => p.Name).ToArray();
                    string sel = await Shell.Current.DisplayActionSheet("Chọn Playlist", "Hủy", null, names);
                    if (!string.IsNullOrEmpty(sel) && sel != "Hủy")
                    {
                        var p = playlists.FirstOrDefault(x => x.Name == sel);
                        if (p != null)
                        {
                            await _firestoreService.AddSongToExistingPlaylist(p.Id, CurrentSong);
                            addedSuccess = true;
                            await Shell.Current.DisplayAlert("Thành công", $"Đã thêm vào '{sel}'", "OK");
                        }
                    }
                }
                else await Shell.Current.DisplayAlert("Thông báo", "Chưa có playlist nào.", "OK");
            }

            if (addedSuccess)
            {
                IsFavorite = true;
                FavoriteColor = "Red";
                WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());
            }
        }

        public void Cleanup()
        {
            if (_mediaElement != null)
            {
                _mediaElement.Stop();
                _mediaElement.Source = null;
            }

            IsPlaying = false;
            CurrentSong = null;
            Playlist.Clear();
            CurrentPosition = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
        }

        // Lệnh Bật/Tắt Lyric
        [RelayCommand]
        public void ToggleLyrics()
        {
            IsLyricsVisible = !IsLyricsVisible;
        }
    }
}