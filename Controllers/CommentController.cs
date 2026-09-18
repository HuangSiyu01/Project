using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using Project.Repositories;

namespace Project.Controllers
{
    [Authorize]
    public class CommentController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;

        public CommentController(
            IUnitOfWork unitOfWork,
            UserManager<ApplicationUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        // POST: /Comment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int postId, string content, int? parentCommentId)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return RedirectToAction("Details", "Post", new { id = postId });
            }

            var comment = new Comment
            {
                PostId = postId,
                Content = content,
                UserId = _userManager.GetUserId(User),
                CreatedAt = DateTime.UtcNow,
                ParentCommentId = parentCommentId
            };

            await _unitOfWork.Comments.AddAsync(comment);
            await _unitOfWork.SaveChangesAsync();

            return RedirectToAction("Details", "Post", new { id = postId });
        }

        // POST: /Comment/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int postId)
        {
            var comment = await _unitOfWork.Comments.Query()
                .Include(c => c.Replies)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (comment.UserId != userId) return Forbid();

            if (comment.Replies != null && comment.Replies.Any())
            {
                foreach (var reply in comment.Replies.ToList())
                {
                    _unitOfWork.Comments.Remove(reply);
                }
            }

            _unitOfWork.Comments.Remove(comment);
            await _unitOfWork.SaveChangesAsync();

            return RedirectToAction("Details", "Post", new { id = postId });
        }
    }
}