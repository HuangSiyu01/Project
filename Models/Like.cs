using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Project.Models
{
    public class Like
    {
        [Key]
        public int Id { get; set; }

        public int PostId { get; set; }

        [ValidateNever]
        [ForeignKey("PostId")]
        public Post Post { get; set; }

        [ValidateNever]
        public string UserId { get; set; }

        [ValidateNever]
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
