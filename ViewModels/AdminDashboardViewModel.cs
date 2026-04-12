using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // 👇 BỔ SUNG: Thư viện Bộ đàm
using CosmicMusic.Models;
using CosmicMusic.Services;
using CosmicMusic.Views;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CosmicMusic.ViewModels
{
   
    public class SongUpdatedMessage
    {
        public Song UpdatedSong { get; set; }
        public SongUpdatedMessage(Song song) => UpdatedSong = song;
    }

    public partial class AdminDashboardViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private readonly AudioViewModel _audioViewModel;


        [ObservableProperty]
        private ObservableCollection<Song> _adminSongs = new();

        [ObservableProperty]
        private bool _isLoading;


        public AdminDashboardViewModel(FirestoreService firestoreService, AudioViewModel audioViewModel)
        {
            _firestoreService = firestoreService;
            _audioViewModel = audioViewModel;

        }

        [RelayCommand]
        public async Task LoadData()
        {
            IsLoading = true;


            var songsFromDb = await _firestoreService.GetAllSongsAsync();

            AdminSongs.Clear();
            foreach (var song in songsFromDb)
            {
                AdminSongs.Add(song);
            }

            IsLoading = false;
        }

        [RelayCommand]
        public async Task DeleteSong(Song song)
        {
            if (song == null) return;


            bool confirm = await Shell.Current.DisplayAlert("Cảnh báo", $"Bạn có chắc chắn muốn xóa dữ liệu của '{song.Title}' khỏi trạm không gian?", "XÓA NGAY", "HỦY");

            if (confirm)
            {
                IsLoading = true;


                bool isSuccess = await _firestoreService.DeleteSongAsync(song.Id);

                if (isSuccess)
                {
                    if (_audioViewModel.CurrentSong != null && _audioViewModel.CurrentSong.Id == song.Id)
                    {
                        _audioViewModel.Cleanup();
                    }
                    await LoadData();
                    await Shell.Current.DisplayAlert("Thành công", "Đã dọn dẹp dữ liệu bài hát.", "OK");
                }
                else
                {
                    IsLoading = false;
                    await Shell.Current.DisplayAlert("Lỗi", "Không thể xóa bài hát lúc này. Hãy thử lại.", "OK");
                }
            }
        }


        [RelayCommand]
        public async Task EditSong(Song song)
        {
            if (song == null) return;


            string newTitle = await Shell.Current.DisplayPromptAsync("Chỉnh sửa", "Nhập tên bài hát mới:", "LƯU", "HỦY", initialValue: song.Title);
            if (string.IsNullOrWhiteSpace(newTitle)) return;


            string newArtist = await Shell.Current.DisplayPromptAsync("Chỉnh sửa", "Nhập tên ca sĩ:", "LƯU", "HỦY", initialValue: song.Artist);
            if (string.IsNullOrWhiteSpace(newArtist)) return;

            IsLoading = true;


            song.Title = newTitle;
            song.Artist = newArtist;


            bool isSuccess = await _firestoreService.AddSongAsync(song);

            if (isSuccess)
            {
               
                WeakReferenceMessenger.Default.Send(new SongUpdatedMessage(song));

                await LoadData();
            }
            else
            {
                IsLoading = false;
                await Shell.Current.DisplayAlert("Lỗi", "Cập nhật thất bại.", "OK");
            }
        }

        [RelayCommand]
        public async Task GoToAddSongPage()
        {
            await Shell.Current.GoToAsync(nameof(AddSongPage));
        }

        [ObservableProperty]
        private string _searchKeyword;

        [RelayCommand]
        public async Task PerformSearch()
        {
            IsLoading = true;
            AdminSongs.Clear();

          
            if (string.IsNullOrWhiteSpace(_searchKeyword))
            {

                var allSongs = await _firestoreService.GetAllSongsAsync();
                foreach (var song in allSongs) AdminSongs.Add(song);
            }
            else
            {

                var searchResults = await _firestoreService.SearchSongsByKeywordsAsync(_searchKeyword);
                foreach (var song in searchResults) AdminSongs.Add(song);
            }

            IsLoading = false;
        }

        [RelayCommand]
        public async Task GoBack()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}