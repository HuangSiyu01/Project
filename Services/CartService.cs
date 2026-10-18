using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using Project.Models;

namespace Project.Services
{
    public class CartService
    {
        private const string CartUserSessionKey = "TravelShare.Cart.UserId";
        private const string CartCountSessionKey = "TravelShare.Cart.Count";
        private const string CartUpdatedSessionKey = "TravelShare.Cart.UpdatedAt";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;

        public CartService(
            IHttpContextAccessor httpContextAccessor,
            ApplicationDbContext context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        private HttpContext GetHttpContext()
        {
            return _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException(
                    "The current HTTP context is unavailable.");
        }

        private string GetUserId()
        {
            var httpContext = GetHttpContext();

            var userId = httpContext.User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException(
                    "Please log in to use your shopping cart.");
            }

            // Bind the current shopping cart session to the authenticated user.
            var session = httpContext.Session;
            var sessionUserId = session.GetString(CartUserSessionKey);

            if (!string.Equals(
                sessionUserId,
                userId,
                StringComparison.Ordinal))
            {
                // Do not reuse another user's session cart state.
                session.Remove(CartCountSessionKey);
                session.Remove(CartUpdatedSessionKey);
                session.SetString(CartUserSessionKey, userId);
            }

            return userId;
        }

        private void UpdateSessionCartState(
            string userId,
            int itemCount)
        {
            var session = GetHttpContext().Session;

            session.SetString(CartUserSessionKey, userId);
            session.SetInt32(CartCountSessionKey, itemCount);
            session.SetString(
                CartUpdatedSessionKey,
                DateTimeOffset.UtcNow.ToString("O"));
        }

        private async Task SyncSessionCartStateAsync(string userId)
        {
            var itemCount = await _context.ShoppingCartItems
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .SumAsync(item => (int?)item.Quantity) ?? 0;

            UpdateSessionCartState(userId, itemCount);
        }

        // Used by the navigation-bar ViewComponent. Reuse the Session value
        // when it belongs to the current user; otherwise rebuild it from the database.
        public async Task<int> GetCartCountAsync()
        {
            var userId = GetUserId();
            var session = GetHttpContext().Session;
            var itemCount = session.GetInt32(CartCountSessionKey);

            if (!itemCount.HasValue)
            {
                await SyncSessionCartStateAsync(userId);
                itemCount = session.GetInt32(CartCountSessionKey);
            }

            return Math.Max(itemCount.GetValueOrDefault(), 0);
        }

        public async Task<List<CartItem>> GetCartAsync()
        {
            var userId = GetUserId();

            var cart = await _context.ShoppingCartItems
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .Select(item => new CartItem
                {
                    AttractionId = item.AttractionId,
                    Quantity = item.Quantity
                })
                .ToListAsync();

            UpdateSessionCartState(
                userId,
                cart.Sum(item => item.Quantity));

            return cart;
        }

        public async Task<List<CartItem>> GetValidatedCartAsync()
        {
            var userId = GetUserId();

            var databaseItems = await _context.ShoppingCartItems
                .Include(item => item.Attraction)
                .Where(item => item.UserId == userId)
                .ToListAsync();

            var unavailableItems = databaseItems
                .Where(item =>
                    item.Attraction == null ||
                    !item.Attraction.IsAvailable)
                .ToList();

            if (unavailableItems.Count > 0)
            {
                _context.ShoppingCartItems.RemoveRange(unavailableItems);
                await _context.SaveChangesAsync();
            }

            var validItems = databaseItems
                .Where(item =>
                    item.Attraction != null &&
                    item.Attraction.IsAvailable)
                .Select(item => new CartItem
                {
                    AttractionId = item.AttractionId,
                    Quantity = item.Quantity
                })
                .ToList();

            UpdateSessionCartState(
                userId,
                validItems.Sum(item => item.Quantity));

            return validItems;
        }

        public async Task AddItemAsync(
            int attractionId,
            int quantity = 1)
        {
            if (quantity < 1 || quantity > 20)
            {
                throw new ArgumentException(
                    "Quantity must be between 1 and 20.");
            }

            var userId = GetUserId();

            var attraction = await _context.Attractions
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == attractionId);

            if (attraction == null || !attraction.IsAvailable)
            {
                throw new InvalidOperationException(
                    "This attraction is unavailable.");
            }

            var cartItem = await _context.ShoppingCartItems
                .FirstOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.AttractionId == attractionId);

            if (cartItem == null)
            {
                cartItem = new ShoppingCartItem
                {
                    UserId = userId,
                    AttractionId = attractionId,
                    Quantity = quantity
                };

                _context.ShoppingCartItems.Add(cartItem);
            }
            else
            {
                if (cartItem.Quantity + quantity > 20)
                {
                    throw new ArgumentException(
                        "The maximum quantity for an attraction is 20.");
                }

                cartItem.Quantity += quantity;
            }

            await _context.SaveChangesAsync();
            await SyncSessionCartStateAsync(userId);
        }

        public async Task UpdateQuantityAsync(
            int attractionId,
            int quantity)
        {
            if (quantity < 1 || quantity > 20)
            {
                throw new ArgumentException(
                    "Quantity must be between 1 and 20.");
            }

            var userId = GetUserId();

            var cartItem = await _context.ShoppingCartItems
                .Include(item => item.Attraction)
                .FirstOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.AttractionId == attractionId);

            if (cartItem == null)
            {
                throw new InvalidOperationException(
                    "The selected item was not found in your cart.");
            }

            if (cartItem.Attraction == null ||
                !cartItem.Attraction.IsAvailable)
            {
                throw new InvalidOperationException(
                    "This attraction is unavailable.");
            }

            cartItem.Quantity = quantity;

            await _context.SaveChangesAsync();
            await SyncSessionCartStateAsync(userId);
        }

        public async Task RemoveItemAsync(int attractionId)
        {
            var userId = GetUserId();

            var cartItem = await _context.ShoppingCartItems
                .FirstOrDefaultAsync(item =>
                    item.UserId == userId &&
                    item.AttractionId == attractionId);

            if (cartItem != null)
            {
                _context.ShoppingCartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
            }

            await SyncSessionCartStateAsync(userId);
        }

        public async Task ClearCartAsync()
        {
            var userId = GetUserId();

            var cartItems = await _context.ShoppingCartItems
                .Where(item => item.UserId == userId)
                .ToListAsync();

            if (cartItems.Count > 0)
            {
                _context.ShoppingCartItems.RemoveRange(cartItems);
                await _context.SaveChangesAsync();
            }

            UpdateSessionCartState(userId, 0);
        }

        public async Task RemoveItemsAsync(
            IEnumerable<int> attractionIds)
        {
            var userId = GetUserId();
            var ids = attractionIds.Distinct().ToList();

            if (ids.Count == 0)
            {
                return;
            }

            var cartItems = await _context.ShoppingCartItems
                .Where(item =>
                    item.UserId == userId &&
                    ids.Contains(item.AttractionId))
                .ToListAsync();

            if (cartItems.Count > 0)
            {
                _context.ShoppingCartItems.RemoveRange(cartItems);
                await _context.SaveChangesAsync();
            }

            await SyncSessionCartStateAsync(userId);
        }
    }
}