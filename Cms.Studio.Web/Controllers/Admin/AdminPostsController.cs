using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cms.Studio.Core.Domain;
using Cms.Studio.Core.Services;
using Cms.Studio.Web.Models;

namespace Cms.Studio.Web.Controllers.Admin;

[Authorize]
[Route("admin")]
public class AdminDashboardController : Controller
{
    private readonly StatsService _stats;
    private readonly PostService _posts;

    public AdminDashboardController(StatsService stats, PostService posts)
    {
        _stats = stats;
        _posts = posts;
    }

    [HttpGet("")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var model = new AdminDashboardViewModel
        {
            Stats = await _stats.GetAsync(),
            Trend = await _posts.GetViewsTrendAsync(30),
            TopPosts = await _posts.GetRankingAsync(RankPeriod.Monthly, 10),
            VisitorTrend = await _stats.GetDailyUniqueVisitorsAsync(30),
            Visitors = await _stats.GetVisitorStatsAsync(30)
        };
        return View("~/Views/Admin/Dashboard.cshtml", model);
    }
}

[Authorize]
[Route("admin/posts")]
public class AdminPostsController : Controller
{
    private readonly PostService _posts;
    private readonly CategoryService _categories;
    private readonly SeriesService _series;
    private readonly SettingService _settings;

    public AdminPostsController(PostService posts, CategoryService categories, SeriesService series, SettingService settings)
    {
        _posts = posts;
        _categories = categories;
        _series = series;
        _settings = settings;
    }

    [HttpGet("")]
    public async Task<IActionResult> List(string? keyword, PostStatus? status, int page = 1)
    {
        ViewBag.Keyword = keyword;
        ViewBag.Status = status;
        ViewBag.Posts = await _posts.AdminSearchAsync(keyword, status, page, 20);
        return View("~/Views/Admin/Posts.cshtml");
    }

    [HttpGet("edit/{id:int?}")]
    public async Task<IActionResult> Edit(int? id)
    {
        var model = new AdminPostEditViewModel
        {
            Categories = await _categories.GetAllAsync(),
            AllSeries = await _series.GetAllAsync()
        };

        if (id is > 0)
        {
            var post = await _posts.GetByIdAdminAsync(id.Value);
            if (post == null)
                return NotFound();
            model.Post = post;
            model.TagsInput = string.Join(", ", post.PostTags.Select(pt => pt.Tag.Name));
            model.SelectedSeriesIds = post.PostSeries.Select(ps => ps.SeriesId).ToList();
        }
        else
        {
            model.Post = new Post
            {
                Status = PostStatus.Draft,
                AuthorName = await _settings.GetAuthorNameAsync(),
                CreatedOnUtc = DateTime.UtcNow
            };
        }

        return View("~/Views/Admin/PostEdit.cshtml", model);
    }

    [HttpPost("edit/{id:int?}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, AdminPostEditViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Post.Title))
            ModelState.AddModelError(string.Empty, "Title is required.");
        if (model.Post.CategoryId <= 0)
            ModelState.AddModelError(string.Empty, "Please choose a category.");

        if (!ModelState.IsValid)
        {
            model.Categories = await _categories.GetAllAsync();
            model.AllSeries = await _series.GetAllAsync();
            return View("~/Views/Admin/PostEdit.cshtml", model);
        }

        var post = id is > 0 ? await _posts.GetByIdAdminAsync(id.Value) ?? model.Post : model.Post;
        post.Title = model.Post.Title;
        post.Slug = model.Post.Slug;
        post.Summary = model.Post.Summary;
        post.ContentMd = model.Post.ContentMd;
        post.CoverImageUrl = model.Post.CoverImageUrl;
        // Author is not editable: new posts are owned by the signed-in admin,
        // existing posts keep their original author (filled in lazily when empty).
        if (string.IsNullOrWhiteSpace(post.AuthorName))
            post.AuthorName = User.Identity?.Name ?? "Admin";
        post.CategoryId = model.Post.CategoryId;
        post.Status = model.Post.Status;
        post.IsFixedTop = model.Post.IsFixedTop;
        post.MetaTitle = model.Post.MetaTitle;
        post.MetaDescription = model.Post.MetaDescription;
        post.PublishedOnUtc = model.Post.PublishedOnUtc;

        await _posts.SaveAsync(post, model.TagsInput, model.SelectedSeriesIds);
        return RedirectToAction("List");
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _posts.DeleteAsync(id);
        return RedirectToAction("List");
    }
}
