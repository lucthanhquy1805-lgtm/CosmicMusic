namespace CosmicMusic.Models
{
    public class User
    {
       
        public string Uid { get; set; }

        public string Email { get; set; }

        public string DisplayName { get; set; }

       
        public bool IsPremium { get; set; } = false;

       
        public string PhotoUrl { get; set; }
        public bool IsAdmin { get; set; } = false;
    }
}