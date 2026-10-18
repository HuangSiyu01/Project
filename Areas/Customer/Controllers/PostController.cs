using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using Project.Models;
using Project.Repositories;
using System.Text;

namespace Project.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class PostController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _context;

        public PostController(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            ApplicationDbContext context)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _context = context;
        }

        // Search user posts or attractions in the same Index page.
        public async Task<IActionResult> Index(
            string? keyword,
            string? searchType = "posts")
        {
            keyword = keyword?.Trim();

            searchType = string.Equals(
                searchType,
                "attractions",
                StringComparison.OrdinalIgnoreCase)
                ? "attractions"
                : "posts";

            ViewBag.Keyword = keyword;
            ViewBag.SearchType = searchType;

            if (string.IsNullOrWhiteSpace(keyword))
            {
                ViewBag.Attractions = new List<Attraction>();
                return View(new List<Post>());
            }

            if (searchType == "attractions")
            {
                var allAttractions = await _context.Attractions
                    .AsNoTracking()
                    .Where(a => a.IsAvailable)
                    .ToListAsync();

                var normalizedKeyword = NormalizeSearchText(keyword);

                var attractions = allAttractions
                    .Where(a =>
                        NormalizeSearchText(a.Name).Contains(normalizedKeyword) ||
                        NormalizeSearchText(a.City).Contains(normalizedKeyword))
                    .OrderBy(a => a.Name)
                    .ToList();

                ViewBag.Attractions = attractions;

                return View(new List<Post>());
            }

            ViewBag.Attractions = new List<Attraction>();

            var posts = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .Include(p => p.User)
                .Where(p =>
                    p.Location != null &&
                    EF.Functions.Like(p.Location, $"%{keyword}%"))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(posts);
        }

        private static string NormalizeSearchText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(
                value.Normalize(NormalizationForm.FormKC)
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant)
                    .ToArray());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var post = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                return NotFound();
            }

            var allComments = await _unitOfWork.Comments.Query()
                .Include(c => c.User)
                .Include(c => c.ParentComment)
                    .ThenInclude(pc => pc.User)
                .Where(c => c.PostId == id)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            post.Comments = allComments;

            return View(post);
        }

        [Authorize]
        public IActionResult Create()
        {
            return View(new Post { Rating = 0 });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Post post,
            List<IFormFile>? images)
        {
            if (!ModelState.IsValid)
            {
                return View(post);
            }

            if (!string.IsNullOrWhiteSpace(post.Review))
            {
                var wordCount = post.Review
                    .Split(
                        new[] { ' ', '\n', '\r', '\t' },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Length;

                if (wordCount > 500)
                {
                    ModelState.AddModelError(
                        "Review",
                        "Review cannot exceed 500 words.");

                    return View(post);
                }
            }

            if (images != null && images.Count > 18)
            {
                ModelState.AddModelError(
                    "images",
                    "You can upload at most 18 images.");

                return View(post);
            }

            post.UserId = _userManager.GetUserId(User);
            post.CreatedAt = DateTime.UtcNow;

            await _unitOfWork.Posts.AddAsync(post);
            await _unitOfWork.SaveChangesAsync();

            if (images != null && images.Count > 0)
            {
                var uploadsFolder = Path.Combine(
                    _webHostEnvironment.WebRootPath,
                    "uploads");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                foreach (var image in images)
                {
                    if (image.Length == 0)
                    {
                        continue;
                    }

                    var fileName =
                        Guid.NewGuid().ToString() +
                        Path.GetExtension(image.FileName);

                    var filePath = Path.Combine(
                        uploadsFolder,
                        fileName);

                    using (var stream = new FileStream(
                        filePath,
                        FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    await _unitOfWork.PostImages.AddAsync(
                        new PostImage
                        {
                            ImageUrl = "/uploads/" + fileName,
                            PostId = post.Id
                        });
                }

                await _unitOfWork.SaveChangesAsync();
            }

            TempData["success"] = "Post created successfully.";

            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var post = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            if (post.UserId != userId)
            {
                return Forbid();
            }

            return View(post);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Post post,
            List<IFormFile>? images)
        {
            if (id != post.Id)
            {
                return NotFound();
            }

            var existing = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existing == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            if (existing.UserId != userId)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return View(post);
            }

            if (!string.IsNullOrWhiteSpace(post.Review))
            {
                var wordCount = post.Review
                    .Split(
                        new[] { ' ', '\n', '\r', '\t' },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Length;

                if (wordCount > 500)
                {
                    ModelState.AddModelError(
                        "Review",
                        "Review cannot exceed 500 words.");

                    return View(post);
                }
            }

            existing.Title = post.Title;
            existing.Location = post.Location;
            existing.Rating = post.Rating;
            existing.Review = post.Review;

            _unitOfWork.Posts.Update(existing);

            if (images != null && images.Count > 0)
            {
                var uploadsFolder = Path.Combine(
                    _webHostEnvironment.WebRootPath,
                    "uploads");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                foreach (var image in images)
                {
                    if (image.Length == 0)
                    {
                        continue;
                    }

                    var fileName =
                        Guid.NewGuid().ToString() +
                        Path.GetExtension(image.FileName);

                    var filePath = Path.Combine(
                        uploadsFolder,
                        fileName);

                    using (var stream = new FileStream(
                        filePath,
                        FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    await _unitOfWork.PostImages.AddAsync(
                        new PostImage
                        {
                            ImageUrl = "/uploads/" + fileName,
                            PostId = existing.Id
                        });
                }
            }

            await _unitOfWork.SaveChangesAsync();

            TempData["success"] = "Post updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var post = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            if (post.UserId != userId)
            {
                return Forbid();
            }

            return View(post);
        }

        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var post = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);

            if (post.UserId != userId)
            {
                return Forbid();
            }

            _unitOfWork.Posts.Remove(post);
            await _unitOfWork.SaveChangesAsync();

            TempData["success"] = "Post deleted successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}