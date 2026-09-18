using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Project.Models
{
    public class PostImage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ImageUrl { get; set; }

        public int PostId { get; set; }

        [ValidateNever]
        [ForeignKey("PostId")]
        public Post Post { get; set; }
    }
}