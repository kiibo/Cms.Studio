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
    private readonly AdminPathProvider _adminPath;

    public AdminSettingsController(SettingService settings, AdminAccountService accounts, AdminPathProvider adminPath)
    {
        _settings = settings;
        _accounts = accounts;
        _adminPath = adminPath;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.SiteTitle = await _settings.GetSiteTitleAsync();
        ViewBag.SiteDescription = await _settings.GetSiteDescriptionAsync();
        ViewBag.SiteBaseUrl = await _settings.GetBaseUrlAsync();
        ViewBag.PostsPerPage = await _settings.GetAsync(SettingService.PostsPerPageKey, "10");
        ViewBag.AuthorName = await _settings.GetAuthorNameAsync();
        ViewBag.AdminUrl = _adminPath.UrlFor("/login");
        ViewBag.UsingDefaultPassword = await _accounts.IsUsingDefaultPasswordAsync();
        return View("~/Views/Admin/Settings.cshtml");
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string siteTitle, string siteDescription, string siteBaseUrl,
        string postsPerPage, string authorName)
    {
        await _settings.SetAsync(SettingService.SiteTitleKey, siteTitle ?? string.Empty);
        await _settings.SetAsync(SettingService.SiteDescriptionKey, siteDescription ?? string.Empty);
        await _settings.SetAsync(SettingService.SiteBaseUrlKey, (siteBaseUrl ?? string.Empty).Trim().TrimEnd('/'));
        await _settings.SetAsync(SettingService.PostsPerPageKey, postsPerPage ?? "10");
        await _settings.SetAsync(SettingService.AuthorNameKey, authorName ?? string.Empty);
        TempData["Saved"] = true;
        return RedirectToAction("Index");
    }

    /// <summary>Security settings: hidden admin path + admin password change.</summary>
    [HttpPost("security")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Security(string? adminPath, string? currentPassword,
        string? newPassword, string? confirmPassword)
    {
        if (!string.IsNullOrWhiteSpace(adminPath))
        {
            if (_adminPath.TryChange(adminPath, out var error))
            {
                TempData["PathSaved"] = _adminPath.UrlFor("/login");
                return RedirectToAction("Index");
            }
            TempData["PathError"] = error;
            return RedirectToAction("Index");
        }

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
}
