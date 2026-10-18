using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using Project.Dtos;
using Project.Models;
using Project.Services;
using Stripe;
using Stripe.Checkout;

namespace Project.Controllers.Api
{
    /// <summary>
    /// RESTful endpoints used to start Stripe Checkout payments.
    /// </summary>
    [ApiController]
    [Route("api/payments")]
    [Authorize(Roles = SD.Role_Customer)]
    public class PaymentsController : ControllerBase
    {
        private readonly CartService _cartService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            CartService cartService,
            ApplicationDbContext context,
            ILogger<PaymentsController> logger)
        {
            _cartService = cartService;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Creates a pending order and a Stripe Checkout Session for selected cart items.
        /// POST /api/payments/checkout-session
        /// </summary>
        [HttpPost("checkout-session")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCheckoutSession(
            [FromBody] CreateCheckoutSessionRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new
                {
                    message = "Please sign in before checkout."
                });
            }

            if (request.SelectedAttractionIds == null ||
                request.SelectedAttractionIds.Count == 0)
            {
                return BadRequest(new
                {
                    message = "Select at least one attraction to checkout."
                });
            }

            var selectedIds = request.SelectedAttractionIds
                .Distinct()
                .ToList();

            if (selectedIds.Count == 0 || selectedIds.Any(id => id <= 0))
            {
                return BadRequest(new
                {
                    message = "Select valid attractions to checkout."
                });
            }

            Order? pendingOrder = null;

            try
            {
                // Read the current user's saved cart. The browser does not submit prices
                // or quantities, so it cannot change the amount charged to Stripe.
                var cart = await _cartService.GetValidatedCartAsync();
                var selectedCartItems = cart
                    .Where(item => selectedIds.Contains(item.AttractionId))
                    .ToList();

                if (selectedCartItems.Count == 0 ||
                    selectedCartItems.Count != selectedIds.Count)
                {
                    return BadRequest(new
                    {
                        message = "Some selected items are no longer available in your cart. Refresh the page and try again."
                    });
                }

                // Prices and availability are always checked against the database.
                var attractions = await _context.Attractions
                    .AsNoTracking()
                    .Where(item => selectedIds.Contains(item.Id) && item.IsAvailable)
                    .ToDictionaryAsync(item => item.Id);

                if (attractions.Count != selectedIds.Count)
                {
                    return BadRequest(new
                    {
                        message = "One or more selected attractions are unavailable."
                    });
                }

                decimal totalAmount = 0m;
                var lineItems = new List<SessionLineItemOptions>();
                var orderItems = new List<OrderItem>();

                foreach (var cartItem in selectedCartItems)
                {
                    var attraction = attractions[cartItem.AttractionId];

                    if (cartItem.Quantity < 1 ||
                        cartItem.Quantity > 20 ||
                        attraction.TicketPrice <= 0)
                    {
                        return BadRequest(new
                        {
                            message = "One or more items have an invalid quantity or price."
                        });
                    }

                    var unitPrice = decimal.Round(
                        attraction.TicketPrice,
                        2,
                        MidpointRounding.AwayFromZero);

                    var unitAmountInCents = (long)decimal.Round(
                        unitPrice * 100m,
                        0,
                        MidpointRounding.AwayFromZero);

                    totalAmount += unitPrice * cartItem.Quantity;

                    lineItems.Add(new SessionLineItemOptions
                    {
                        Quantity = cartItem.Quantity,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "nzd",
                            UnitAmount = unitAmountInCents,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = attraction.Name,
                                Description = attraction.City
                            }
                        }
                    });

                    orderItems.Add(new OrderItem
                    {
                        AttractionId = attraction.Id,
                        Quantity = cartItem.Quantity,
                        UnitPrice = unitPrice
                    });
                }

                totalAmount = decimal.Round(
                    totalAmount,
                    2,
                    MidpointRounding.AwayFromZero);

                if (totalAmount <= 0m)
                {
                    return BadRequest(new
                    {
                        message = "The order total must be greater than zero."
                    });
                }

                // Save the pending order first so the Stripe Session can be linked to
                // a real order ID, matching the order/session workflow from the lab.
                pendingOrder = new Order
                {
                    UserId = userId,
                    OrderStatus = SD.StatusPending,
                    PaymentStatus = SD.PaymentPending,
                    TotalAmount = totalAmount,
                    CreatedAt = DateTime.UtcNow,
                    Items = orderItems
                };

                _context.Orders.Add(pendingOrder);
                await _context.SaveChangesAsync();

                var checkoutOptions = new SessionCreateOptions
                {
                    Mode = "payment",
                    ClientReferenceId = pendingOrder.Id.ToString(),
                    SuccessUrl = $"{Request.Scheme}://{Request.Host}/Customer/Cart/Success?session_id={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = $"{Request.Scheme}://{Request.Host}/Customer/Cart/Index",
                    LineItems = lineItems,
                    Metadata = new Dictionary<string, string>
                    {
                        ["UserId"] = userId,
                        ["OrderId"] = pendingOrder.Id.ToString(),
                        ["AttractionIds"] = string.Join(",", selectedIds)
                    }
                };

                var stripeService = new SessionService();
                var stripeSession = await stripeService.CreateAsync(checkoutOptions);

                if (string.IsNullOrWhiteSpace(stripeSession.Id) ||
                    string.IsNullOrWhiteSpace(stripeSession.Url))
                {
                    pendingOrder.PaymentStatus = SD.PaymentFailed;
                    pendingOrder.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogError(
                        "Stripe returned an incomplete Checkout Session for order {OrderId}.",
                        pendingOrder.Id);

                    return StatusCode(StatusCodes.Status502BadGateway, new
                    {
                        message = "The payment provider could not start checkout. Please try again."
                    });
                }

                // Store both Stripe identifiers on the order. The PaymentIntent may
                // remain empty until payment is completed; Success() fills it in later.
                pendingOrder.StripeSessionId = stripeSession.Id;
                pendingOrder.StripePaymentIntentId = stripeSession.PaymentIntentId;
                pendingOrder.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    checkoutUrl = stripeSession.Url,
                    sessionId = stripeSession.Id,
                    orderId = pendingOrder.Id
                });
            }
            catch (StripeException ex)
            {
                _logger.LogError(
                    ex,
                    "Stripe Checkout Session creation failed for user {UserId}.",
                    userId);

                await MarkOrderPaymentFailedAsync(pendingOrder);

                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = "The payment provider could not start checkout. Please try again."
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "The pending order could not be saved for user {UserId}.",
                    userId);

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "The order could not be saved. Please try again."
                });
            }
        }

        private async Task MarkOrderPaymentFailedAsync(Order? order)
        {
            if (order == null || order.Id <= 0 ||
                !string.IsNullOrWhiteSpace(order.StripeSessionId))
            {
                return;
            }

            order.PaymentStatus = SD.PaymentFailed;
            order.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(
                    ex,
                    "Could not mark order {OrderId} as failed after Stripe returned an error.",
                    order.Id);
            }
        }
    }
}

