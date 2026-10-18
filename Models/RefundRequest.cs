using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project.Models
{
    public class RefundRequest
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        // Nullable so existing refund rows can survive the migration.
        // All new refund requests must supply an OrderItemId in the controller.
        [Range(1, int.MaxValue)]
        public int? OrderItemId { get; set; }

        // Nullable for legacy records; new requests are validated by the controller.
        [Range(1, int.MaxValue)]
        public int? RefundQuantity { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string RefundStatus { get; set; } = SD.RefundPending;

        [StringLength(1000)]
        public string? AdminRemarks { get; set; }

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        [StringLength(255)]
        public string? ReviewedByUserId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0.01", "99999999.99")]
        public decimal RefundAmount { get; set; }

        [StringLength(255)]
        public string? StripeRefundId { get; set; }

        public Order? Order { get; set; }

        public OrderItem? OrderItem { get; set; }

        public ApplicationUser? User { get; set; }
    }
}
