using Microsoft.AspNetCore.Mvc;
using Cms.Studio.Core.Services;
using Cms.Studio.Web.Helpers;
using Cms.Studio.Web.Models;

namespace Cms.Studio.Web.Controllers;

public class HomeController : Controller
{
    private readonly PostService _posts;
    private readonly SettingService _settings;

    public HomeController(PostService posts, SettingService settings)
    {
        _posts = posts;
        _settings = settings;
    }

    [HttpGet("/")]
    public async Task<IActionResult> Index(int page = 1)
    {
        var pageSize = await _settings.GetPostsPerPageAsync();
        var feed = await _posts.GetFeedAsync(page, pageSize);

        var model = new ListViewModel
        {
            Title = await _settings.GetSiteTitleAsync(),
            Description = await _settings.GetSiteDescriptionAsync(),
            CanonicalUrl = page > 1 ? $"/?page={page}" : "/",
            Posts = feed
        };

        ViewData["Title"] = model.Title;
        ViewData["MetaDescription"] = model.Description;
        ViewData["JsonLd"] = JsonLd.WebSite(
            Ui.Absolute(await _settings.GetBaseUrlAsync(), "/"),
            model.Title,
            model.Description ?? string.Empty);

        return View(model);
    }

    [HttpGet("/error")]
    public IActionResult Error() => View();
}
