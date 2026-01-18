using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using CosmicMusic.Views;
using CosmicMusic.Services;
using CosmicMusic.Models; // 👈 1. Thêm cái này để dùng được class Song

namespace CosmicMusic.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly MusicApiService _musicService;

    // 👇 2. Khai báo AudioViewModel để điều khiển phát nhạc
    private readonly AudioViewModel _audioViewModel;
    public AudioViewModel AudioPlayer => _audioViewModel;

    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<LibraryItem> LibraryItems { get; } = new();

    // 👇 3. Inject AudioViewModel vào Constructor
    public LibraryViewModel(MusicApiService musicService, AudioViewModel audioViewModel)
    {
        _musicService = musicService;
        _audioViewModel = audioViewModel; // Lưu lại để dùng
        LoadData();
    }

    private async void LoadData()
    {
        Categories.Clear();
        Categories.Add("Playlists");
        Categories.Add("Artists");
        Categories.Add("Albums");
        Categories.Add("Podcasts");

        LibraryItems.Clear();

        try
        {
            var songs = await _musicService.GetSongsAsync();

            foreach (var song in songs)
            {
                LibraryItems.Add(new LibraryItem
                {
                    Title = song.Title,
                    Subtitle = song.Artist,
                    CoverImage = song.CoverImage,
                    Url = song.AudioUrl,
                    ImageColor = "#120520"
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi tải nhạc: {ex.Message}");
        }
    }

    [RelayCommand]
    async Task TapSong(LibraryItem item)
    {
        if (item == null) return;

        // 👇 4. LOGIC MỚI QUAN TRỌNG NHẤT 👇

        // Bước A: Chuyển đổi LibraryItem (giao diện) -> Song (model phát nhạc)
        var songToPlay = new Song
        {
            Title = item.Title,
            Artist = item.Subtitle, // LibraryItem dùng Subtitle cho ca sĩ
            CoverImage = item.CoverImage,
            AudioUrl = item.Url
        };

        // Bước B: Tạo danh sách phát (Context List) từ Library
        // Để khi bấm Next/Previous nó sẽ chuyển bài trong danh sách Library này
        var contextList = new ObservableCollection<Song>();
        foreach (var libItem in LibraryItems)
        {
            contextList.Add(new Song
            {
                Title = libItem.Title,
                Artist = libItem.Subtitle,
                CoverImage = libItem.CoverImage,
                AudioUrl = libItem.Url
            });
        }

        // Bước C: Ra lệnh cho AudioViewModel phát bài này NGAY LẬP TỨC
        _audioViewModel.PlaySong(songToPlay, contextList);

        // 👆 HẾT PHẦN LOGIC PHÁT NHẠC 👆


        // Bước D: Chuyển trang (Giữ nguyên logic cũ để UI cập nhật)
        var navigationParameter = new Dictionary<string, object>
        {
            { "SongData", item }
        };

        await Shell.Current.GoToAsync(nameof(PlayerPage), navigationParameter);
    }
    [RelayCommand]
    public async Task NavigateToPlayer()
    {
        if (_audioViewModel.CurrentSong == null) return;

        var currentSong = _audioViewModel.CurrentSong;
        var libraryItem = new LibraryItem
        {
            Title = currentSong.Title,
            Subtitle = currentSong.Artist,
            CoverImage = currentSong.CoverImage,
            Url = currentSong.AudioUrl,
            ImageColor = "#120520"
        };

        var navigationParameter = new Dictionary<string, object>
    {
        { "SongData", libraryItem }
    };

        await Shell.Current.GoToAsync(nameof(PlayerPage), navigationParameter);
    }

    [RelayCommand]
    void SelectCategory(string category)
    {
    }
}

public class LibraryItem
{
    public string Title { get; set; }
    public string Subtitle { get; set; }
    public string CoverImage { get; set; }
    public string ImageColor { get; set; }
    public string Url { get; set; }
}