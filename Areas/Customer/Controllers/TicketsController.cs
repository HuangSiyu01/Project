using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using Project.Models;
using Project.Services;

namespace Project.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize(Roles = SD.Role_Customer)]
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly TicketService _ticketService;

        public TicketsController(ApplicationDbContext db, TicketService ticketService)
        {
            _db = db;
            _ticketService = ticketService;
        }

        // GET: /Customer/Tickets
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            // Only approved and successfully paid orders may have visible e-tickets.
            // Backfill is safe for older approved orders and will not create duplicates.
            var eligibleOrderIds = await _db.Orders
                .Where(o => o.UserId == userId &&
                    o.OrderStatus == SD.StatusApproved &&
                    (o.PaymentStatus == SD.PaymentPaid ||
                     o.PaymentStatus == SD.PaymentRefunded))
                .Select(o => o.Id)
                .ToListAsync();

            foreach (var orderId in eligibleOrderIds)
            {
                await _ticketService.EnsureTicketsForOrderAsync(orderId);
            }

            var tickets = await _db.Tickets
                .AsNoTracking()
                .Where(t => t.Order != null &&
                    t.Order.UserId == userId &&
                    t.Order.OrderStatus == SD.StatusApproved)
                .Include(t => t.Order)
                .Include(t => t.OrderItem)
                    .ThenInclude(item => item!.Attraction)
                .OrderByDescending(t => t.IssuedAt)
                .ThenBy(t => t.TicketNumber)
                .ToListAsync();

            return View(tickets);
        }
    }
}

