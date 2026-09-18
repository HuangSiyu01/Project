using Project.Models;

namespace Project.ViewModels
{
    public class ProfileViewModel
    {
        public string UserId { get; set; }
        public string? DisplayName { get; set; }

        public List<Post> MyPosts { get; set; } = new List<Post>();
        public List<Post> LikedPosts { get; set; } = new List<Post>();
        public List<Post> FavoritePosts { get; set; } = new List<Post>();

        public List<ApplicationUser> FollowingUsers { get; set; } = new List<ApplicationUser>();
        public List<ApplicationUser> FollowerUsers { get; set; } = new List<ApplicationUser>();
    }
}