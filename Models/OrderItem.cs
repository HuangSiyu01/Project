using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int AttractionId { get; set; }

        [Range(1, 20)]
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0.01", "99999999.99")]
        public decimal UnitPrice { get; set; }

        [NotMapped]
        public decimal Subtotal => UnitPrice * Quantity;

        // Navigation properties
        public Order? Order { get; set; }

        public Attraction? Attraction { get; set; }
    }
}

