using Project.Models;

namespace Project.ViewModels
{
    public class UserProfileViewModel
    {
        public string UserId { get; set; }
        public string? DisplayName { get; set; }

        public bool IsCurrentUser { get; set; }
        public bool IsFollowing { get; set; }

        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }

        public List<Post> Posts { get; set; } = new List<Post>();
    }
}