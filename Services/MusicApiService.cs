using CosmicMusic.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CosmicMusic.Services
{
    public class MusicApiService
    {
        public async Task<List<Song>> GetSongsAsync()
        {
            await Task.Delay(100);

            return new List<Song>
            {
                // --- PHẦN 1: 5 PLAYLIST GỐC (Sẽ hiện ở Library) ---
                new Song { Title = "Liked Songs", Artist = "Playlist • 128 songs", CoverImage = "cover_liked.jpg", AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-1.mp3" },
                new Song { Title = "Chill Morning", Artist = "Playlist • 42 songs", CoverImage = "cover_chill.jpg", AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-2.mp3" },
                new Song { Title = "Late Night Drives", Artist = "Playlist • 89 songs", CoverImage = "cover_drive.jpg", AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-3.mp3" },
                new Song { Title = "Indie Coffeehouse", Artist = "Playlist • 67 songs", CoverImage = "cover_indie.jpg", AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-4.mp3" },
                new Song { Title = "Workout Beats", Artist = "Playlist • 112 songs", CoverImage = "cover_workout.jpg", AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-15.mp3" },

                // --- PHẦN 2: CÁC BÀI HÁT LẺ (Sẽ hiện ở Home - Recently Played) ---
                
                // Bài 1
                new Song
                {
                    Title = "Cosmic Dream",
                    Artist = "Orion Nebula",
                    CoverImage = "https://images.unsplash.com/photo-1534447677768-be436bb09401?w=500&q=80",
                    AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-1.mp3"
                },
                // Bài 2
                new Song
                {
                    Title = "Neon City Lights",
                    Artist = "Cyber Runner",
                    CoverImage = "https://images.unsplash.com/photo-1496568816309-51d7c20e3b21?w=500&q=80",
                    AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-2.mp3"
                },
                // Bài 3 (Mới thêm)
                new Song
                {
                    Title = "Midnight Rain",
                    Artist = "Lofi Bot",
                    CoverImage = "https://images.unsplash.com/photo-1493225255756-d9584f8606e9?w=500&q=80",
                    AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-3.mp3"
                },
                // Bài 4 (Mới thêm)
                new Song
                {
                    Title = "Solar Voyage",
                    Artist = "Star Walker",
                    CoverImage = "https://images.unsplash.com/photo-1462331940025-496dfbfc7564?w=500&q=80",
                    AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-4.mp3"
                },
                // Bài 5 (Mới thêm)
                new Song
                {
                    Title = "Deep Space",
                    Artist = "Void Explorer",
                    CoverImage = "https://images.unsplash.com/photo-1444703686981-a3abbc4d4fe3?w=500&q=80",
                    AudioUrl = "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-5.mp3"
                }
            };
        }
    }
}