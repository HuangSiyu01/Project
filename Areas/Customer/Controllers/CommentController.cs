using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using Project.Repositories;

namespace Project.Areas.Customer.Controllers
{
    [Area("Customer")]
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
            var comment = await _unitOfWork.Comments.GetByIdAsync(id);

            if (comment == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (comment.UserId != userId) return Forbid();

            // 递归删除该评论及其所有子回复
            await DeleteCommentAndReplies(comment.Id);

            await _unitOfWork.SaveChangesAsync();

            return RedirectToAction("Details", "Post", new { id = postId });
        }

        private async Task DeleteCommentAndReplies(int commentId)
        {
            var replies = await _unitOfWork.Comments.Query()
                .Where(c => c.ParentCommentId == commentId)
                .ToListAsync();

            foreach (var reply in replies)
            {
                await DeleteCommentAndReplies(reply.Id);
            }

            var comment = await _unitOfWork.Comments.GetByIdAsync(commentId);
            if (comment != null)
            {
                _unitOfWork.Comments.Remove(comment);
            }
        }
    }
}