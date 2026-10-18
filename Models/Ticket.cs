using System;
using System.ComponentModel.DataAnnotations;

namespace Project.Models
{
    /// <summary>
    /// One record represents one individually issued e-ticket.
    /// Tickets are never deleted when refunded; their status is changed for history/audit.
    /// </summary>
    public class Ticket
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int OrderItemId { get; set; }

        [Required]
        [StringLength(64)]
        public string TicketNumber { get; set; } = Guid.NewGuid().ToString("N").ToUpperInvariant();

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = SD.TicketActive;

        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

        public DateTime? RefundedAt { get; set; }

        public int? RefundRequestId { get; set; }

        public Order? Order { get; set; }

        public OrderItem? OrderItem { get; set; }

        public RefundRequest? RefundRequest { get; set; }
    }
}

