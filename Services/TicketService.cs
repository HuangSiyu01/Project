using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using Project.Models;

namespace Project.Services
{
    /// <summary>
    /// Idempotently issues one e-ticket per purchased unit and synchronizes ticket
    /// statuses with completed item-level refund requests.
    /// </summary>
    public class TicketService
    {
        private readonly ApplicationDbContext _db;

        public TicketService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task EnsureTicketsForOrderAsync(int orderId, bool saveChanges = true)
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .Include(o => o.RefundRequests)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            // E-tickets must not be issued until an administrator approves the order.
            if (order == null ||
                order.OrderStatus != SD.StatusApproved ||
                (order.PaymentStatus != SD.PaymentPaid &&
                 order.PaymentStatus != SD.PaymentRefunded))
            {
                return;
            }

            // Keep in-memory groups including newly added tickets so the refund
            // synchronization below also works when saveChanges is false.
            var ticketsByItem = new Dictionary<int, List<Ticket>>();

            foreach (var orderItem in order.Items)
            {
                var itemTickets = await _db.Tickets
                    .Where(t => t.OrderItemId == orderItem.Id)
                    .OrderBy(t => t.Id)
                    .ToListAsync();

                // Payment success may be visited more than once, so only create
                // the missing number of tickets; never issue duplicates on refresh.
                while (itemTickets.Count < orderItem.Quantity)
                {
                    var ticket = new Ticket
                    {
                        OrderId = order.Id,
                        OrderItemId = orderItem.Id,
                        TicketNumber = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                        Status = SD.TicketActive,
                        IssuedAt = DateTime.UtcNow
                    };

                    _db.Tickets.Add(ticket);
                    itemTickets.Add(ticket);
                }

                ticketsByItem[orderItem.Id] = itemTickets;
            }

            var completedRefunds = order.RefundRequests
                .Where(r => r.RefundStatus == SD.RefundCompleted)
                .OrderBy(r => r.RequestedAt)
                .ToList();

            // Link the appropriate number of tickets to every completed item-level
            // refund. The first synchronization marks them inactive; later calls are idempotent.
            foreach (var orderItem in order.Items)
            {
                if (!ticketsByItem.TryGetValue(orderItem.Id, out var itemTickets))
                {
                    continue;
                }

                var itemRefunds = completedRefunds
                    .Where(r => r.OrderItemId == orderItem.Id && r.RefundQuantity.HasValue)
                    .ToList();

                foreach (var refund in itemRefunds)
                {
                    var expectedQuantity = Math.Max(refund.RefundQuantity ?? 0, 0);
                    var alreadyLinked = itemTickets.Count(t =>
                        t.Status == SD.TicketRefunded && t.RefundRequestId == refund.Id);
                    var needed = expectedQuantity - alreadyLinked;

                    if (needed <= 0)
                    {
                        continue;
                    }

                    var ticketsToRefund = itemTickets
                        .Where(t => t.Status == SD.TicketActive && !t.RefundRequestId.HasValue)
                        .OrderBy(t => t.Id)
                        .Take(needed)
                        .ToList();

                    foreach (var ticket in ticketsToRefund)
                    {
                        ticket.Status = SD.TicketRefunded;
                        ticket.RefundedAt = refund.ReviewedAt ?? DateTime.UtcNow;
                        ticket.RefundRequestId = refund.Id;
                    }
                }
            }

            // Older order-level full refunds did not store an item/quantity. If the
            // recorded completed refunds cover the whole order, invalidate any remaining
            // tickets rather than accidentally presenting them as valid.
            var completedAmount = completedRefunds.Sum(r => r.RefundAmount);
            if (order.TotalAmount > 0m && completedAmount >= order.TotalAmount)
            {
                var legacyRefund = completedRefunds
                    .Where(r => !r.OrderItemId.HasValue || !r.RefundQuantity.HasValue)
                    .OrderByDescending(r => r.RequestedAt)
                    .FirstOrDefault();

                foreach (var ticket in ticketsByItem.Values.SelectMany(t => t))
                {
                    if (ticket.Status == SD.TicketActive)
                    {
                        ticket.Status = SD.TicketRefunded;
                        ticket.RefundedAt = legacyRefund?.ReviewedAt ?? DateTime.UtcNow;
                        ticket.RefundRequestId = legacyRefund?.Id;
                    }
                }
            }

            if (saveChanges)
            {
                await _db.SaveChangesAsync();
            }
        }
    }
}

