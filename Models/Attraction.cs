using System.ComponentModel.DataAnnotations;

namespace Project.Models
{
    public class Attraction
    {
        public int Id { get; set; }

        [Required]
        [StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        [Range(0.01, 10000)]
        public decimal TicketPrice { get; set; }

        public bool IsAvailable { get; set; } = true;
        public ICollection<AttractionImage> Images { get; set; } = new List<AttractionImage>();
    }
}

