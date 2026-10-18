using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string OrderStatus { get; set; } = "Pending";

        [Required]
        [StringLength(30)]
        public string PaymentStatus { get; set; } = "Pending";

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0.01", "99999999.99")]
        public decimal TotalAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [StringLength(255)]
        public string? StripeSessionId { get; set; }

        [StringLength(255)]
        public string? StripePaymentIntentId { get; set; }

        // Navigation properties
        public ApplicationUser? User { get; set; }

        public ICollection<OrderItem> Items { get; set; }
            = new List<OrderItem>();

        public ICollection<RefundRequest> RefundRequests { get; set; }
            = new List<RefundRequest>();
    }
}

