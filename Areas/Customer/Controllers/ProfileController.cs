using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using Project.Repositories;
using Project.ViewModels;

namespace Project.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProfileController(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: /Profile/Index
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            var model = new ProfileViewModel
            {
                UserId = userId,
                DisplayName = user?.DisplayName,
                AvatarUrl = user?.AvatarUrl,
                Bio = user?.Bio,

                MyPosts = await _unitOfWork.Posts.Query()
                    .Include(p => p.Images)
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync(),

                LikedPosts = await _unitOfWork.Likes.Query()
                    .Where(l => l.UserId == userId)
                    .Include(l => l.Post).ThenInclude(p => p.Images)
                    .Select(l => l.Post)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync(),

                FavoritePosts = await _unitOfWork.Favorites.Query()
                    .Where(f => f.UserId == userId)
                    .Include(f => f.Post).ThenInclude(p => p.Images)
                    .Select(f => f.Post)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync(),

                FollowingUsers = await _unitOfWork.Follows.Query()
                    .Where(f => f.FollowerId == userId)
                    .Include(f => f.Followee)
                    .Select(f => f.Followee)
                    .ToListAsync(),

                FollowerUsers = await _unitOfWork.Follows.Query()
                    .Where(f => f.FolloweeId == userId)
                    .Include(f => f.Follower)
                    .Select(f => f.Follower)
                    .ToListAsync()
            };

            return View(model);
        }

        // GET: /Profile/Edit
        [HttpGet]
        public async Task<IActionResult> Edit()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null) return NotFound();

            var model = new EditProfileViewModel
            {
                DisplayName = user.DisplayName,
                Bio = user.Bio
            };

            return View(model);
        }

        // POST: /Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null) return NotFound();

            user.DisplayName = model.DisplayName;
            user.Bio = model.Bio;

            if (model.Avatar != null && model.Avatar.Length > 0)
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "avatars");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                if (!string.IsNullOrEmpty(user.AvatarUrl))
                {
                    var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, user.AvatarUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.Avatar.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Avatar.CopyToAsync(stream);
                }

                user.AvatarUrl = "/avatars/" + fileName;
            }

            await _userManager.UpdateAsync(user);

            TempData["success"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Profile/UserProfile/{id}
        public async Task<IActionResult> UserProfile(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(id);

            if (user == null) return NotFound();

            var isFollowing = await _unitOfWork.Follows.Query()
                .AnyAsync(f => f.FollowerId == currentUserId && f.FolloweeId == id);

            var model = new UserProfileViewModel
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                Bio = user.Bio,
                IsCurrentUser = currentUserId == user.Id,
                IsFollowing = isFollowing,

                Posts = await _unitOfWork.Posts.Query()
                    .Include(p => p.Images)
                    .Where(p => p.UserId == id)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync(),

                FollowerCount = await _unitOfWork.Follows.Query()
                    .CountAsync(f => f.FolloweeId == id),

                FollowingCount = await _unitOfWork.Follows.Query()
                    .CountAsync(f => f.FollowerId == id)
            };

            return View(model);
        }
    }
}