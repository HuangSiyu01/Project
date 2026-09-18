using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using Project.Repositories;

namespace Project.Controllers
{
    public class PostController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public PostController(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: /Post/Index?keyword=wellington
        public async Task<IActionResult> Index(string? keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                ViewBag.Keyword = null;
                return View(new List<Post>());
            }

            var k = keyword.Trim();

            var posts = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .Include(p => p.User)
                .Where(p => EF.Functions.Like(p.Location, $"%{k}%"))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            ViewBag.Keyword = keyword;
            return View(posts);
        }

        // GET: /Post/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var post = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .Include(p => p.User)
                .Include(p => p.Comments).ThenInclude(c => c.User)
                .Include(p => p.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            return View(post);
        }

        // GET: /Post/Create
        [Authorize]
        public IActionResult Create()
        {
            return View(new Post { Rating = 0 });
        }

        // POST: /Post/Create
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Post post, List<IFormFile>? images)
        {
            if (!ModelState.IsValid) return View(post);

            // Review 最多 500 词
            if (!string.IsNullOrWhiteSpace(post.Review))
            {
                var wordCount = post.Review
                    .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Length;

                if (wordCount > 500)
                {
                    ModelState.AddModelError("Review", "Review cannot exceed 500 words.");
                    return View(post);
                }
            }

            if (images != null && images.Count > 18)
            {
                ModelState.AddModelError("images", "You can upload at most 18 images.");
                return View(post);
            }

            post.UserId = _userManager.GetUserId(User);
            post.CreatedAt = DateTime.UtcNow;

            await _unitOfWork.Posts.AddAsync(post);
            await _unitOfWork.SaveChangesAsync();

            if (images != null && images.Count > 0)
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                foreach (var image in images)
                {
                    if (image.Length == 0) continue;

                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    await _unitOfWork.PostImages.AddAsync(new PostImage
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

        // GET: /Post/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var post = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (post.UserId != userId) return Forbid();

            return View(post);
        }

        // POST: /Post/Edit/5
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Post post, List<IFormFile>? images)
        {
            if (id != post.Id) return NotFound();

            var existing = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existing == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (existing.UserId != userId) return Forbid();

            if (!ModelState.IsValid) return View(post);

            if (!string.IsNullOrWhiteSpace(post.Review))
            {
                var wordCount = post.Review
                    .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Length;

                if (wordCount > 500)
                {
                    ModelState.AddModelError("Review", "Review cannot exceed 500 words.");
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
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                foreach (var image in images)
                {
                    if (image.Length == 0) continue;

                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    await _unitOfWork.PostImages.AddAsync(new PostImage
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

        // GET: /Post/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var post = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (post.UserId != userId) return Forbid();

            return View(post);
        }

        // POST: /Post/Delete/5
        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var post = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (post.UserId != userId) return Forbid();

            _unitOfWork.Posts.Remove(post);
            await _unitOfWork.SaveChangesAsync();

            TempData["success"] = "Post deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}