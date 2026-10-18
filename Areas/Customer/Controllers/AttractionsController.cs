using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Data;

namespace Project.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class AttractionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AttractionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // The standalone Explore Attractions page has been retired.
        // Send old links to the homepage instead. Users can still search
        // attractions from the integrated homepage search.
        public IActionResult Index(string? keyword)
        {
            return Redirect("~/");
        }

        // Display attraction details and its existing photos.
        public async Task<IActionResult> Details(
            int? id,
            string? keyword)
        {
            if (id == null)
            {
                return NotFound();
            }

            var attraction = await _context.Attractions
                .AsNoTracking()
                .Include(a => a.Images)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (attraction == null)
            {
                return NotFound();
            }

            // Exclude the main image from additional photos
            // and remove duplicate additional image URLs.
            if (attraction.Images != null)
            {
                var mainImage = NormalizeImageUrl(attraction.ImageUrl);

                var uniqueImages = attraction.Images
                    .Where(i => !string.IsNullOrWhiteSpace(i.ImageUrl))
                    .Where(i =>
                        NormalizeImageUrl(i.ImageUrl) != mainImage)
                    .GroupBy(i => NormalizeImageUrl(i.ImageUrl))
                    .Select(g => g.First())
                    .ToList();

                attraction.Images = uniqueImages;
            }

            ViewBag.Keyword = keyword;

            return View(attraction);
        }

        private static string NormalizeImageUrl(string? url)
        {
            return (url ?? string.Empty)
                .Trim()
                .Replace('\\', '/');
        }
    }
}