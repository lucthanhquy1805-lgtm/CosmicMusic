using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace CosmicMusic.ViewModels
{
    public partial class AlbumDetailViewModel : ObservableObject, IQueryAttributable
    {
        private readonly FirestoreService _firestoreService;

     
        [ObservableProperty] private AudioViewModel _audioPlayer;
        [ObservableProperty] private bool _isBusy;
       

        [ObservableProperty] private string _coverImage;
        [ObservableProperty] private string _mainTitle;
        [ObservableProperty] private string _subTitle;
        [ObservableProperty] private bool _isAlbumType;
        [ObservableProperty] private bool _canEdit = false;

        private string _currentId;    
        private string _currentType;   
        private Album _receivedAlbum;  

        public ObservableCollection<Song> Songs { get; } = new();

        public AlbumDetailViewModel(FirestoreService firestoreService, AudioViewModel audioPlayer)
        {
            _firestoreService = firestoreService;
            AudioPlayer = audioPlayer;
        }

       
        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            Songs.Clear(); 

          
            if (query.ContainsKey("Type") && query["Type"].ToString() == "Favorites")
            {
                _currentType = "Favorites";
                MainTitle = "Bài hát đã thích";
                CoverImage = "https://misc.scdn.co/liked-songs/liked-songs-300.png";
                SubTitle = "Danh sách yêu thích của bạn";
                IsAlbumType = false;

                await LoadFavoriteSongs();
            }
           
            else if (query.ContainsKey("AlbumData"))
            {
                _receivedAlbum = query["AlbumData"] as Album;
                if (_receivedAlbum != null)
                {
                    MainTitle = _receivedAlbum.Title;
                    CoverImage = _receivedAlbum.CoverImage;

                   
                    if (_receivedAlbum.Description == "Artist")
                    {
                        _currentType = "Artist";
                        SubTitle = "Nghệ sĩ";
                    }
                    else
                    {
                        _currentType = "Album";
                        SubTitle = $"Album • {_receivedAlbum.Artist}";
                    }

                    IsAlbumType = true;
                    await LoadSongsFromGlobal();
                }
            }
           
            else if (query.ContainsKey("Id"))
            {
                _currentId = query["Id"].ToString();
                _currentType = "Playlist";

                MainTitle = query.ContainsKey("Name") ? query["Name"].ToString() : "Playlist";
                CoverImage = query.ContainsKey("Image") ? query["Image"].ToString() : "";
                string count = query.ContainsKey("Description") ? query["Description"].ToString() : "0";
                SubTitle = $"Playlist • {count} songs";

                IsAlbumType = false;
                await LoadSongsFromPlaylist();
            }
            _canEdit = Preferences.Get("IsAdmin", false) &&
             (_currentType == "Album" || _currentType == "Artist");
            OnPropertyChanged(nameof(CanEdit));
        }
     
        [RelayCommand]
        public async Task EditAlbum()
        {
            if (_receivedAlbum == null) return;

            bool isArtist = _currentType == "Artist"; // ← Key point

            string newTitle = await Shell.Current.DisplayPromptAsync(
                title: isArtist ? "Chỉnh sửa Nghệ sĩ" : "Chỉnh sửa Album",
                message: isArtist ? "Nhập tên mới cho nghệ sĩ:" : "Nhập tên mới cho album:",
                accept: "Lưu",
                cancel: "Hủy",
                initialValue: MainTitle,
                maxLength: 80,
                keyboard: Keyboard.Default);

            if (string.IsNullOrWhiteSpace(newTitle)) return;
            if (newTitle.Trim() == MainTitle) return;

            IsBusy = true;
            try
            {
                bool success = isArtist
                    ? await _firestoreService.UpdateArtistNameAsync(_receivedAlbum.Id, newTitle.Trim())
                    : await _firestoreService.UpdateAlbumTitleAsync(_receivedAlbum.Id, newTitle.Trim());

                if (success)
                {
                    MainTitle = newTitle.Trim();
                    _receivedAlbum.Title = newTitle.Trim();
                    await Shell.Current.DisplayAlert("✅ Thành công",
                        isArtist ? "Đã cập nhật tên nghệ sĩ!" : "Đã cập nhật tên album!", "OK");
                }
                else
                    await Shell.Current.DisplayAlert("Lỗi", "Không thể cập nhật. Thử lại!", "OK");
            }
            finally { IsBusy = false; }
        }

        

        private async Task LoadFavoriteSongs()
        {
            IsBusy = true;
            try
            {
                var favSongs = await _firestoreService.GetFavoritesAsync();
                foreach (var s in favSongs) Songs.Add(s);
                SubTitle = $"Yêu thích • {Songs.Count} bài hát";
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            finally { IsBusy = false; }
        }

        private async Task LoadSongsFromPlaylist()
        {
            if (string.IsNullOrEmpty(_currentId)) return;
            IsBusy = true;
            try
            {
                var fetchedSongs = await _firestoreService.GetSongsFromPlaylist(_currentId);
                foreach (var s in fetchedSongs) Songs.Add(s);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
            finally { IsBusy = false; }
        }

        private async Task LoadSongsFromGlobal()
        {
            IsBusy = true;
            try
            {
                Songs.Clear();
                int totalLikes = 0;
                List<Song> fetchedSongs = new List<Song>();

                if (_currentType == "Artist")
                {
                    
                    fetchedSongs = await _firestoreService.GetSongsByArtistIdAsync(_receivedAlbum.Id);
                }
                else if (_currentType == "Album")
                {
                    
                    fetchedSongs = await _firestoreService.GetSongsByAlbumIdAsync(_receivedAlbum.Id);
                }

                if (fetchedSongs != null)
                {
                    foreach (var s in fetchedSongs)
                    {
                        Songs.Add(s);
                        totalLikes += s.LikeCount;
                    }
                }

                UpdateTotalLikesSubtitle();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Lỗi LoadSongsFromGlobal: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        
        private void UpdateTotalLikesSubtitle()
        {
            int totalLikes = 0;
            foreach (var s in Songs) totalLikes += s.LikeCount;

            if (_currentType == "Album" && _receivedAlbum != null)
            {
                SubTitle = $"Album • {_receivedAlbum.Artist} • {Songs.Count} bài\n❤️ {totalLikes} lượt thích";
            }
            else if (_currentType == "Artist")
            {
                SubTitle = $"Nghệ sĩ • {Songs.Count} bài hát\n❤️ {totalLikes} lượt thích";
            }
            else if (_currentType == "Favorites")
            {
                SubTitle = $"Yêu thích • {Songs.Count} bài hát";
            }
            else if (_currentType == "Playlist")
            {
                SubTitle = $"Playlist • {Songs.Count} bài hát";
            }
        }

       
        private bool _isNavigating = false; 

        [RelayCommand]
        public async Task PlaySong(Song song)
        {
            if (song == null || _isNavigating) return;

            try
            {
                _isNavigating = true;

                // KIỂM TRA QUYỀN LỰC VIP
                bool isCurrentVip = Preferences.Get("IsPremium", false);
                if (song.IsPremium && !isCurrentVip)
                {
                    bool answer = await Shell.Current.DisplayAlert("Premium Content 👑", "Bài này dành cho VIP. Nâng cấp nhé?", "Xem gói VIP", "Để sau");
                    if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                    return; 
                }

                AudioPlayer.PlaySong(song, Songs);
                await Shell.Current.GoToAsync(nameof(PlayerPage));
            }
            finally
            {
                await Task.Delay(500);
                _isNavigating = false;
            }
        }

        [RelayCommand]
        public async Task PlayAll()
        {
            if (Songs == null || Songs.Count == 0 || _isNavigating) return;

            try
            {
                _isNavigating = true;

                bool isVip = Preferences.Get("IsPremium", false);
                var playableSongs = new ObservableCollection<Song>();

               
                if (isVip)
                {
                    foreach (var s in Songs) playableSongs.Add(s); 
                }
                else
                {
                    var freeSongs = Songs.Where(s => !s.IsPremium).ToList(); 
                    foreach (var s in freeSongs) playableSongs.Add(s);
                }

               
                if (playableSongs.Count == 0)
                {
                    bool answer = await Shell.Current.DisplayAlert("Premium Album 👑", "Toàn bộ bài hát trong danh sách này dành riêng cho VIP. Nâng cấp ngay?", "Nâng cấp", "Đóng");
                    if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                    return; 
                }

               
                if (!isVip && playableSongs.Count < Songs.Count)
                {
                    await Shell.Current.DisplayAlert("Lưu ý", "Bạn đang dùng tài khoản thường. Hệ thống chỉ phát các bài miễn phí trong danh sách này.", "OK");
                }

                AudioPlayer.IsShuffle = false;

                
                AudioPlayer.PlaySong(playableSongs[0], playableSongs);
                await Shell.Current.GoToAsync(nameof(PlayerPage));
            }
            finally
            {
                await Task.Delay(500);
                _isNavigating = false;
            }
        }

        [RelayCommand]
        public async Task ShuffleAll()
        {
            if (Songs == null || Songs.Count == 0 || _isNavigating) return;

            try
            {
                _isNavigating = true;

                bool isVip = Preferences.Get("IsPremium", false);
                var playableSongs = new ObservableCollection<Song>();

             
                if (isVip)
                {
                    foreach (var s in Songs) playableSongs.Add(s);
                }
                else
                {
                    var freeSongs = Songs.Where(s => !s.IsPremium).ToList();
                    foreach (var s in freeSongs) playableSongs.Add(s);
                }

                if (playableSongs.Count == 0)
                {
                    bool answer = await Shell.Current.DisplayAlert("Premium Album 👑", "Danh sách này chỉ dành cho VIP. Nâng cấp nhé?", "Nâng cấp", "Đóng");
                    if (answer) await Shell.Current.GoToAsync(nameof(PremiumPage));
                    return;
                }

                if (!isVip && playableSongs.Count < Songs.Count)
                {
                    await Shell.Current.DisplayAlert("Lưu ý", "Chỉ phát ngẫu nhiên các bài miễn phí.", "OK");
                }

                var r = new Random();
                int index = r.Next(playableSongs.Count);

                AudioPlayer.IsShuffle = true;

               
                AudioPlayer.PlaySong(playableSongs[index], playableSongs);
                await Shell.Current.GoToAsync(nameof(PlayerPage));
            }
            finally
            {
                await Task.Delay(500);
                _isNavigating = false;
            }
        }

        [RelayCommand]
        public async Task NavigateToPlayer()
        {
            if (_isNavigating || _audioPlayer.CurrentSong == null) return;
            try
            {
                _isNavigating = true;
                await Shell.Current.GoToAsync(nameof(PlayerPage));
            }
            finally
            {
                await Task.Delay(500);
                _isNavigating = false;
            }
        }

        [RelayCommand]
        public async Task GoBack()
        {
            if (_isNavigating) return;
            try
            {
                _isNavigating = true;
                await Shell.Current.GoToAsync("..");
            }
            finally
            {
                await Task.Delay(500);
                _isNavigating = false;
            }
        }

        [RelayCommand]
        public async Task OpenOptionMenu(Song song)
        {
            if (song == null) return;
            string action = "";

            if (_currentType == "Favorites")
            {
                action = await Shell.Current.DisplayActionSheet(song.Title, "Hủy", "Bỏ Yêu thích 💔", "Chia sẻ");
            }
            else if (_currentType == "Playlist")
            {
                action = await Shell.Current.DisplayActionSheet(song.Title, "Hủy", "Xóa khỏi Playlist", "Thêm vào Yêu thích ❤️", "Chia sẻ");
            }
            else
            {
                action = await Shell.Current.DisplayActionSheet(song.Title, "Hủy", null, "Thêm vào Yêu thích ❤️", "Chia sẻ");
            }

            if (action == "Xóa khỏi Playlist")
            {
                bool confirm = await Shell.Current.DisplayAlert("Xác nhận", "Xóa bài này?", "Xóa", "Hủy");
                if (confirm) await DeleteSong(song);
            }
            else if (action == "Bỏ Yêu thích 💔")
            {
                await _firestoreService.RemoveFromFavoritesAsync(song);

                song.LikeCount = Math.Max(0, song.LikeCount - 1);
                _ = _firestoreService.UpdateGlobalLikeCount(song, -1);

                if (_currentType == "Favorites") Songs.Remove(song);
                UpdateTotalLikesSubtitle();

                WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());
                var toast = Toast.Make("Đã xóa khỏi Yêu thích", ToastDuration.Short);
                await toast.Show();
            }
            else if (action == "Thêm vào Yêu thích ❤️")
            {
                bool isExist = await _firestoreService.IsSongInFavoritesAsync(song);
                if (isExist)
                {
                    var toast = Toast.Make("Bài này đã có trong tim bạn rồi! 😎", ToastDuration.Short);
                    await toast.Show();
                }
                else
                {
                    await _firestoreService.AddToFavoritesAsync(song);
                    song.LikeCount++;
                    _ = _firestoreService.UpdateGlobalLikeCount(song, 1);
                    UpdateTotalLikesSubtitle();

                    var toast = Toast.Make("Đã thêm vào mục Yêu thích! 💚", ToastDuration.Short);
                    await toast.Show();
                }
            }
            else if (action == "Chia sẻ")
            {
                await Share.RequestAsync(new ShareTextRequest
                {
                    Title = "Chia sẻ nhạc",
                    Text = $"Đang nghe bài {song.Title} của {song.Artist} trên CosmicMusic! 🎵"
                });
            }
        }

        private async Task DeleteSong(Song song)
        {
            IsBusy = true;
            await _firestoreService.RemoveSongFromPlaylist(_currentId, song);
            Songs.Remove(song);
            SubTitle = $"Playlist • {Songs.Count} songs";
            WeakReferenceMessenger.Default.Send(new RefreshLibraryMessage());
            IsBusy = false;
        }
    }
}