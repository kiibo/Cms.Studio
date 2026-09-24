using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cms.Studio.Core.Services;

namespace Cms.Studio.Web.Controllers.Admin;

/// <summary>Comment moderation (comment approval).</summary>
[Authorize]
[Route("admin/comments")]
public class AdminCommentsController : Controller
{
    private readonly CommentService _comments;
    private readonly PostService _posts;

    public AdminCommentsController(CommentService comments, PostService posts)
    {
        _comments = comments;
        _posts = posts;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string filter = "pending")
    {
        var onlyPending = filter != "all";
        var comments = await _comments.AdminListAsync(onlyPending);
        var postTitles = new Dictionary<int, string>();
        foreach (var comment in comments)
        {
            if (!postTitles.ContainsKey(comment.PostId))
            {
                var post = await _posts.GetByIdAdminAsync(comment.PostId);
                postTitles[comment.PostId] = post?.Title ?? "(deleted post)";
            }
        }

        ViewBag.Comments = comments;
        ViewBag.PostTitles = postTitles;
        ViewBag.Filter = onlyPending ? "pending" : "all";
        ViewBag.PendingCount = await _comments.CountPendingAsync();
        return View("~/Views/Admin/Comments.cshtml");
    }

    [HttpPost("approve/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        await _comments.ApproveAsync(id);
        return RedirectToAction("Index", new { filter = "pending" });
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _comments.DeleteAsync(id);
        return RedirectToAction("Index", new { filter = "pending" });
    }
}
