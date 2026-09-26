using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cms.Studio.Core.Services;

namespace Cms.Studio.Web.Controllers.Admin;

/// <summary>Comment moderation, modelled on nopCommerce's Blog comments admin list.</summary>
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
    public async Task<IActionResult> Index(string? keyword, string status = "all", string? filter = null)
    {
        // Legacy links (?filter=pending|all) keep working.
        if (!string.IsNullOrEmpty(filter))
            status = filter == "pending" ? "pending" : "all";

        bool? approved = status switch
        {
            "approved" => true,
            "pending" => false,
            _ => null
        };

        var comments = await _comments.AdminSearchAsync(keyword, approved);

        var postTitles = new Dictionary<int, string>();
        foreach (var postId in comments.Select(c => c.PostId).Distinct())
        {
            var post = await _posts.GetByIdAdminAsync(postId);
            postTitles[postId] = post?.Title ?? "(deleted post)";
        }

        ViewBag.Comments = comments;
        ViewBag.PostTitles = postTitles;
        ViewBag.Keyword = keyword ?? string.Empty;
        ViewBag.Status = status;
        ViewBag.PendingCount = await _comments.CountPendingAsync();
        return View("~/Views/Admin/Comments.cshtml");
    }

    /// <summary>
    /// Handles both row actions (single=approve:12) and bulk actions (bulkAction=approve&ids=…).
    /// One form on the page, so row buttons and bulk buttons post here.
    /// </summary>
    [HttpPost("action")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Action(string? bulkAction, string? single, int[] ids, string status = "all", string? keyword = null)
    {
        if (!string.IsNullOrEmpty(single))
        {
            var parts = single.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out var id))
            {
                switch (parts[0])
                {
                    case "approve":
                        await _comments.SetApprovedAsync(id, true);
                        break;
                    case "disapprove":
                        await _comments.SetApprovedAsync(id, false);
                        break;
                    case "delete":
                        await _comments.DeleteAsync(id);
                        break;
                }
            }
        }
        else if (!string.IsNullOrEmpty(bulkAction) && ids is { Length: > 0 })
        {
            switch (bulkAction)
            {
                case "approve":
                    await _comments.SetApprovedManyAsync(ids, true);
                    break;
                case "disapprove":
                    await _comments.SetApprovedManyAsync(ids, false);
                    break;
                case "delete":
                    await _comments.DeleteManyAsync(ids);
                    break;
            }
        }

        return RedirectToAction("Index", new { status, keyword });
    }
}
