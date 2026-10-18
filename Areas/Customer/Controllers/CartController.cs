using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using Project.Models;
using Project.Services;
using Stripe;
using Stripe.Checkout;

namespace Project.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly CartService _cartService;
        private readonly ApplicationDbContext _context;

        public CartController(
            CartService cartService,
            ApplicationDbContext context)
        {
            _cartService = cartService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cart = await _cartService.GetValidatedCartAsync();

            var attractionIds = cart
                .Select(item => item.AttractionId)
                .Distinct()
                .ToList();

            var attractions = await _context.Attractions
                .AsNoTracking()
                .Where(item => attractionIds.Contains(item.Id))
                .ToListAsync();

            ViewBag.CartItems = cart;
            ViewBag.Attractions = attractions;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(
            int attractionId,
            int quantity = 1,
            string? returnUrl = null)
        {
            // Administrators may browse attractions, but cannot add items to the cart.
            // Keep this server-side check so manually submitted POST requests are blocked too.
            if (User.IsInRole(SD.Role_Admin))
            {
                TempData["ErrorMessage"] =
                    "Administrators cannot add attractions to the shopping cart.";

                if (!string.IsNullOrWhiteSpace(returnUrl) &&
                    Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction(
                    "Index",
                    "Attractions",
                    new { area = "Customer" });
            }

            try
            {
                await _cartService.AddItemAsync(
                    attractionId,
                    quantity);

                TempData["SuccessMessage"] =
                    "The attraction was added to your cart.";
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(
                "Index",
                "Attractions",
                new { area = "Customer" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(
            int attractionId,
            int quantity)
        {
            try
            {
                await _cartService.UpdateQuantityAsync(
                    attractionId,
                    quantity);

                TempData["SuccessMessage"] =
                    "The cart quantity was updated.";
            }
            catch (ArgumentException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int attractionId)
        {
            await _cartService.RemoveItemAsync(attractionId);

            TempData["SuccessMessage"] =
                "The item was removed from your cart.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Clear()
        {
            await _cartService.ClearCartAsync();

            TempData["SuccessMessage"] =
                "Your shopping cart has been cleared.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Success(string? session_id)
        {
            if (string.IsNullOrWhiteSpace(session_id))
            {
                TempData["ErrorMessage"] =
                    "The payment session could not be verified.";

                return RedirectToAction(nameof(Index));
            }

            var currentUserId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Challenge();
            }

            try
            {
                // Retrieve the session from Stripe rather than trusting query-string
                // values or the browser's claim that payment was successful.
                var stripeService = new SessionService();
                var stripeSession = await stripeService.GetAsync(session_id);
                var metadata = stripeSession.Metadata;

                var sessionUserId = metadata != null &&
                    metadata.TryGetValue("UserId", out var metadataUserId)
                        ? metadataUserId
                        : null;

                var orderIdFromMetadata = metadata != null &&
                    metadata.TryGetValue("OrderId", out var metadataOrderId) &&
                    int.TryParse(metadataOrderId, out var parsedOrderId)
                        ? parsedOrderId
                        : 0;

                if (!string.Equals(
                        sessionUserId,
                        currentUserId,
                        StringComparison.Ordinal) ||
                    orderIdFromMetadata <= 0 ||
                    !string.Equals(
                        stripeSession.ClientReferenceId,
                        orderIdFromMetadata.ToString(),
                        StringComparison.Ordinal))
                {
                    TempData["ErrorMessage"] =
                        "The payment session could not be matched to your account and order.";

                    return RedirectToAction(nameof(Index));
                }

                var order = await _context.Orders
                    .Include(item => item.Items)
                    .FirstOrDefaultAsync(item =>
                        item.Id == orderIdFromMetadata &&
                        item.StripeSessionId == stripeSession.Id &&
                        item.UserId == currentUserId);

                if (order == null)
                {
                    TempData["ErrorMessage"] =
                        "The order could not be found. Please contact support.";

                    return RedirectToAction(nameof(Index));
                }

                if (!string.Equals(
                    stripeSession.PaymentStatus,
                    "paid",
                    StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] =
                        "Payment has not been confirmed. Your order remains pending.";

                    return RedirectToAction(nameof(Index));
                }

                // Cross-check the authoritative Stripe amount and currency against
                // the saved order before marking the order as paid.
                var expectedAmountInCents = (long)decimal.Round(
                    order.TotalAmount * 100m,
                    0,
                    MidpointRounding.AwayFromZero);

                if (!string.Equals(
                        stripeSession.Currency,
                        "nzd",
                        StringComparison.OrdinalIgnoreCase) ||
                    stripeSession.AmountTotal != expectedAmountInCents)
                {
                    TempData["ErrorMessage"] =
                        "The payment amount could not be matched to the order. Please contact support.";

                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(stripeSession.PaymentIntentId))
                {
                    TempData["ErrorMessage"] =
                        "Payment was successful, but the Stripe PaymentIntent ID is missing. Please contact support.";

                    return RedirectToAction(nameof(Index));
                }

                // Never change a fully refunded order back to Paid when a customer
                // revisits an old success URL.
                if (order.PaymentStatus == SD.PaymentRefunded)
                {
                    TempData["ErrorMessage"] =
                        "This order has already been refunded.";

                    return RedirectToAction(nameof(Index));
                }

                var needsSave = false;

                if (order.PaymentStatus != SD.PaymentPaid)
                {
                    order.PaymentStatus = SD.PaymentPaid;
                    order.UpdatedAt = DateTime.UtcNow;
                    needsSave = true;
                }

                if (order.StripePaymentIntentId != stripeSession.PaymentIntentId)
                {
                    order.StripePaymentIntentId = stripeSession.PaymentIntentId;
                    needsSave = true;
                }

                if (needsSave)
                {
                    await _context.SaveChangesAsync();
                }

                // Remove only the attraction rows associated with this paid order.
                var purchasedIds = order.Items
                    .Select(item => item.AttractionId)
                    .Distinct()
                    .ToList();

                await _cartService.RemoveItemsAsync(purchasedIds);

                TempData["SuccessMessage"] =
                    "Payment successful. Your order is awaiting admin approval.";

                // Return directly to the application's homepage (Post/Index).
                return RedirectToAction(
                    "Index",
                    "Post",
                    new { area = "Customer" });
            }
            catch (StripeException)
            {
                TempData["ErrorMessage"] =
                    "The payment could not be verified. If you were charged, please contact support.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] =
                    "Payment was received, but the order could not be updated. Please contact support.";

                return RedirectToAction(nameof(Index));
            }
        }
    }
}
