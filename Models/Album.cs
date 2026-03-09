using System.Collections.Generic;

namespace CosmicMusic.Models
{
    public class Album
    {
        public string Id { get; set; }          // Khóa chính (Document ID trên Firestore)
        public string ArtistId { get; set; }

        public string Title { get; set; }
        public string Artist { get; set; }
        public string Year { get; set; }
        public string CoverImage { get; set; }
        public string Description { get; set; } // Ví dụ: "Album by Orion Nebula • 2023"
        public List<Song> Songs { get; set; } = new(); // Danh sách bài hát trong Album
    }
}