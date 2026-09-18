using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using Project.Repositories;

namespace Project.Controllers
{
    [Authorize]
    public class InteractionController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;

        public InteractionController(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        // POST: /Interaction/ToggleLike
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike(int postId, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User);

            var existing = await _unitOfWork.Likes.Query()
                .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

            if (existing != null)
            {
                _unitOfWork.Likes.Remove(existing);
            }
            else
            {
                await _unitOfWork.Likes.AddAsync(new Like
                {
                    PostId = postId,
                    UserId = userId
                });
            }

            await _unitOfWork.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Details", "Post", new { id = postId });
        }

        // POST: /Interaction/ToggleFavorite
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFavorite(int postId, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User);

            var existing = await _unitOfWork.Favorites.Query()
                .FirstOrDefaultAsync(f => f.PostId == postId && f.UserId == userId);

            if (existing != null)
            {
                _unitOfWork.Favorites.Remove(existing);
            }
            else
            {
                await _unitOfWork.Favorites.AddAsync(new Favorite
                {
                    PostId = postId,
                    UserId = userId
                });
            }

            await _unitOfWork.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Details", "Post", new { id = postId });
        }

        // POST: /Interaction/ToggleFollow
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFollow(string followeeId, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User);

            if (userId == followeeId)
            {
                return BadRequest();
            }

            var existing = await _unitOfWork.Follows.Query()
                .FirstOrDefaultAsync(f => f.FollowerId == userId && f.FolloweeId == followeeId);

            if (existing != null)
            {
                _unitOfWork.Follows.Remove(existing);
            }
            else
            {
                await _unitOfWork.Follows.AddAsync(new Follow
                {
                    FollowerId = userId,
                    FolloweeId = followeeId
                });
            }

            await _unitOfWork.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Post");
        }
    }
}
