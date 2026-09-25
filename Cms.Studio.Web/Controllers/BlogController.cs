using Microsoft.AspNetCore.Mvc;
using Cms.Studio.Core.Domain;
using Cms.Studio.Core.Services;
using Cms.Studio.Web.Helpers;
using Cms.Studio.Web.Models;

namespace Cms.Studio.Web.Controllers;

public class BlogController : Controller
{
    private readonly PostService _posts;
    private readonly CategoryService _categories;
    private readonly TagService _tags;
    private readonly SeriesService _series;
    private readonly ContentService _content;
    private readonly SettingService _settings;
    private readonly CommentService _comments;
    private readonly EmailVerificationService _codes;

    public BlogController(PostService posts, CategoryService categories, TagService tags,
        SeriesService series, ContentService content, SettingService settings,
        CommentService comments, EmailVerificationService codes)
    {
        _posts = posts;
        _categories = categories;
        _tags = tags;
        _series = series;
        _content = content;
        _settings = settings;
        _comments = comments;
        _codes = codes;
    }

    // ---------- post detail ----------

    [HttpGet("/blog/{slug}")]
    public async Task<IActionResult> Post(string slug, int? replyTo)
    {
        var post = await _posts.GetBySlugAsync(slug);
        if (post == null)
        {
            var redirectSlug = await _posts.GetRedirectSlugAsync(slug);
            if (redirectSlug != null)
                return RedirectPermanent($"/blog/{redirectSlug}");
            return NotFound();
        }

        await _posts.RegisterViewAsync(post.Id);

        var form = new CommentFormModel { ParentId = replyTo };
        var model = await BuildPostViewModelAsync(post, form);
        await PreparePostMetaAsync(post);
        return View(model);
    }

    /// <summary>Raw Markdown mirror of a post — the friendliest format for LLM crawlers.</summary>
    [HttpGet("/blog/{slug}.md")]
    [HttpGet("/blog/{slug}/index.md")]
    public async Task<IActionResult> PostMarkdown(string slug)
    {
        var post = await _posts.GetBySlugAsync(slug);
        if (post == null)
        {
            var redirectSlug = await _posts.GetRedirectSlugAsync(slug);
            if (redirectSlug != null)
                return RedirectPermanent($"/blog/{redirectSlug}.md");
            return NotFound();
        }

        var header = $"# {post.Title}\n\n> {post.Summary}\n\nSource: {Ui.Absolute(await _settings.GetBaseUrlAsync(), $"/blog/{post.Slug}")}\nPublished: {Ui.Date(post.PublishedOnUtc)}\n\n";
        return Content(header + post.ContentMd, "text/markdown; charset=utf-8");
    }

    // ---------- comments ----------

