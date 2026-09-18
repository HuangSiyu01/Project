using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using Project.Repositories;
using Project.ViewModels;

namespace Project.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
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