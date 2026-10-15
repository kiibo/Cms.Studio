using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Cms.Studio.Core.Domain;
using Cms.Studio.Core.Services;
using Cms.Studio.Web.Helpers;
using Cms.Studio.Web.Infrastructure;
using Cms.Studio.Web.Models;

namespace Cms.Studio.Web.Controllers.Api;

/// <summary>
/// Machine API for AI agents / automations: create, update and look up articles without
/// going through the admin UI. Authenticate with the API token from the admin settings page:
/// <c>Authorization: Bearer cms_…</c> or <c>X-Api-Key: cms_…</c>.
/// </summary>
[Route("api")]
[EnableRateLimiting("api")]
public class PostsApiController : Controller
{
    private readonly ApiTokenService _tokens;
    private readonly PostService _posts;
    private readonly CategoryService _categories;
    private readonly TagService _tags;
    private readonly SeriesService _series;
    private readonly SettingService _settings;
    private readonly ContentService _content;
    private readonly IWebHostEnvironment _env;

    public PostsApiController(ApiTokenService tokens, PostService posts, CategoryService categories,
        TagService tags, SeriesService series, SettingService settings, ContentService content,
        IWebHostEnvironment env)
    {
        _tokens = tokens;
        _posts = posts;
        _categories = categories;
        _tags = tags;
        _series = series;
        _settings = settings;
        _content = content;
        _env = env;
    }

    // ---------- auth ----------

