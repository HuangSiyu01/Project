using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Project.Data;
using Project.Models;
using Project.Services;
using Stripe;

namespace Project.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<OrderController> _logger;
        private readonly TicketService _ticketService;

        public OrderController(
            ApplicationDbContext db,
            ILogger<OrderController> logger,
            TicketService ticketService)
        {
            _db = db;
            _logger = logger;
            _ticketService = ticketService;
        }

        // GET: /Admin/Order
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var orders = await _db.Orders
                .AsNoTracking()
                .Include(o => o.User)
                .Include(o => o.Items)
                .Include(o => o.RefundRequests)
                .Where(o =>
                    o.PaymentStatus == SD.PaymentPaid ||
                    o.RefundRequests.Any(r =>
                        r.RefundStatus == SD.RefundPending ||
                        r.RefundStatus == SD.RefundApproved ||
                        r.RefundStatus == SD.RefundCompleted))
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // GET: /Admin/Order/Details/1
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            var order = await _db.Orders
                .AsNoTracking()
                .Include(o => o.User)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Attraction)
                .Include(o => o.RefundRequests)
                    .ThenInclude(r => r.OrderItem)
                        .ThenInclude(i => i!.Attraction)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // POST: /Admin/Order/Approve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            if (order.OrderStatus != SD.StatusPending)
            {
                TempData["Error"] = "Only pending orders can be approved.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (order.PaymentStatus != SD.PaymentPaid)
            {
                TempData["Error"] = "Only successfully paid orders can be approved.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.OrderStatus = SD.StatusApproved;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Issue e-tickets only after the admin approval has been saved.
            await _ticketService.EnsureTicketsForOrderAsync(order.Id);

            TempData["Success"] = "The order has been approved and its e-tickets have been issued.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Admin/Order/ApproveRefund
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRefund(int refundRequestId, string? adminRemarks)
        {
            var refundRequest = await _db.RefundRequests
                .Include(r => r.Order)
                .Include(r => r.OrderItem)
                .FirstOrDefaultAsync(r => r.Id == refundRequestId);

            if (refundRequest == null || refundRequest.Order == null)
            {
                return NotFound();
            }

            var order = refundRequest.Order;
            var orderId = order.Id;

            if (refundRequest.RefundStatus != SD.RefundPending)
            {
                TempData["Error"] = "This refund request has already been reviewed.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            if (order.PaymentStatus != SD.PaymentPaid || order.OrderStatus == SD.StatusCancelled)
            {
                TempData["Error"] = "The order is not currently eligible for a refund.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            if (!refundRequest.OrderItemId.HasValue || refundRequest.OrderItem == null ||
                !refundRequest.RefundQuantity.HasValue)
            {
                TempData["Error"] = "This is an older order-level refund request without item and quantity details. It cannot be processed by the new item-level refund workflow; review it manually.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            if (string.IsNullOrWhiteSpace(order.StripePaymentIntentId))
            {
                _logger.LogError(
                    "Refund request {RefundRequestId} for order {OrderId} has no Stripe PaymentIntent ID.",
                    refundRequestId, orderId);
                TempData["Error"] = "The Stripe PaymentIntent ID is missing. Please check the payment record.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            var orderItem = refundRequest.OrderItem;
            var refundQuantity = refundRequest.RefundQuantity.Value;

            if (orderItem.OrderId != order.Id || refundQuantity <= 0 || refundQuantity > orderItem.Quantity)
            {
                TempData["Error"] = "The refund request contains an invalid order item or quantity.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            var expectedRefundAmount = orderItem.UnitPrice * refundQuantity;
            if (refundRequest.RefundAmount <= 0 || refundRequest.RefundAmount != expectedRefundAmount)
            {
                _logger.LogWarning(
                    "Refund amount mismatch for request {RefundRequestId}. Stored: {StoredAmount}; expected: {ExpectedAmount}.",
                    refundRequestId, refundRequest.RefundAmount, expectedRefundAmount);
                TempData["Error"] = "The refund amount does not match the item price and requested quantity.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            // Reserve money against all requests that are pending, accepted by Stripe,
            // or completed. This prevents approvals exceeding the amount paid.
            var reservedAmount = await _db.RefundRequests
                .Where(r => r.OrderId == orderId && r.Id != refundRequest.Id &&
                    (r.RefundStatus == SD.RefundPending ||
                     r.RefundStatus == SD.RefundApproved ||
                     r.RefundStatus == SD.RefundCompleted))
                .SumAsync(r => (decimal?)r.RefundAmount) ?? 0m;

            if (reservedAmount + refundRequest.RefundAmount > order.TotalAmount)
            {
                TempData["Error"] = "Approving this request would exceed the order's remaining refundable amount. Review other active refund requests first.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            var reservedQuantity = await _db.RefundRequests
                .Where(r => r.OrderId == orderId &&
                    r.OrderItemId == orderItem.Id &&
                    r.Id != refundRequest.Id &&
                    (r.RefundStatus == SD.RefundPending ||
                     r.RefundStatus == SD.RefundApproved ||
                     r.RefundStatus == SD.RefundCompleted))
                .SumAsync(r => (int?)(r.RefundQuantity ?? 0)) ?? 0;

            if (reservedQuantity + refundQuantity > orderItem.Quantity)
            {
                TempData["Error"] = "Approving this request would exceed the item's remaining refundable ticket quantity. Review other active requests first.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            try
            {
                var refundService = new RefundService();
                var refundOptions = new RefundCreateOptions
                {
                    PaymentIntent = order.StripePaymentIntentId,
                    Amount = decimal.ToInt64(decimal.Round(
                        refundRequest.RefundAmount * 100m,
                        0,
                        MidpointRounding.AwayFromZero)),
                    Reason = "requested_by_customer"
                };

                // Reusing this key is important if Stripe succeeded but saving to the
                // database failed; a retry for this request should not create a second refund.
                var requestOptions = new RequestOptions
                {
                    IdempotencyKey = $"refund-request-{refundRequest.Id}"
                };

                var stripeRefund = await refundService.CreateAsync(refundOptions, requestOptions);

                refundRequest.StripeRefundId = stripeRefund.Id;
                refundRequest.AdminRemarks = string.IsNullOrWhiteSpace(adminRemarks)
                    ? null
                    : adminRemarks.Trim();
                refundRequest.ReviewedAt = DateTime.UtcNow;
                refundRequest.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (stripeRefund.Status == "succeeded")
                {
                    refundRequest.RefundStatus = SD.RefundCompleted;

                    var previouslyCompletedAmount = await _db.RefundRequests
                        .Where(r => r.OrderId == orderId &&
                            r.Id != refundRequest.Id &&
                            r.RefundStatus == SD.RefundCompleted)
                        .SumAsync(r => (decimal?)r.RefundAmount) ?? 0m;

                    var completedAmount = previouslyCompletedAmount + refundRequest.RefundAmount;
                    if (completedAmount >= order.TotalAmount)
                    {
                        order.PaymentStatus = SD.PaymentRefunded;
                        order.OrderStatus = SD.StatusCancelled;
                    }

                    // Partial refunds do not cancel the entire order.
                    order.UpdatedAt = DateTime.UtcNow;
                    TempData["Success"] = "The refund was completed successfully.";
                }
                else if (stripeRefund.Status == "pending")
                {
                    // This is not marked Completed and no order status is changed.
                    refundRequest.RefundStatus = SD.RefundApproved;
                    TempData["Success"] = "Stripe accepted the refund request, but its status is still pending. Check the Stripe Dashboard before considering it complete.";
                }
                else
                {
                    refundRequest.RefundStatus = SD.RefundRejected;
                    refundRequest.AdminRemarks =
                        $"Stripe did not complete the refund. Stripe status: {stripeRefund.Status ?? "Unknown"}. " +
                        (string.IsNullOrWhiteSpace(adminRemarks) ? string.Empty : adminRemarks.Trim());
                    TempData["Error"] = $"Stripe did not complete the refund. Current status: {stripeRefund.Status ?? "Unknown"}. The request was closed and the order was not cancelled.";
                }

                await _db.SaveChangesAsync();
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex,
                    "Stripe refund failed. RefundRequestId: {RefundRequestId}, OrderId: {OrderId}, StripeError: {StripeError}",
                    refundRequestId, orderId, ex.StripeError?.Message);
                TempData["Error"] = ex.StripeError?.Message ?? "Stripe could not process the refund. Check the application logs.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex,
                    "Database update failed after Stripe refund. RefundRequestId: {RefundRequestId}, OrderId: {OrderId}, StripeRefundId: {StripeRefundId}",
                    refundRequestId, orderId, refundRequest.StripeRefundId);
                TempData["Error"] = "The database could not save the refund result. Check the Stripe Dashboard before retrying. The same request uses an idempotency key to reduce duplicate-refund risk.";
            }

            return RedirectToAction(nameof(Details), new { id = orderId });
        }

        // POST: /Admin/Order/RejectRefund
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRefund(int refundRequestId, string? adminRemarks)
        {
            var refundRequest = await _db.RefundRequests
                .Include(r => r.Order)
                .FirstOrDefaultAsync(r => r.Id == refundRequestId);

            if (refundRequest == null || refundRequest.Order == null)
            {
                return NotFound();
            }

            var orderId = refundRequest.Order.Id;
            if (refundRequest.RefundStatus != SD.RefundPending)
            {
                TempData["Error"] = "Only pending refund requests can be rejected.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            if (string.IsNullOrWhiteSpace(adminRemarks))
            {
                TempData["Error"] = "Please provide a reason for rejecting the refund.";
                return RedirectToAction(nameof(Details), new { id = orderId });
            }

            refundRequest.RefundStatus = SD.RefundRejected;
            refundRequest.AdminRemarks = adminRemarks.Trim();
            refundRequest.ReviewedAt = DateTime.UtcNow;
            refundRequest.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            await _db.SaveChangesAsync();
            TempData["Success"] = "The refund request has been rejected.";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }
    }
}
