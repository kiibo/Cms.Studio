using Microsoft.AspNetCore.Mvc;
using Cms.Studio.Core.Domain;
using Cms.Studio.Core.Services;
using Cms.Studio.Web.Models;

namespace Cms.Studio.Web.Controllers;

/// <summary>Daily / weekly / monthly view leaderboards.</summary>
public class RankController : Controller
{
    private readonly PostService _posts;
    private readonly SettingService _settings;

    public RankController(PostService posts, SettingService settings)
    {
        _posts = posts;
        _settings = settings;
    }

    [HttpGet("/rank/{period}")]
    public async Task<IActionResult> Index(string period, int count = 50)
    {
        var rankPeriod = period.ToLowerInvariant() switch
        {
            "daily" => RankPeriod.Daily,
            "weekly" => RankPeriod.Weekly,
            "monthly" => RankPeriod.Monthly,
            _ => (RankPeriod?)null
        };
        if (rankPeriod == null)
            return NotFound();

        var model = new RankViewModel
        {
            Period = rankPeriod.Value,
            Items = await _posts.GetRankingAsync(rankPeriod.Value, Math.Clamp(count, 5, 200))
        };

        var title = rankPeriod.Value switch
        {
            RankPeriod.Daily => "Daily Top",
            RankPeriod.Weekly => "Weekly Top",
            _ => "Monthly Top"
        };

        ViewData["Title"] = title;
        ViewData["MetaDescription"] = $"{title} — the most read posts on {await _settings.GetSiteTitleAsync()}.";
        ViewData["CanonicalUrl"] = $"/rank/{period.ToLowerInvariant()}";
        return View(model);
    }
}
