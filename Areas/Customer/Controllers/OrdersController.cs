using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using Project.Models;

namespace Project.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize(Roles = SD.Role_Customer)]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _db;

        public OrdersController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var orders = await _db.Orders
                .AsNoTracking()
                .Where(o => o.UserId == userId)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Attraction)
                .Include(o => o.RefundRequests)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var order = await _db.Orders
                .AsNoTracking()
                .Where(o => o.Id == id && o.UserId == userId)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Attraction)
                .Include(o => o.RefundRequests)
                    .ThenInclude(r => r.OrderItem)
                        .ThenInclude(i => i!.Attraction)
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRefund(
            int orderId,
            int orderItemId,
            int refundQuantity,
            string? reason)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            {
                TempData["Error"] =
                    "Please enter a refund reason of no more than 1000 characters.";

                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            if (refundQuantity <= 0)
            {
                TempData["Error"] = "Refund quantity must be at least 1.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            var order = await _db.Orders
                .Include(o => o.Items)
                .Include(o => o.RefundRequests)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            if (order.PaymentStatus != SD.PaymentPaid)
            {
                TempData["Error"] = "Only paid orders can request a refund.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            if (order.OrderStatus == SD.StatusCancelled)
            {
                TempData["Error"] = "This order has already been cancelled.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            // Old requests created before item-level refunds have no OrderItemId.
            // Block new item-level requests while such an old request is unresolved.
            var hasActiveLegacyRequest = order.RefundRequests.Any(r =>
                !r.OrderItemId.HasValue &&
                (r.RefundStatus == SD.RefundPending ||
                 r.RefundStatus == SD.RefundApproved));

            if (hasActiveLegacyRequest)
            {
                TempData["Error"] =
                    "This order has an older order-level refund request that must be reviewed first.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            var orderItem = order.Items.FirstOrDefault(i => i.Id == orderItemId);
            if (orderItem == null)
            {
                TempData["Error"] = "The selected item does not belong to this order.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            // Pending, approved, and completed refunds reserve quantity so that
            // the same ticket quantity cannot be requested twice.
            var reservedQuantity = order.RefundRequests
                .Where(r =>
                    r.OrderItemId == orderItem.Id &&
                    (r.RefundStatus == SD.RefundPending ||
                     r.RefundStatus == SD.RefundApproved ||
                     r.RefundStatus == SD.RefundCompleted))
                .Sum(r => r.RefundQuantity ?? 0);

            var remainingQuantity = orderItem.Quantity - reservedQuantity;
            if (remainingQuantity <= 0 || refundQuantity > remainingQuantity)
            {
                TempData["Error"] =
                    $"You can request a refund for at most {Math.Max(remainingQuantity, 0)} more ticket(s) for this attraction.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            var refundAmount = orderItem.UnitPrice * refundQuantity;

            // Reserve monetary value from active/completed requests to stop the
            // total requested refund from exceeding the amount paid for the order.
            var reservedAmount = order.RefundRequests
                .Where(r =>
                    r.RefundStatus == SD.RefundPending ||
                    r.RefundStatus == SD.RefundApproved ||
                    r.RefundStatus == SD.RefundCompleted)
                .Sum(r => r.RefundAmount);

            if (refundAmount <= 0 || reservedAmount + refundAmount > order.TotalAmount)
            {
                TempData["Error"] =
                    "This request would exceed the remaining refundable amount for this order.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            var request = new RefundRequest
            {
                OrderId = order.Id,
                OrderItemId = orderItem.Id,
                RefundQuantity = refundQuantity,
                UserId = userId,
                Reason = reason.Trim(),
                RefundStatus = SD.RefundPending,
                RefundAmount = refundAmount,
                RequestedAt = DateTime.UtcNow
            };

            _db.RefundRequests.Add(request);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Your refund request has been submitted.";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }
    }
}
