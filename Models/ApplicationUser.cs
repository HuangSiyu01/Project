using Microsoft.AspNetCore.Identity;

namespace Project.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }

        public ICollection<Post> Posts { get; set; } = new List<Post>();
        public ICollection<Like> Likes { get; set; } = new List<Like>();
        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
        public ICollection<Follow> Following { get; set; } = new List<Follow>();
        public ICollection<Follow> Followers { get; set; } = new List<Follow>();
    }
}