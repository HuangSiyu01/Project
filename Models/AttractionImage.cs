using System.ComponentModel.DataAnnotations;

namespace Project.Models
{
    public class AttractionImage
    {
        public int Id { get; set; }

        [Required]
        public int AttractionId { get; set; }

        [Required]
        [StringLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        // Navigation property
        public Attraction Attraction { get; set; } = null!;
    }
}
