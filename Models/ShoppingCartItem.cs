using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project.Models
{
    public class ShoppingCartItem
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public int AttractionId { get; set; }

        [Range(1, 20)]
        public int Quantity { get; set; }

        [ForeignKey(nameof(AttractionId))]
        public Attraction? Attraction { get; set; }
    }
}
