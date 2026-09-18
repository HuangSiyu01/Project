using Microsoft.AspNetCore.Identity;

namespace Project.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string DisplayName { get; set; }
        public string? AvatarUrl { get; set; }

        // 我的帖子
        public ICollection<Post> Posts { get; set; } = new List<Post>();

        // 我点赞的
        public ICollection<Like> Likes { get; set; } = new List<Like>();

        // 我收藏的
        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();

        // 我关注的人（Follower 是我自己）
        public ICollection<Follow> Following { get; set; } = new List<Follow>();

        // 关注我的人（Followee 是我自己）
        public ICollection<Follow> Followers { get; set; } = new List<Follow>();
    }
}