    private async Task<bool> IsAuthorizedAsync()
    {
        var provided = Request.Headers.Authorization.ToString();
        if (provided.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            provided = provided["Bearer ".Length..];
        else
            provided = Request.Headers["X-Api-Key"].ToString();

        return await _tokens.VerifyAsync(provided);
    }

    private async Task<IActionResult?> CheckAuthAsync() =>
        await IsAuthorizedAsync()
            ? null
            : Unauthorized(new { error = "Missing or invalid API token. Use 'Authorization: Bearer <token>'." });

    // ---------- catalog ----------

    /// <summary>GET /api/catalog — categories, tags and series so agents can pick valid ids/names.</summary>
    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog()
    {
        if (await CheckAuthAsync() is { } denied) return denied;

        return Ok(new
        {
            categories = (await _categories.GetAllAsync()).Select(c => new { c.Id, c.Name, c.Slug }),
            tags = (await _tags.GetAllAsync()).Select(t => new { t.Id, t.Name, t.Slug }),
            series = (await _series.GetAllAsync()).Select(s => new { s.Id, s.Title, s.Slug })
        });
    }

    // ---------- read ----------

    /// <summary>GET /api/posts?keyword=&amp;status=draft|published&amp;page=&amp;pageSize=</summary>
    [HttpGet("posts")]
    public async Task<IActionResult> List(string? keyword, string? status, int page = 1, int pageSize = 10)
    {
        if (await CheckAuthAsync() is { } denied) return denied;

        pageSize = Math.Clamp(pageSize, 1, 50);
        PostStatus? filter = status?.Trim().ToLowerInvariant() switch
        {
            "draft" => PostStatus.Draft,
            "published" => PostStatus.Published,
            _ => null
        };

        var result = await _posts.AdminSearchAsync(keyword, filter, page, pageSize);
        var baseUrl = await _settings.GetBaseUrlAsync();
        return Ok(new
        {
            result.TotalCount,
            result.Page,
            result.PageSize,
            Items = result.Items.Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                Status = p.Status.ToString().ToLowerInvariant(),
                Url = Ui.Absolute(baseUrl, $"/blog/{p.Slug}")
            })
        });
    }

    /// <summary>GET /api/posts/{idOrSlug} — the full article including its Markdown body.</summary>
    [HttpGet("posts/{idOrSlug}")]
    public async Task<IActionResult> Get(string idOrSlug)
    {
        if (await CheckAuthAsync() is { } denied) return denied;

        var post = int.TryParse(idOrSlug, out var id)
            ? await _posts.GetByIdAdminAsync(id)
            : await _posts.GetBySlugAdminAsync(idOrSlug);
        if (post == null)
            return NotFound(new { error = $"Post '{idOrSlug}' was not found." });

        return Ok(await ShapeAsync(post, includeBody: true));
    }

    // ---------- create ----------

    /// <summary>POST /api/posts — create an article. Status defaults to "published".</summary>
    [HttpPost("posts")]
    public async Task<IActionResult> Create([FromBody] ApiPostInput? input)
    {
        if (await CheckAuthAsync() is { } denied) return denied;
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid JSON body.", details = ModelErrors() });
        if (input == null)
            return BadRequest(new { error = "A JSON body is required." });

        if (string.IsNullOrWhiteSpace(input.Title))
            return BadRequest(new { error = "title is required." });
        if (input.Title.Trim().Length > 200)
            return BadRequest(new { error = "title must be 200 characters or fewer." });
        if (string.IsNullOrWhiteSpace(input.ContentMd))
            return BadRequest(new { error = "contentMd is required (Markdown body)." });
        if (input.ContentMd.Length > 200_000)
            return BadRequest(new { error = "contentMd must be 200,000 characters or fewer." });

        var status = ParseStatus(input.Status, defaultStatus: PostStatus.Published, out var statusError);
        if (statusError != null)
            return BadRequest(new { error = statusError });

        var (categoryId, categoryError) = await ResolveCategoryAsync(input);
        if (categoryError != null)
            return BadRequest(new { error = categoryError });

        var (seriesIds, seriesError) = await ResolveSeriesAsync(input.SeriesIds);
        if (seriesError != null)
            return BadRequest(new { error = seriesError });

        var post = new Post
        {
            Title = input.Title.Trim(),
            ContentMd = input.ContentMd,
            Summary = ResolveSummary(input),
            CoverImageUrl = Truncate(input.CoverImageUrl, 500),
            AuthorName = string.IsNullOrWhiteSpace(input.AuthorName)
                ? await _settings.GetAuthorNameAsync()
                : input.AuthorName.Trim(),
            CategoryId = categoryId,
            Status = status!.Value,
            IsFixedTop = input.IsFixedTop ?? false,
            MetaTitle = Truncate(input.MetaTitle, 200),
            MetaDescription = Truncate(input.MetaDescription, 500),
            PublishedOnUtc = status == PostStatus.Published ? input.PublishedOnUtc ?? DateTime.UtcNow : input.PublishedOnUtc
        };

        if (!string.IsNullOrWhiteSpace(input.Slug))
            post.Slug = input.Slug.Trim();

        await _posts.SaveAsync(post, string.Join(",", input.Tags ?? new List<string>()), seriesIds);

        return StatusCode(StatusCodes.Status201Created, await ShapeAsync(post, includeBody: false));
    }

    // ---------- update ----------

    /// <summary>PUT /api/posts/{id} — partial update; omitted fields keep their current values.</summary>
    [HttpPut("posts/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ApiPostInput? input)
    {
        if (await CheckAuthAsync() is { } denied) return denied;
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid JSON body.", details = ModelErrors() });
        if (input == null)
            return BadRequest(new { error = "A JSON body is required." });

        var post = await _posts.GetByIdAdminAsync(id);
        if (post == null)
            return NotFound(new { error = $"Post {id} was not found." });

        if (input.Title != null && input.Title.Trim().Length is 0 or > 200)
            return BadRequest(new { error = "title must be 1–200 characters." });
        if (input.ContentMd is { Length: > 200_000 })
            return BadRequest(new { error = "contentMd must be 200,000 characters or fewer." });

        var status = post.Status;
        if (input.Status != null)
        {
            var parsed = ParseStatus(input.Status, post.Status, out var statusError);
            if (statusError != null)
                return BadRequest(new { error = statusError });
            status = parsed ?? post.Status;
        }

        int categoryId;
        if (input.CategoryId == null && input.CategoryName == null)
            categoryId = post.CategoryId;
        else
        {
            var (resolvedId, categoryError) = await ResolveCategoryAsync(input);
            if (categoryError != null)
                return BadRequest(new { error = categoryError });
            categoryId = resolvedId;
        }

        List<int> seriesIds;
        if (input.SeriesIds == null)
            seriesIds = post.PostSeries.Select(ps => ps.SeriesId).ToList();
        else
        {
            var (resolvedIds, seriesError) = await ResolveSeriesAsync(input.SeriesIds);
            if (seriesError != null)
                return BadRequest(new { error = seriesError });
            seriesIds = resolvedIds;
        }

        post.Title = input.Title?.Trim() ?? post.Title;
        post.ContentMd = input.ContentMd ?? post.ContentMd;
        post.Summary = input.Summary != null ? Truncate(input.Summary, 1000)! : post.Summary;
        post.Slug = input.Slug?.Trim() ?? post.Slug;
        post.CoverImageUrl = input.CoverImageUrl != null ? Truncate(input.CoverImageUrl, 500) : post.CoverImageUrl;
        post.AuthorName = string.IsNullOrWhiteSpace(input.AuthorName) ? post.AuthorName : input.AuthorName.Trim();
        post.CategoryId = categoryId;
        post.Status = status;
        post.IsFixedTop = input.IsFixedTop ?? post.IsFixedTop;
        post.MetaTitle = input.MetaTitle != null ? Truncate(input.MetaTitle, 200) : post.MetaTitle;
        post.MetaDescription = input.MetaDescription != null ? Truncate(input.MetaDescription, 500) : post.MetaDescription;
        post.PublishedOnUtc = input.PublishedOnUtc ?? post.PublishedOnUtc;
        if (post.Status == PostStatus.Published && post.PublishedOnUtc == null)
            post.PublishedOnUtc = DateTime.UtcNow;

        var tags = input.Tags != null
            ? string.Join(",", input.Tags)
            : string.Join(",", post.PostTags.Select(pt => pt.Tag.Name));

        await _posts.SaveAsync(post, tags, seriesIds);
        return Ok(await ShapeAsync(post, includeBody: false));
    }

    // ---------- images ----------

    /// <summary>
    /// POST /api/images (multipart, field "file") — upload an image for article bodies.
    /// Returns the public URL plus a ready-to-paste Markdown snippet:
    /// <c>![alt](/uploads/2026/10/…jpg)</c>.
    /// </summary>
    [HttpPost("images")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile? file)
    {
        if (await CheckAuthAsync() is { } denied) return denied;
        if (file == null)
            return BadRequest(new { error = "Send the image as multipart/form-data in the 'file' field." });

        await using var stream = file.OpenReadStream();
        var (ok, url, error) = await UploadedImageStore.SaveAsync(_env, file.FileName, stream, file.Length);
        if (!ok)
            return BadRequest(new { error });

        return StatusCode(StatusCodes.Status201Created, await ShapeImageAsync(url!, file.FileName));
    }

    /// <summary>
    /// POST /api/images/base64 (JSON: {"fileName":"cover.png","dataBase64":"…"}) — for agents
    /// that hold image bytes (generated charts, screenshots) instead of a file.
    /// </summary>
    [HttpPost("images/base64")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> UploadImageBase64([FromBody] ApiImageInput? input)
    {
        if (await CheckAuthAsync() is { } denied) return denied;
        if (!ModelState.IsValid || input == null || string.IsNullOrWhiteSpace(input.DataBase64))
            return BadRequest(new { error = "JSON body with fileName and dataBase64 is required." });

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(input.DataBase64.Trim());
        }
        catch (FormatException)
        {
            return BadRequest(new { error = "dataBase64 is not valid base64." });
        }

        await using var stream = new MemoryStream(bytes);
        var (ok, url, error) = await UploadedImageStore.SaveAsync(_env, input.FileName, stream, bytes.LongLength);
        if (!ok)
            return BadRequest(new { error });

        return StatusCode(StatusCodes.Status201Created, await ShapeImageAsync(url!, input.FileName));
    }

    private async Task<object> ShapeImageAsync(string url, string? fileName) => new
    {
        Url = url,
        AbsoluteUrl = Ui.Absolute(await _settings.GetBaseUrlAsync(), url),
        Markdown = $"![{Path.GetFileNameWithoutExtension(fileName ?? "image")}]({url})",
        Html = $"<img src=\"{url}\" alt=\"\" loading=\"lazy\" decoding=\"async\" />"
    };

    // ---------- helpers ----------

    private static PostStatus? ParseStatus(string? raw, PostStatus defaultStatus, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
            return defaultStatus;

        switch (raw.Trim().ToLowerInvariant())
        {
            case "published":
                return PostStatus.Published;
            case "draft":
                return PostStatus.Draft;
            default:
                error = "status must be 'published' or 'draft'.";
                return null;
        }
    }

    private async Task<(int CategoryId, string? Error)> ResolveCategoryAsync(ApiPostInput input)
    {
        if (input.CategoryId.HasValue)
        {
            var category = await _categories.GetByIdAsync(input.CategoryId.Value);
            if (category == null)
                return (0, $"categoryId {input.CategoryId} does not exist. See GET /api/catalog.");
            return (category.Id, null);
        }

        if (!string.IsNullOrWhiteSpace(input.CategoryName))
        {
            var slug = _content.Slugify(input.CategoryName);
            var category = await _categories.GetBySlugAsync(slug);
            if (category == null)
            {
                category = new Category { Name = input.CategoryName.Trim(), Slug = slug, ShowOnMenu = false };
                await _categories.SaveAsync(category);
            }
            return (category.Id, null);
        }

        return (0, "categoryId or categoryName is required (see GET /api/catalog).");
    }

    private async Task<(List<int> SeriesIds, string? Error)> ResolveSeriesAsync(List<int>? ids)
    {
        if (ids == null || ids.Count == 0)
            return (new List<int>(), null);

        var existing = (await _series.GetAllAsync()).Select(s => s.Id).ToHashSet();
        var missing = ids.Distinct().Where(i => !existing.Contains(i)).ToList();
        if (missing.Count > 0)
            return (new List<int>(), $"seriesIds not found: {string.Join(", ", missing)}. See GET /api/catalog.");
        return (ids.Distinct().ToList(), null);
    }

    private static string ResolveSummary(ApiPostInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.Summary))
            return Truncate(input.Summary, 1000)!;

        // Derived summary: first ~160 chars of the body with markup stripped loosely.
        var text = System.Text.RegularExpressions.Regex.Replace(input.ContentMd ?? string.Empty, @"[#*`>_\[\]()!-]", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= 160 ? text : text[..160].TrimEnd() + "…";
    }

    private async Task<object> ShapeAsync(Post post, bool includeBody)
    {
        var category = await _categories.GetByIdAsync(post.CategoryId);
        return new
        {
            post.Id,
            post.Title,
            post.Slug,
            Status = post.Status.ToString().ToLowerInvariant(),
            Url = Ui.Absolute(await _settings.GetBaseUrlAsync(), $"/blog/{post.Slug}"),
            post.Summary,
            ContentMd = includeBody ? post.ContentMd : null,
            post.CoverImageUrl,
            post.AuthorName,
            Category = category == null ? null : new { category.Id, category.Name, category.Slug },
            Tags = post.PostTags?.Select(pt => pt.Tag.Name).ToList() ?? new List<string>(),
            post.IsFixedTop,
            post.MetaTitle,
            post.MetaDescription,
            post.PublishedOnUtc,
            post.CreatedOnUtc,
            post.ModifiedOnUtc
        };
    }

    private Dictionary<string, string[]> ModelErrors() =>
        ModelState.ToDictionary(kv => kv.Key, kv => kv.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>());

    private static string? Truncate(string? value, int max) =>
        value == null ? null : value.Length <= max ? value : value[..max];
}
