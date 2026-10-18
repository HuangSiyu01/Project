using Microsoft.AspNetCore.Mvc;
using Project.Services;

namespace Project.ViewComponents
{
    public class ShoppingCartViewComponent : ViewComponent
    {
        private readonly CartService _cartService;

        public ShoppingCartViewComponent(CartService cartService)
        {
            _cartService = cartService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // A guest has no database-backed cart in this application. Clear any
            // leftover temporary Session state but do not remove saved cart rows.
            if (User.Identity?.IsAuthenticated != true)
            {
                HttpContext.Session.Clear();
                return View(0);
            }

            // The shared layout only invokes this component for guests and customers.
            // Use the Session count if present; otherwise CartService reloads it from DB.
            var count = await _cartService.GetCartCountAsync();
            return View(count);
        }
    }
}