    /// <summary>
    /// Handles both comment buttons on the form: "send-code" (issue the email verification
    /// code and re-render the page keeping every entered value) and "post" (validate the
    /// code and create the comment awaiting moderation).
    /// </summary>
    [HttpPost("/blog/{slug}/comment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitComment(string slug, CommentFormModel form, string action)
    {
        var post = await _posts.GetBySlugAsync(slug);
        if (post == null)
            return NotFound();

        if (action == "send-code")
        {
            var result = await _codes.IssueAsync(form.Email, await _settings.GetSiteTitleAsync());
            var sendModel = await BuildPostViewModelAsync(post, form);
            if (!result.Success)
                ModelState.AddModelError(string.Empty, result.Message);
            sendModel.CommentNotice = result.Success ? result.Message : null;
            await PreparePostMetaAsync(post);
            return View("Post", sendModel);
        }

        // ---- post comment: validate everything before consuming the code ----
        if (form.NickName.Trim().Length is < 2 or > 50)
            ModelState.AddModelError(nameof(form.NickName), "Nick name must be between 2 and 50 characters.");
        if (!EmailVerificationService.IsEmail(form.Email))
            ModelState.AddModelError(nameof(form.Email), "Please enter a valid email address.");
        if (string.IsNullOrWhiteSpace(form.Content))
            ModelState.AddModelError(nameof(form.Content), "The comment cannot be empty.");
        else if (form.Content.Trim().Length > 2000)
            ModelState.AddModelError(nameof(form.Content), "The comment is too long (2000 characters max).");

        if (form.ParentId is > 0)
        {
            var parent = (await _comments.GetApprovedForPostAsync(post.Id)).FirstOrDefault(c => c.Id == form.ParentId);
            if (parent == null)
                ModelState.AddModelError(string.Empty, "The comment you are replying to no longer exists.");
        }

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildPostViewModelAsync(post, form);
            await PreparePostMetaAsync(post);
            return View("Post", invalidModel);
        }

        if (await _comments.IsDuplicateAsync(post.Id, form.Email, form.Content, TimeSpan.FromMinutes(5)))
        {
            ModelState.AddModelError(string.Empty, "You already posted this exact comment a moment ago.");
            var dupModel = await BuildPostViewModelAsync(post, form);
            await PreparePostMetaAsync(post);
            return View("Post", dupModel);
        }

        if (!_codes.TryConsume(form.Email, form.Code))
        {
            ModelState.AddModelError(nameof(form.Code), "The verification code is invalid or expired. Please request a new one.");
            var codeModel = await BuildPostViewModelAsync(post, form);
            await PreparePostMetaAsync(post);
            return View("Post", codeModel);
        }

        var isAdmin = User.Identity?.IsAuthenticated == true;
        var comment = await _comments.CreateAsync(post.Id, form.ParentId, form.NickName, form.Email, form.Content);
        if (isAdmin)
            await _comments.ApproveAsync(comment.Id);

        TempData["CommentNotice"] = isAdmin
            ? "Your comment has been posted."
            : "Thanks! Your comment is awaiting moderation and will appear once it is approved.";
        return Redirect($"/blog/{post.Slug}#comments");
    }

    private async Task<PostViewModel> BuildPostViewModelAsync(Post post, CommentFormModel form)
    {
        var all = await _comments.GetApprovedForPostAsync(post.Id);

        // one level of threading: replies are grouped under their parent
        var byParent = all.Where(c => c.ParentId != null).ToLookup(c => c.ParentId);
        var comments = new List<CommentViewModel>();
        foreach (var top in all.Where(c => c.ParentId == null))
        {
            var node = new CommentViewModel { Comment = top };
            node.Replies.AddRange(byParent[top.Id].OrderBy(c => c.CreatedOnUtc).Select(r => new CommentViewModel { Comment = r }));
            comments.Add(node);
        }

        return new PostViewModel
        {
            Post = post,
            HtmlContent = _content.ToHtml(post.ContentMd),
            Related = await _posts.GetRelatedAsync(post, 3),
            Comments = comments,
            CommentCount = all.Count,
            CommentForm = form,
            CommentNotice = TempData["CommentNotice"] as string,
            ReplyingTo = form.ParentId is > 0 ? all.FirstOrDefault(c => c.Id == form.ParentId) : null
        };
    }

    private async Task PreparePostMetaAsync(Post post)
    {
        var baseUrl = await _settings.GetBaseUrlAsync();
        var siteTitle = await _settings.GetSiteTitleAsync();
        var url = Ui.Absolute(baseUrl, $"/blog/{post.Slug}");

        ViewData["Title"] = post.MetaTitle ?? post.Title;
        ViewData["MetaDescription"] = post.MetaDescription ?? post.Summary;
        ViewData["CanonicalUrl"] = $"/blog/{post.Slug}";
        ViewData["OgImage"] = Ui.CoverUrl(post);
        ViewData["JsonLd"] = JsonLd.BlogPosting(post, url, siteTitle, await _settings.GetSiteDescriptionAsync());
    }

    // ---------- listings ----------

    [HttpGet("/category/{slug}")]
    public async Task<IActionResult> Category(string slug, int page = 1)
    {
        var category = await _categories.GetBySlugAsync(slug);
        if (category == null)
            return NotFound();

        var pageSize = await _settings.GetPostsPerPageAsync();
        var model = new ListViewModel
        {
            Title = category.MetaTitle ?? category.Name,
            Description = category.MetaDescription ?? category.Description,
            CanonicalUrl = $"/category/{category.Slug}",
            Posts = await _posts.GetByCategoryAsync(category.Id, page, pageSize)
        };
        await PrepareListMetaAsync(model, (category.Name, $"/category/{category.Slug}"));
        return View("List", model);
    }

    [HttpGet("/tag/{slug}")]
    public async Task<IActionResult> Tag(string slug, int page = 1)
    {
        var tag = await _tags.GetBySlugAsync(slug);
        if (tag == null)
            return NotFound();

        var pageSize = await _settings.GetPostsPerPageAsync();
        var model = new ListViewModel
        {
            Title = $"Tag: {tag.Name}",
            Description = $"Posts tagged “{tag.Name}”.",
            CanonicalUrl = $"/tag/{tag.Slug}",
            Posts = await _posts.GetByTagAsync(tag.Id, page, pageSize)
        };
        await PrepareListMetaAsync(model, ($"Tag: {tag.Name}", $"/tag/{tag.Slug}"));
        return View("List", model);
    }

    [HttpGet("/series/{slug}")]
    public async Task<IActionResult> Series(string slug, int page = 1)
    {
        var series = await _series.GetBySlugAsync(slug);
        if (series == null)
            return NotFound();

        var pageSize = await _settings.GetPostsPerPageAsync();
        var model = new ListViewModel
        {
            Title = series.Title,
            Description = series.Description,
            CanonicalUrl = $"/series/{series.Slug}",
            Posts = await _posts.GetBySeriesAsync(series.Id, page, pageSize)
        };
        await PrepareListMetaAsync(model, (series.Title, $"/series/{series.Slug}"));
        return View("List", model);
    }

    [HttpGet("/archive/{year:int}/{month:int}")]
    public async Task<IActionResult> Archive(int year, int month, int page = 1)
    {
        if (month is < 1 or > 12)
            return NotFound();

        var pageSize = await _settings.GetPostsPerPageAsync();
        var model = new ListViewModel
        {
            Title = new DateTime(year, month, 1).ToString("MMMM yyyy"),
            Description = $"Posts published in {new DateTime(year, month, 1).ToString("MMMM yyyy")}.",
            CanonicalUrl = $"/archive/{year}/{month}",
            Posts = await _posts.GetByMonthAsync(year, month, page, pageSize)
        };
        await PrepareListMetaAsync(model, (model.Title, model.CanonicalUrl));
        return View("List", model);
    }

    [HttpGet("/search")]
    public async Task<IActionResult> Search(string q, int page = 1)
    {
        var pageSize = await _settings.GetPostsPerPageAsync();
        var model = new ListViewModel
        {
            Title = $"Search: {q}",
            Description = $"Search results for “{q}”.",
            CanonicalUrl = "/search",
            Posts = await _posts.SearchAsync(q ?? string.Empty, page, pageSize)
        };
        ViewData["Title"] = model.Title;
        ViewData["MetaDescription"] = model.Description;
        ViewData["CanonicalUrl"] = model.CanonicalUrl;
        return View(model);
    }

    [HttpGet("/archive")]
    public async Task<IActionResult> ArchiveIndex()
    {
        ViewBag.Months = await _posts.GetArchiveMonthsAsync();
        var model = new ListViewModel { Title = "Archive", Description = "All posts by month.", CanonicalUrl = "/archive" };
        ViewData["Title"] = model.Title;
        ViewData["MetaDescription"] = model.Description;
        ViewData["CanonicalUrl"] = model.CanonicalUrl;
        return View("ArchiveIndex", model);
    }

    private async Task PrepareListMetaAsync(ListViewModel model, (string Name, string Url) crumb)
    {
        var baseUrl = await _settings.GetBaseUrlAsync();
        var siteTitle = await _settings.GetSiteTitleAsync();
        ViewData["Title"] = model.Title;
        ViewData["MetaDescription"] = model.Description;
        ViewData["CanonicalUrl"] = model.CanonicalUrl;
        ViewData["JsonLd"] = JsonLd.Breadcrumb(siteTitle, baseUrl, crumb);
    }
}
