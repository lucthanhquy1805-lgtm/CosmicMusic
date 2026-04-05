using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Models;
using CosmicMusic.Services;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace CosmicMusic.ViewModels
{
    public class RefreshLibraryMessage { }
    public class RefreshRecentlyPlayedMessage { }


    public class SongPlayedMessage
    {
        public Song PlayedSong { get; set; }
        public SongPlayedMessage(Song song) => PlayedSong = song;
    }
    public class LyricScrolledMessage
    {
        public LyricLine CurrentLine { get; set; }
        public LyricScrolledMessage(LyricLine line) => CurrentLine = line;
    }
    public class MediaControlMessage
    {
        public string Action { get; set; }
        public MediaControlMessage(string action) => Action = action;
    }

    public partial class AudioViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private readonly LyricsService _lyricsService;
        public ObservableCollection<LyricLine> SyncedLyrics { get; set; } = new();
        private MediaElement _mediaElement;
        private bool _isDraggingSlider = false;
        private bool _isNavigating = false;
        public ObservableCollection<Song> Playlist { get; set; } = new();

        public AudioViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            _lyricsService = new LyricsService();
          

            FavoriteColor = "#A569F7";
            IsFavorite = false;

            WeakReferenceMessenger.Default.Register<PlayRequestedMessage>(this, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PlaySong(m.SongToPlay);
                });
            });
            WeakReferenceMessenger.Default.Register<MediaControlMessage>(this, (r, m) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (m.Action == "PLAY_PAUSE") PlayPause();
                    else if (m.Action == "NEXT") Next();
                    else if (m.Action == "PREV") Previous();

               
                    else if (m.Action != null && m.Action.StartsWith("SEEK:"))
                    {
                        if (long.TryParse(m.Action.Substring(5), out long ms))
                        {
                            SeekFromBackground(ms);
                        }
                    }
                });
            });
        }
        private bool _isLrcLyrics = false;
        [ObservableProperty] private Song _currentSong;
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(TotalDurationText))] private TimeSpan _duration;
        [ObservableProperty] private bool _isRepeat;
        public string TotalDurationText => $"{Duration:mm\\:ss}";
        [ObservableProperty][NotifyPropertyChangedFor(nameof(CurrentPositionText))] private TimeSpan _currentPosition;
        public string CurrentPositionText => $"{CurrentPosition:mm\\:ss}";
        [ObservableProperty] private double _volume = 1.0;

        [ObservableProperty] private bool _isFavorite;
        [ObservableProperty] private string _favoriteColor;
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

        partial void OnRepeatModeChanged(int value) => IsRepeat = value > 0;

        
        [ObservableProperty]
        private double _lyricsOffset = 0;

        private int _currentLyricIndex = -1;

        [RelayCommand]
        public void LyricsFaster()
        {
            LyricsOffset += 0.5; 
            ForceRefreshLyrics();
        }
        


        [RelayCommand]
        public void LyricsSlower()
        {
            LyricsOffset -= 0.5; 
            ForceRefreshLyrics();
        }
        private void ForceRefreshLyrics()
        {
            _currentLyricIndex = -1; 
            foreach (var line in SyncedLyrics) line.IsCurrent = false; 
            SyncLyricWithTime(CurrentPosition); 
        }
        
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
                        _mediaElement.SeekTo(CurrentPosition);

                    if (IsPlaying)
                        _mediaElement.Play();
                }
            }
        }

        private void OnMediaOpened(object sender, EventArgs e)
        {
            if (_mediaElement != null) Duration = _mediaElement.Duration;

           
            UpdateAndroidService();
        }

        private void OnPositionChanged(object sender, MediaPositionChangedEventArgs e)
        {
            if (_isDraggingSlider) return;
            CurrentPosition = e.Position;
            OnPropertyChanged(nameof(CurrentPositionSeconds));
            SyncLyricWithTime(e.Position);

            if (_mediaElement != null && _mediaElement.Duration > TimeSpan.Zero && Duration != _mediaElement.Duration)
            {
                Duration = _mediaElement.Duration;
            }
        }

        private void OnMediaEnded(object sender, EventArgs e) => Next();

        public void PlaySong(Song song, ObservableCollection<Song>? contextList = null)
        {
            if (song == null) return;
            bool isCurrentVip = Preferences.Get("IsPremium", false);
            if (song.IsPremium && !isCurrentVip)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    bool answer = await Shell.Current.DisplayAlert("Premium Content 👑", $"Bài hát '{song.Title}' chỉ dành cho tài khoản VIP. Bạn có muốn nâng cấp không?", "Xem gói VIP", "Bỏ qua");
                    if (answer)
                    {
                        await Shell.Current.GoToAsync("PremiumPage"); 
                    }
                });

                
                if (contextList != null && contextList.Count > 1)
                {
                    Next();
                }
                return; 
            }

            bool isSameSong = (CurrentSong != null && CurrentSong.Title == song.Title);
            if (isSameSong && IsPlaying) return;

            if (contextList != null && contextList.Count > 0)
            {
                bool needUpdate = Playlist.Count != contextList.Count;
                if (!needUpdate && Playlist.Count > 0 && Playlist[0].Title != contextList[0].Title) needUpdate = true;
                if (needUpdate) { Playlist.Clear(); foreach (var item in contextList) Playlist.Add(item); }
            }
            else if (!Playlist.Contains(song)) { Playlist.Add(song); }

            CurrentPosition = TimeSpan.Zero;
            CurrentPositionSeconds = 0;
            LyricsOffset = 0;
            _currentLyricIndex = -1;
            Duration = song.Duration > 0 ? TimeSpan.FromSeconds(song.Duration) : TimeSpan.Zero;
            LyricsOffset = 0;
            IsFavorite = false;
            FavoriteColor = "#A569F7";
            CurrentSong = song;

            if (_mediaElement != null)
            {
                IsPlaying = true;
                if (!isSameSong) _mediaElement.Source = MediaSource.FromUri(song.AudioUrl);
                _mediaElement.Play();
                WeakReferenceMessenger.Default.Send(new SongPlayedMessage(song));

                
                UpdateAndroidService();
                Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    MainThread.BeginInvokeOnMainThread(() => UpdateAndroidService());
                });
            }

            
            if (!string.IsNullOrEmpty(CurrentSong.Lyrics))
            {
                ParseLrcLyrics(CurrentSong.Lyrics);
            }

            if (string.IsNullOrEmpty(CurrentSong.Lyrics))
            {
                CurrentSong.Lyrics = "Đang tìm lời bài hát... ⏳";
                OnPropertyChanged(nameof(CurrentSong));

                Task.Run(async () =>
                {
                  
                    string dbLyrics = await _firestoreService.GetLyricsFromDatabaseAsync(CurrentSong.Id);

                    if (!string.IsNullOrEmpty(dbLyrics))
                    {
                      
                        CurrentSong.Lyrics = dbLyrics;
                        ParseLrcLyrics(dbLyrics);
                    }
                    else
                    {
                       
                        string foundLyrics = await _lyricsService.GetLyricsAsync(CurrentSong.Title, CurrentSong.Artist);
                        if (!string.IsNullOrEmpty(foundLyrics))
                        {
                            CurrentSong.Lyrics = foundLyrics;
                            ParseLrcLyrics(foundLyrics);

                            _ = Task.Run(() => _firestoreService.UpdateSongLyricsAsync(CurrentSong));
                        }
                        else
                        {
                            CurrentSong.Lyrics = "Chưa tìm thấy lời bài hát 😔";
                            MainThread.BeginInvokeOnMainThread(() => SyncedLyrics.Clear());
                        }
                    }
                    OnPropertyChanged(nameof(CurrentSong));
                });
            }

           
            Task.Run(async () =>
            {
                try
                {
                    bool isLiked = await _firestoreService.IsSongInFavoritesAsync(song);
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
                catch { }
            });

            string uid = Preferences.Get("UserId", "");
            if (!string.IsNullOrEmpty(uid))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        
                        await _firestoreService.AddToRecentlyPlayedAsync(uid, song);

                        
                        WeakReferenceMessenger.Default.Send(new RefreshRecentlyPlayedMessage());

                       
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Lỗi lưu Nghe gần đây: {ex.Message}");
                    }
                });
            }
        }

        [RelayCommand]
        public async Task ToggleFavorite()
        {
            if (CurrentSong == null) return;
            string uid = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(uid))
            {
                await Shell.Current.DisplayAlert("Yêu cầu", "Vui lòng đăng nhập!", "OK");
                return;
            }

          
            IsFavorite = !IsFavorite;
            FavoriteColor = IsFavorite ? "Red" : "#A569F7";

            try
            {
                if (IsFavorite)
                {
                 
                    await _firestoreService.AddToFavoritesAsync(CurrentSong);

              
                    CurrentSong.LikeCount++;
                    _ = _firestoreService.UpdateGlobalLikeCount(CurrentSong, 1);
                }
                else
                {
                
                    await _firestoreService.RemoveFromFavoritesAsync(CurrentSong);

                
                    CurrentSong.LikeCount = Math.Max(0, CurrentSong.LikeCount - 1);
                    _ = _firestoreService.UpdateGlobalLikeCount(CurrentSong, -1);
                }

                WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());
            }
            catch (Exception ex)
            {
              
                IsFavorite = !IsFavorite;
                FavoriteColor = IsFavorite ? "Red" : "#A569F7";
                System.Diagnostics.Debug.WriteLine($"Lỗi Thả Tim: {ex.Message}");
            }
        }

        [ObservableProperty] private bool _isEditingLyrics = false;
        [ObservableProperty] private string _editLyricsText;
        [RelayCommand]
        public void ToggleEditLyrics()
        {
            IsEditingLyrics = !IsEditingLyrics;
            if (IsEditingLyrics) EditLyricsText = CurrentSong?.Lyrics; 
        }
        [RelayCommand]
        public async Task SaveLyrics()
        {
            if (CurrentSong == null) return;
            IsEditingLyrics = false;

        
            CurrentSong.Lyrics = EditLyricsText;

          
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ParseLrcLyrics(CurrentSong.Lyrics);

               
                var tempSong = CurrentSong;
                CurrentSong = null;
                CurrentSong = tempSong;
            });

        
            bool success = await _firestoreService.UpdateSongLyricsAsync(CurrentSong);
            if (success)
                await Shell.Current.DisplayAlert("Thành công", "Đã cập nhật lời bài hát! 💖", "OK");
            else
                await Shell.Current.DisplayAlert("Lỗi", "Không thể lưu. Vui lòng thử lại!", "OK");
        }

        [RelayCommand]
        public void PlayPause()
        {
            if (_mediaElement == null) return;
            if (IsPlaying) { _mediaElement.Pause(); IsPlaying = false; }
            else { _mediaElement.Play(); IsPlaying = true; }

            UpdateAndroidService();
        }
        [RelayCommand] public void DragStarted() => _isDraggingSlider = true;
        [RelayCommand]
        public async Task DragCompleted()
        {
            if (_mediaElement != null)
            {
                await _mediaElement.SeekTo(CurrentPosition);
                await Task.Delay(200); 
            }
            _isDraggingSlider = false;
            SyncLyricWithTime(CurrentPosition); 
            UpdateAndroidService();
        }
        [RelayCommand] public void ToggleShuffle() => IsShuffle = !IsShuffle;
        [RelayCommand] public void ToggleRepeat() => RepeatMode = (RepeatMode + 1) % 3;


        
        [RelayCommand]
        public async Task GoBack()
        {
            if (_isNavigating) return;
            _isNavigating = true;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    
                    await Shell.Current.Navigation.PopAsync();
                }
                catch
                {
                   
                    try { await Shell.Current.GoToAsync(".."); } catch { }
                }
            });

            await Task.Delay(500);
            _isNavigating = false;
        }
        [RelayCommand]
        public void Next()
        {
            if (CurrentSong == null || Playlist.Count == 0) return;
            if (RepeatMode == 1 && _mediaElement != null) { _mediaElement.SeekTo(TimeSpan.Zero); _mediaElement.Play(); return; }

            int index = -1;
            for (int i = 0; i < Playlist.Count; i++)
                if (Playlist[i].Title == CurrentSong.Title && Playlist[i].Artist == CurrentSong.Artist) { index = i; break; }

            if (index == -1) { PlaySong(Playlist[0]); return; }
            if (IsShuffle && Playlist.Count > 1)
            {
                var r = new Random(); int nextIndex;
                do { nextIndex = r.Next(Playlist.Count); } while (nextIndex == index);
                PlaySong(Playlist[nextIndex]); return;
            }
            if (index < Playlist.Count - 1) PlaySong(Playlist[index + 1]);
            else PlaySong(Playlist[0]);
        }

        [RelayCommand]
        public void Previous()
        {
            if (CurrentSong == null || Playlist.Count == 0) return;
            if (CurrentPosition.TotalSeconds > 3 && _mediaElement != null) { _mediaElement.SeekTo(TimeSpan.Zero); return; }

            int index = -1;
            for (int i = 0; i < Playlist.Count; i++)
                if (Playlist[i].Title == CurrentSong.Title && Playlist[i].Artist == CurrentSong.Artist) { index = i; break; }

            if (index > 0) PlaySong(Playlist[index - 1]);
            else PlaySong(Playlist[Playlist.Count - 1]);
        }

        public void Cleanup()
        {
            if (_mediaElement != null) { _mediaElement.Stop(); _mediaElement.Source = null; }
            IsPlaying = false; CurrentSong = null; Playlist.Clear();
            CurrentPosition = TimeSpan.Zero; Duration = TimeSpan.Zero;

        
            UpdateAndroidService();
        }

        [RelayCommand] public void ToggleLyrics() => IsLyricsVisible = !IsLyricsVisible;
       
        [RelayCommand]
        public async Task AddToPlaylist()
        {
            if (CurrentSong == null) return;
            string uid = Preferences.Get("UserId", "");
            if (string.IsNullOrEmpty(uid))
            {
                await Shell.Current.DisplayAlert("Yêu cầu", "Vui lòng đăng nhập!", "OK");
                return;
            }

          
            string action = await Shell.Current.DisplayActionSheet("Lưu bài hát", "Hủy", null, "Thêm vào Playlist có sẵn", "Tạo Playlist mới");

            if (action == "Tạo Playlist mới")
            {
                string name = await Shell.Current.DisplayPromptAsync("Tạo Mới", "Nhập tên Playlist:");
                if (!string.IsNullOrEmpty(name))
                {
                    await _firestoreService.CreatePlaylistAndAddSong(uid, name, CurrentSong);
                    await Shell.Current.DisplayAlert("Thành công", $"Đã tạo và thêm vào '{name}'", "OK");

                
                    WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());
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
                            await Shell.Current.DisplayAlert("Thành công", $"Đã thêm vào '{sel}'", "OK");
                            WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());
                        }
                    }
                }
                else
                {
                    await Shell.Current.DisplayAlert("Thông báo", "Chưa có playlist nào. Hãy tạo mới!", "OK");
                }
            }
        }
       
        private void ParseLrcLyrics(string rawLyrics)
        {
            MainThread.BeginInvokeOnMainThread(() => SyncedLyrics.Clear());
            if (string.IsNullOrEmpty(rawLyrics)) return;

            var parsedLyrics = new List<LyricLine>();
            _isLrcLyrics = false; 
            string normalizedLyrics = rawLyrics.Replace("[", "\n[");
            var lines = normalizedLyrics.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimLine = line.Trim();
                if (string.IsNullOrEmpty(trimLine)) continue;

                int startBracket = trimLine.IndexOf('[');
                int endBracket = trimLine.IndexOf(']');

                if (startBracket >= 0 && endBracket > startBracket)
                {
                    string timeStr = trimLine.Substring(startBracket + 1, endBracket - startBracket - 1);
                    string textStr = trimLine.Substring(endBracket + 1).Trim();

                    var timeParts = timeStr.Split(':');
                    if (timeParts.Length >= 2 &&
                        double.TryParse(timeParts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double min) &&
                        double.TryParse(timeParts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double sec))
                    {
                        parsedLyrics.Add(new LyricLine { Time = TimeSpan.FromSeconds(min * 60 + sec), Text = textStr, IsCurrent = false });
                        _isLrcLyrics = true; 
                    }
                    else
                    {
                        parsedLyrics.Add(new LyricLine { Time = TimeSpan.Zero, Text = trimLine, IsCurrent = false });
                    }
                }
                else 
                {
                    parsedLyrics.Add(new LyricLine { Time = TimeSpan.Zero, Text = trimLine, IsCurrent = false });
                }
            }

          
            if (_isLrcLyrics)
            {
                parsedLyrics = parsedLyrics.OrderBy(x => x.Time).ToList();
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var item in parsedLyrics) SyncedLyrics.Add(item);

                
                if (parsedLyrics.Count > 0)
                {
                    WeakReferenceMessenger.Default.Send(new LyricScrolledMessage(parsedLyrics[0]));
                }
            });
        }

        private void SyncLyricWithTime(TimeSpan currentPosition)
        {
            if (!_isLrcLyrics || SyncedLyrics.Count == 0) return;

            double effectiveSeconds = currentPosition.TotalSeconds + LyricsOffset;
            if (effectiveSeconds < 0) effectiveSeconds = 0;
            TimeSpan effectivePosition = TimeSpan.FromSeconds(effectiveSeconds);

            int newIndex = -1;

          
            for (int i = 0; i < SyncedLyrics.Count; i++)
            {
                if (effectivePosition >= SyncedLyrics[i].Time)
                {
                    newIndex = i;
                }
                else
                {
                    break; 
                }
            }

         
            if (newIndex != _currentLyricIndex && newIndex != -1)
            {
              
                if (_currentLyricIndex >= 0 && _currentLyricIndex < SyncedLyrics.Count)
                {
                    SyncedLyrics[_currentLyricIndex].IsCurrent = false;
                }

             
                SyncedLyrics[newIndex].IsCurrent = true;
                _currentLyricIndex = newIndex;

              
                WeakReferenceMessenger.Default.Send(new LyricScrolledMessage(SyncedLyrics[newIndex]));
            }
        }
        [RelayCommand]
        private async Task SeekToLyric(LyricLine selectedLyric)
        {
            if (selectedLyric == null || _mediaElement == null) return;

           
            double targetSeconds = selectedLyric.Time.TotalSeconds - LyricsOffset;

            if (targetSeconds < 0) targetSeconds = 0;

            await _mediaElement.SeekTo(TimeSpan.FromSeconds(targetSeconds));

            CurrentPosition = TimeSpan.FromSeconds(targetSeconds);
            OnPropertyChanged(nameof(CurrentPositionSeconds));
        }

        public async void SeekFromBackground(long ms)
        {
            if (_mediaElement != null)
            {
                var newPos = TimeSpan.FromMilliseconds(ms);
                await _mediaElement.SeekTo(newPos);
                CurrentPosition = newPos;         
                SyncLyricWithTime(newPos);
                UpdateAndroidService();
            }
        }
        public void UpdateAndroidService()
        {
#if ANDROID
            try
            {
                if (CurrentSong == null) return;
                var context = global::Android.App.Application.Context;
                var intent = new global::Android.Content.Intent(context, typeof(CosmicMusic.Platforms.Android.MusicService));

               
                intent.PutExtra("title", CurrentSong.Title);
                intent.PutExtra("artist", CurrentSong.Artist);
                intent.PutExtra("isPlaying", IsPlaying);

                long durationMs = Duration.TotalMilliseconds > 0 ? (long)Duration.TotalMilliseconds : (long)(CurrentSong.Duration * 1000);
                intent.PutExtra("duration", durationMs);
                intent.PutExtra("position", (long)(CurrentPosition.TotalMilliseconds));

                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
                    context.StartForegroundService(intent);
                else
                    context.StartService(intent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi Update Android Service: {ex.Message}");
            }
#endif
        }

    }
}