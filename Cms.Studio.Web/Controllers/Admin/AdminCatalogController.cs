using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cms.Studio.Core.Domain;
using Cms.Studio.Core.Services;
using Cms.Studio.Web.Infrastructure;

namespace Cms.Studio.Web.Controllers.Admin;

[Authorize]
[Route("admin/catalog")]
public class AdminCatalogController : Controller
{
    private readonly CategoryService _categories;
    private readonly TagService _tags;
    private readonly SeriesService _series;

    public AdminCatalogController(CategoryService categories, TagService tags, SeriesService series)
    {
        _categories = categories;
        _tags = tags;
        _series = series;
    }

    // ---------- categories ----------

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(int? editId)
    {
        var categories = await _categories.GetAllAsync();
        ViewBag.Categories = categories;
        ViewBag.Edit = editId != null ? categories.FirstOrDefault(c => c.Id == editId.Value) : null;
        return View("~/Views/Admin/Categories.cshtml");
    }

    [HttpPost("categories/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(Category category)
    {
        if (!string.IsNullOrWhiteSpace(category.Name))
            await _categories.SaveAsync(category);
        return RedirectToAction("Categories");
    }

    [HttpPost("categories/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        await _categories.DeleteAsync(id);
        return RedirectToAction("Categories");
    }

    // ---------- tags ----------

    [HttpGet("tags")]
    public async Task<IActionResult> Tags()
    {
        ViewBag.Tags = await _tags.GetAllAsync();
        return View("~/Views/Admin/Tags.cshtml");
    }

    [HttpPost("tags/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTag(int id)
    {
        await _tags.DeleteAsync(id);
        return RedirectToAction("Tags");
    }

    // ---------- series ----------

    [HttpGet("series")]
    public async Task<IActionResult> SeriesList(int? editId)
    {
        var series = await _series.GetAllAsync();
        ViewBag.Series = series;
        ViewBag.Edit = editId != null ? series.FirstOrDefault(s => s.Id == editId.Value) : null;
        return View("~/Views/Admin/Series.cshtml");
    }

    [HttpPost("series/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSeries(Series series)
    {
        if (!string.IsNullOrWhiteSpace(series.Title))
            await _series.SaveAsync(series);
        return RedirectToAction("SeriesList");
    }

    [HttpPost("series/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSeries(int id)
    {
        await _series.DeleteAsync(id);
        return RedirectToAction("SeriesList");
    }
}

[Authorize]
[Route("admin/settings")]
public class AdminSettingsController : Controller
{
    private readonly SettingService _settings;
    private readonly AdminAccountService _accounts;
    private readonly ApiTokenService _apiTokens;

    public AdminSettingsController(SettingService settings, AdminAccountService accounts, ApiTokenService apiTokens)
    {
        _settings = settings;
        _accounts = accounts;
        _apiTokens = apiTokens;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.SiteTitle = await _settings.GetSiteTitleAsync();
        ViewBag.SiteDescription = await _settings.GetSiteDescriptionAsync();
        ViewBag.SiteBaseUrl = await _settings.GetBaseUrlAsync();
        ViewBag.PostsPerPage = await _settings.GetAsync(SettingService.PostsPerPageKey, "10");
        ViewBag.AuthorName = await _settings.GetAuthorNameAsync();
        ViewBag.ArticleStyle = await _settings.GetArticleStyleAsync();
        ViewBag.ArticleTemplate = await _settings.GetArticleTemplateAsync();
        ViewBag.BrandText = await _settings.GetSiteBrandTextAsync();
        ViewBag.SiteLogo = await _settings.GetSiteLogoAsync();
        ViewBag.ApiToken = await _apiTokens.GetOrCreateAsync();
        ViewBag.UsingDefaultPassword = await _accounts.IsUsingDefaultPasswordAsync();
        return View("~/Views/Admin/Settings.cshtml");
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string siteTitle, string siteDescription, string siteBaseUrl,
        string postsPerPage, string authorName, string? articleStyle, string? articleTemplate)
    {
        await _settings.SetAsync(SettingService.SiteTitleKey, siteTitle ?? string.Empty);
        await _settings.SetAsync(SettingService.SiteDescriptionKey, siteDescription ?? string.Empty);
        await _settings.SetAsync(SettingService.SiteBaseUrlKey, (siteBaseUrl ?? string.Empty).Trim().TrimEnd('/'));
        await _settings.SetAsync(SettingService.PostsPerPageKey, postsPerPage ?? "10");
        await _settings.SetAsync(SettingService.AuthorNameKey, authorName ?? string.Empty);
        await _settings.SetAsync(SettingService.ArticleStyleKey,
            articleStyle is "comfy" or "paper" or "large" ? articleStyle : "default");
        await _settings.SetAsync(SettingService.ArticleTemplateKey,
            articleTemplate == "huashu" ? "huashu" : "classic");
        TempData["Saved"] = true;
        return RedirectToAction("Index");
    }

    /// <summary>Security settings: admin password change.</summary>
    [HttpPost("security")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Security(string? currentPassword, string? newPassword, string? confirmPassword)
    {
        if (!string.IsNullOrEmpty(newPassword) || !string.IsNullOrEmpty(confirmPassword))
        {
            if (!await _accounts.VerifyPasswordAsync(currentPassword ?? string.Empty))
                TempData["PasswordError"] = "The current password is not correct.";
            else if ((newPassword ?? string.Empty).Length < 10)
                TempData["PasswordError"] = "The new password must be at least 10 characters.";
            else if (newPassword != confirmPassword)
                TempData["PasswordError"] = "The two new passwords do not match.";
            else
            {
                await _accounts.ChangePasswordAsync(newPassword!);
                TempData["PasswordSaved"] = true;
            }
        }

        return RedirectToAction("Index");
    }

    /// <summary>Machine API token: rotate it when leaked or when an integration is retired.</summary>
    [HttpPost("api-token")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiToken(string? regenerate)
    {
        if (string.Equals(regenerate, "true", StringComparison.OrdinalIgnoreCase))
        {
            await _apiTokens.RegenerateAsync();
            TempData["TokenSaved"] = true;
        }
        return RedirectToAction("Index");
    }

    /// <summary>Brand settings: header logo image + brand text.</summary>
    [HttpPost("brand")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Brand(string? brandText, IFormFile? logo, string? removeLogo,
        [FromServices] IWebHostEnvironment env)
    {
        // Brand text — blank means "back to the classic CMS.STUDIO logo text".
        var text = (brandText ?? string.Empty).Trim();
        if (text.Length == 0)
            text = "Cms.Studio";
        if (text.Length > 50)
        {
            TempData["BrandError"] = "The brand text must be 50 characters or fewer.";
            return RedirectToAction("Index");
        }
        await _settings.SetAsync(SettingService.SiteBrandTextKey, text);

        // Logo removal
        if (string.Equals(removeLogo, "true", StringComparison.OrdinalIgnoreCase))
        {
            await DeleteLogoFileAsync(await _settings.GetSiteLogoAsync(), env);
            await _settings.SetAsync(SettingService.SiteLogoKey, string.Empty);
            TempData["BrandSaved"] = true;
            return RedirectToAction("Index");
        }

        // Logo upload (same validation pipeline as post images)
        if (logo is { Length: > 0 })
        {
            await using var read = logo.OpenReadStream();
            var (ok, savedUrl, error) = await UploadedImageStore.SaveAsync(env, logo.FileName, read, logo.Length);
            if (!ok)
            {
                TempData["BrandError"] = error;
                return RedirectToAction("Index");
            }

            await DeleteLogoFileAsync(await _settings.GetSiteLogoAsync(), env);
            await _settings.SetAsync(SettingService.SiteLogoKey, savedUrl!);
        }

        TempData["BrandSaved"] = true;
        return RedirectToAction("Index");
    }

    /// <summary>Removes a replaced/removed logo file from wwwroot/uploads (never touches anything else).</summary>
    private static Task DeleteLogoFileAsync(string? logoUrl, IWebHostEnvironment env)
    {
        if (!string.IsNullOrWhiteSpace(logoUrl) && logoUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var absolute = Path.Combine(env.WebRootPath ?? string.Empty,
                    logoUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                var root = Path.GetFullPath(Path.Combine(env.WebRootPath ?? string.Empty, "uploads"));
                if (Path.GetFullPath(absolute).StartsWith(root, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(absolute))
                    System.IO.File.Delete(absolute);
            }
            catch
            {
                // A leftover file is harmless; never fail the settings save over it.
            }
        }
        return Task.CompletedTask;
    }
}
