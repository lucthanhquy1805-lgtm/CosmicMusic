using System;
using System.Collections.Generic;
using System.Text;

using SQLite;

namespace CosmicMusic.Models
{
    public class Playlist
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; }
        public bool IsSystem { get; set; } // True nếu là playlist mặc định (vd: Favorites)
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}