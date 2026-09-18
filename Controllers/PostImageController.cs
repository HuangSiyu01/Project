using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using Project.Repositories;

namespace Project.Controllers
{
    [Authorize]
    public class PostImageController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public PostImageController(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: /PostImage/Index?postId=5
        public async Task<IActionResult> Index(int postId)
        {
            var post = await _unitOfWork.Posts.Query()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == postId);

            if (post == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (post.UserId != userId) return Forbid();

            ViewBag.PostId = postId;
            ViewBag.PostTitle = post.Title;

            return View(post.Images.ToList());
        }

        // GET: /PostImage/Create?postId=5
        public async Task<IActionResult> Create(int postId)
        {
            var post = await _unitOfWork.Posts.GetByIdAsync(postId);
            if (post == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (post.UserId != userId) return Forbid();

            ViewBag.PostId = postId;
            return View();
        }

        // POST: /PostImage/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int postId, List<IFormFile> images)
        {
            var post = await _unitOfWork.Posts.GetByIdAsync(postId);
            if (post == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (post.UserId != userId) return Forbid();

            if (images == null || images.Count == 0)
            {
                ModelState.AddModelError("images", "Please select at least one image.");
                ViewBag.PostId = postId;
                return View();
            }

            if (images.Count > 18)
            {
                ModelState.AddModelError("images", "You can upload at most 18 images at once.");
                ViewBag.PostId = postId;
                return View();
            }

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
                    PostId = postId
                });
            }

            await _unitOfWork.SaveChangesAsync();

            TempData["success"] = "Images added successfully.";
            return RedirectToAction(nameof(Index), new { postId });
        }

        // GET: /PostImage/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var image = await _unitOfWork.PostImages.Query()
                .Include(pi => pi.Post)
                .FirstOrDefaultAsync(pi => pi.Id == id);

            if (image == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (image.Post.UserId != userId) return Forbid();

            return View(image);
        }

        // POST: /PostImage/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IFormFile image)
        {
            var existing = await _unitOfWork.PostImages.Query()
                .Include(pi => pi.Post)
                .FirstOrDefaultAsync(pi => pi.Id == id);

            if (existing == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (existing.Post.UserId != userId) return Forbid();

            if (image == null || image.Length == 0)
            {
                ModelState.AddModelError("image", "Please select a new image.");
                return View(existing);
            }

            // Delete old file
            var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, existing.ImageUrl.TrimStart('/'));
            if (System.IO.File.Exists(oldPath))
            {
                System.IO.File.Delete(oldPath);
            }

            // Save new file
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            existing.ImageUrl = "/uploads/" + fileName;
            _unitOfWork.PostImages.Update(existing);
            await _unitOfWork.SaveChangesAsync();

            TempData["success"] = "Image updated successfully.";
            return RedirectToAction(nameof(Index), new { postId = existing.PostId });
        }

        // GET: /PostImage/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var image = await _unitOfWork.PostImages.Query()
                .Include(pi => pi.Post)
                .FirstOrDefaultAsync(pi => pi.Id == id);

            if (image == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (image.Post.UserId != userId) return Forbid();

            return View(image);
        }

        // POST: /PostImage/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var image = await _unitOfWork.PostImages.Query()
                .Include(pi => pi.Post)
                .FirstOrDefaultAsync(pi => pi.Id == id);

            if (image == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (image.Post.UserId != userId) return Forbid();

            var postId = image.PostId;

            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, image.ImageUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            _unitOfWork.PostImages.Remove(image);
            await _unitOfWork.SaveChangesAsync();

            TempData["success"] = "Image deleted successfully.";
            return RedirectToAction(nameof(Index), new { postId });
        }
    }
}