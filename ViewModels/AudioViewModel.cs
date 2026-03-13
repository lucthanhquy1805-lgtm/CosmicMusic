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
    public class LyricScrolledMessage
    {
        public LyricLine CurrentLine { get; set; }
        public LyricScrolledMessage(LyricLine line) => CurrentLine = line;
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
        }
        private bool _isLrcLyrics = false;

        // ==========================================================
        // CÁC THUỘC TÍNH (GIỮ NGUYÊN CỦA BẠN)
        // ==========================================================
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

        // ==========================================================
        // 👇 BỔ SUNG TÍNH NĂNG ĐỒNG BỘ LỜI NHẠC (OFFSET) 👇
        // ==========================================================
        [ObservableProperty]
        private double _lyricsOffset = 0;

        private int _currentLyricIndex = -1;

        [RelayCommand]
        public void LyricsFaster()
        {
            LyricsOffset += 0.5; // Kéo lời nhanh lên
            ForceRefreshLyrics();
        }

        [RelayCommand]
        public void LyricsSlower()
        {
            LyricsOffset -= 0.5; // Kéo lời chậm lại
            ForceRefreshLyrics();
        }
        private void ForceRefreshLyrics()
        {
            _currentLyricIndex = -1; // Reset bộ nhớ
            foreach (var line in SyncedLyrics) line.IsCurrent = false; // Tắt hết màu
            SyncLyricWithTime(CurrentPosition); // Quét và tính toán lại ngay lập tức
        }
        // ==========================================================


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
                        _mediaElement.SeekTo(CurrentPosition);

                    if (IsPlaying)
                        _mediaElement.Play();
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
            SyncLyricWithTime(e.Position);

            if (_mediaElement != null && _mediaElement.Duration > TimeSpan.Zero && Duration != _mediaElement.Duration)
            {
                Duration = _mediaElement.Duration;
            }
        }

        private void OnMediaEnded(object sender, EventArgs e) => Next();


        // ==========================================================
        // 4. HÀM PHÁT NHẠC (ĐÃ FIX LỖI LOAD LYRIC CÓ SẴN)
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

            CurrentPosition = TimeSpan.Zero;
            CurrentPositionSeconds = 0;
            LyricsOffset = 0;
            _currentLyricIndex = -1;
            Duration = song.Duration > 0 ? TimeSpan.FromSeconds(song.Duration) : TimeSpan.Zero;

            // 👇 BỔ SUNG: Reset lại độ lệch Lyrics về 0 mỗi khi qua bài hát mới
            LyricsOffset = 0;

            // Reset Tim
            IsFavorite = false;
            FavoriteColor = "#A569F7";
            CurrentSong = song;

            if (_mediaElement != null)
            {
                IsPlaying = true;
                if (!isSameSong) _mediaElement.Source = MediaSource.FromUri(song.AudioUrl);
                _mediaElement.Play();
                WeakReferenceMessenger.Default.Send(new SongPlayedMessage(song));
            }

            // 👇👇 BỔ SUNG QUAN TRỌNG: Nếu bài hát ĐÃ CÓ SẴN LỜI (Từ Firebase), thì nạp vào Karaoke luôn!
            if (!string.IsNullOrEmpty(CurrentSong.Lyrics))
            {
                ParseLrcLyrics(CurrentSong.Lyrics);
            }

            // Tìm Lyric (Nếu bài hát CHƯA CÓ LỜI)
            // CƠ CHẾ TÌM LYRIC THÔNG MINH MỚI
            if (string.IsNullOrEmpty(CurrentSong.Lyrics))
            {
                CurrentSong.Lyrics = "Đang tìm lời bài hát... ⏳";
                OnPropertyChanged(nameof(CurrentSong));

                Task.Run(async () =>
                {
                    // 1. ƯU TIÊN 1: Tìm trong Firebase xem có bản LRC nào xịn do User lưu không
                    string dbLyrics = await _firestoreService.GetLyricsFromDatabaseAsync(CurrentSong.Id);

                    if (!string.IsNullOrEmpty(dbLyrics))
                    {
                        // Nếu có, nạp ngay vào màn hình
                        CurrentSong.Lyrics = dbLyrics;
                        ParseLrcLyrics(dbLyrics);
                    }
                    else
                    {
                        // 2. NẾU TRỐNG: Mới cầu cứu API mạng
                        string foundLyrics = await _lyricsService.GetLyricsAsync(CurrentSong.Title, CurrentSong.Artist);
                        if (!string.IsNullOrEmpty(foundLyrics))
                        {
                            CurrentSong.Lyrics = foundLyrics;
                            ParseLrcLyrics(foundLyrics);

                            // CHỈ LƯU TỰ ĐỘNG NẾU DATABASE TRỐNG HOÀN TOÀN
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

            // Check Tim từ ROOT Collection Favorites
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
                Task.Run(() => _firestoreService.AddToRecentlyPlayedAsync(uid, song));
            }
        }

        // ==========================================================
        // 👇👇 5. ĐÃ SỬA: LOGIC NÚT THẢ TIM HOÀN TOÀN MỚI 👇👇
        // ==========================================================
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

            // 1. Giao diện thay đổi ngay lập tức để người dùng không phải chờ
            IsFavorite = !IsFavorite;
            FavoriteColor = IsFavorite ? "Red" : "#A569F7";

            try
            {
                if (IsFavorite)
                {
                    // LƯU VÀO YÊU THÍCH (Root Collection)
                    await _firestoreService.AddToFavoritesAsync(CurrentSong);

                    // Tăng Like Global
                    CurrentSong.LikeCount++;
                    _ = _firestoreService.UpdateGlobalLikeCount(CurrentSong, 1);
                }
                else
                {
                    // XÓA KHỎI YÊU THÍCH
                    await _firestoreService.RemoveFromFavoritesAsync(CurrentSong);

                    // Giảm Like Global
                    CurrentSong.LikeCount = Math.Max(0, CurrentSong.LikeCount - 1);
                    _ = _firestoreService.UpdateGlobalLikeCount(CurrentSong, -1);
                }

                // Gửi thông báo để trang Library tự động tải lại
                WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());
            }
            catch (Exception ex)
            {
                // Nếu lỗi, trả lại giao diện như cũ
                IsFavorite = !IsFavorite;
                FavoriteColor = IsFavorite ? "Red" : "#A569F7";
                System.Diagnostics.Debug.WriteLine($"Lỗi Thả Tim: {ex.Message}");
            }
        }

        // Các lệnh khác giữ nguyên...
        [ObservableProperty] private bool _isEditingLyrics = false;
        [ObservableProperty] private string _editLyricsText;
        [RelayCommand]
        public void ToggleEditLyrics()
        {
            IsEditingLyrics = !IsEditingLyrics;
            if (IsEditingLyrics) EditLyricsText = CurrentSong?.Lyrics; // Mở lên thì nạp chữ cũ vào
        }
        [RelayCommand]
        public async Task SaveLyrics()
        {
            if (CurrentSong == null) return;
            IsEditingLyrics = false;

            // Ép nhận chữ mới nhất từ giao diện
            CurrentSong.Lyrics = EditLyricsText;

            // Dùng Dispatcher để đảm bảo UI thực sự "vẽ" lại chữ mới ngay lập tức
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ParseLrcLyrics(CurrentSong.Lyrics);

                // Mẹo nhỏ: Báo cho UI biết là biến CurrentSong đã thay đổi hoàn toàn
                var tempSong = CurrentSong;
                CurrentSong = null;
                CurrentSong = tempSong;
            });

            // Gửi lên Firebase
            bool success = await _firestoreService.UpdateSongLyricsAsync(CurrentSong);
            if (success)
                await Shell.Current.DisplayAlert("Thành công", "Đã cập nhật lời bài hát! 💖", "OK");
            else
                await Shell.Current.DisplayAlert("Lỗi", "Không thể lưu. Vui lòng thử lại!", "OK");
        }

        [RelayCommand] public void PlayPause() { if (_mediaElement == null) return; if (IsPlaying) { _mediaElement.Pause(); IsPlaying = false; } else { _mediaElement.Play(); IsPlaying = true; } }
        [RelayCommand] public void DragStarted() => _isDraggingSlider = true;
        [RelayCommand]
        public async Task DragCompleted()
        {
            if (_mediaElement != null)
            {
                await _mediaElement.SeekTo(CurrentPosition);
                await Task.Delay(200); // Chờ nhạc load 1 nhịp để bắt time cho chuẩn
            }
            _isDraggingSlider = false;
            SyncLyricWithTime(CurrentPosition); // Bắt lại đúng câu hát đó
        }
        [RelayCommand] public void ToggleShuffle() => IsShuffle = !IsShuffle;
        [RelayCommand] public void ToggleRepeat() => RepeatMode = (RepeatMode + 1) % 3;


        // ==========================================================
        // LỆNH LÙI TRANG (ÉP RÚT TRANG VẬT LÝ - BYPASS LỖI SHELL)
        // ==========================================================
        [RelayCommand]
        public async Task GoBack()
        {
            if (_isNavigating) return;
            _isNavigating = true;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    // Lệnh Pop vật lý tuyệt đối: Không kiểm tra điều kiện, 
                    // ép hệ thống gỡ trang hiện tại ra khỏi màn hình ngay lập tức!
                    await Shell.Current.Navigation.PopAsync();
                }
                catch
                {
                    // Phương án dự phòng cuối cùng
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
        }

        [RelayCommand] public void ToggleLyrics() => IsLyricsVisible = !IsLyricsVisible;
        // ==========================================================
        // TÍNH NĂNG THÊM VÀO PLAYLIST CÁ NHÂN
        // ==========================================================
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

            // Hiện menu chọn
            string action = await Shell.Current.DisplayActionSheet("Lưu bài hát", "Hủy", null, "Thêm vào Playlist có sẵn", "Tạo Playlist mới");

            if (action == "Tạo Playlist mới")
            {
                string name = await Shell.Current.DisplayPromptAsync("Tạo Mới", "Nhập tên Playlist:");
                if (!string.IsNullOrEmpty(name))
                {
                    await _firestoreService.CreatePlaylistAndAddSong(uid, name, CurrentSong);
                    await Shell.Current.DisplayAlert("Thành công", $"Đã tạo và thêm vào '{name}'", "OK");

                    // Báo cho trang Library load lại
                    WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());
                }
            }
            else if (action == "Thêm vào Playlist có sẵn")
            {
                // Gọi Firebase lấy danh sách list của User
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
        // ==========================================================
        // XỬ LÝ LYRIC CHẠY THEO THỜI GIAN (KARAOKE)
        // ==========================================================
        private void ParseLrcLyrics(string rawLyrics)
        {
            MainThread.BeginInvokeOnMainThread(() => SyncedLyrics.Clear());
            if (string.IsNullOrEmpty(rawLyrics)) return;

            var parsedLyrics = new List<LyricLine>();
            _isLrcLyrics = false; // Mặc định là chữ thường

            // 👇 CHUẨN HÓA THÔNG MINH: Xuống dòng trước dấu '[' để trị lỗi dính chữ từ Firebase,
            // nhưng VẪN GIỮ NGUYÊN các dấu xuống dòng của bài hát chữ thường.
            string normalizedLyrics = rawLyrics.Replace("[", "\n[");
            var lines = normalizedLyrics.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimLine = line.Trim();
                if (string.IsNullOrEmpty(trimLine)) continue;

                int startBracket = trimLine.IndexOf('[');
                int endBracket = trimLine.IndexOf(']');

                // Nếu có dấu thời gian
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
                        _isLrcLyrics = true; // Bật cờ hiệu: Đây là nhạc Karaoke!
                    }
                    else
                    {
                        parsedLyrics.Add(new LyricLine { Time = TimeSpan.Zero, Text = trimLine, IsCurrent = false });
                    }
                }
                else // Dòng chữ thường
                {
                    parsedLyrics.Add(new LyricLine { Time = TimeSpan.Zero, Text = trimLine, IsCurrent = false });
                }
            }

            // 👇 NẾU LÀ KARAOKE thì sắp xếp thời gian. NẾU LÀ CHỮ THƯỜNG thì giữ nguyên gốc của văn bản
            if (_isLrcLyrics)
            {
                parsedLyrics = parsedLyrics.OrderBy(x => x.Time).ToList();
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var item in parsedLyrics) SyncedLyrics.Add(item);

                // MẸO: Ngay khi load xong chữ, lập tức gửi thư báo giao diện kéo lên DÒNG ĐẦU TIÊN
                if (parsedLyrics.Count > 0)
                {
                    WeakReferenceMessenger.Default.Send(new LyricScrolledMessage(parsedLyrics[0]));
                }
            });
        }

        private void SyncLyricWithTime(TimeSpan currentPosition)
        {
            if (!_isLrcLyrics || SyncedLyrics.Count == 0) return;

            // 1. Tính thời gian đã bù trừ Offset (Không để bị âm)
            double effectiveSeconds = currentPosition.TotalSeconds + LyricsOffset;
            if (effectiveSeconds < 0) effectiveSeconds = 0;
            TimeSpan effectivePosition = TimeSpan.FromSeconds(effectiveSeconds);

            int newIndex = -1;

            // 2. Quét tìm câu hiện tại (Chỉ lấy câu cuối cùng thỏa mãn điều kiện)
            for (int i = 0; i < SyncedLyrics.Count; i++)
            {
                if (effectivePosition >= SyncedLyrics[i].Time)
                {
                    newIndex = i;
                }
                else
                {
                    break; // Tối ưu: Vượt quá thời gian thì dừng luôn không quét nữa
                }
            }

            // 3. Nếu câu hát bị thay đổi (Bật màu câu mới, tắt câu cũ)
            if (newIndex != _currentLyricIndex && newIndex != -1)
            {
                // Tắt câu cũ
                if (_currentLyricIndex >= 0 && _currentLyricIndex < SyncedLyrics.Count)
                {
                    SyncedLyrics[_currentLyricIndex].IsCurrent = false;
                }

                // Bật câu mới sáng lên
                SyncedLyrics[newIndex].IsCurrent = true;
                _currentLyricIndex = newIndex;

                // Ra lệnh cuộn giao diện đến đúng câu đó
                WeakReferenceMessenger.Default.Send(new LyricScrolledMessage(SyncedLyrics[newIndex]));
            }
        }
    }
}