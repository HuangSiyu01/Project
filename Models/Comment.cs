using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Project.Models
{
    public class Comment
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Comment is required.")]
        [MaxLength(500, ErrorMessage = "Comment cannot exceed 500 characters.")]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int PostId { get; set; }

        [ValidateNever]
        [ForeignKey("PostId")]
        public Post Post { get; set; }

        [ValidateNever]
        public string UserId { get; set; }

        [ValidateNever]
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        // 回复的目标评论（顶层评论为 null）
        public int? ParentCommentId { get; set; }

        [ValidateNever]
        [ForeignKey("ParentCommentId")]
        public Comment? ParentComment { get; set; }

        // 子回复
        public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    }
}