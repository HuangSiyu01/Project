using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Project.Models
{
    public class Follow
    {
        [Key]
        public int Id { get; set; }

        // 关注者
        [ValidateNever]
        public string FollowerId { get; set; }

        [ValidateNever]
        [ForeignKey("FollowerId")]
        public ApplicationUser Follower { get; set; }

        // 被关注者
        [ValidateNever]
        public string FolloweeId { get; set; }

        [ValidateNever]
        [ForeignKey("FolloweeId")]
        public ApplicationUser Followee { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}