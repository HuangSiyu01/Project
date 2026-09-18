using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Project.Models
{
    public class Post
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Location is required.")]
        [MaxLength(200, ErrorMessage = "Location cannot exceed 200 characters.")]
        public string Location { get; set; }

        [Range(0, 10, ErrorMessage = "Rating must be between 0 and 10.")]
        [Display(Name = "Rating (0-10)")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Review is required.")]
        [Display(Name = "Review (max 500 words)")]
        public string Review { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ValidateNever]
        public string UserId { get; set; }

        [ValidateNever]
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        public ICollection<PostImage> Images { get; set; } = new List<PostImage>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<Like> Likes { get; set; } = new List<Like>();
        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    }
}