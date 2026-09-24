using Microsoft.AspNetCore.Mvc;
using Cms.Studio.Core.Domain;
using Cms.Studio.Core.Services;
using Cms.Studio.Web.Models;

namespace Cms.Studio.Web.ViewComponents;

/// <summary>Renders the desktop sidebar: rankings (daily/weekly/monthly), categories, tag cloud, series.</summary>
public class SidebarViewComponent : ViewComponent
{
    private readonly PostService _posts;
    private readonly CategoryService _categories;
    private readonly TagService _tags;
    private readonly SeriesService _series;

    public SidebarViewComponent(PostService posts, CategoryService categories, TagService tags, SeriesService series)
    {
        _posts = posts;
        _categories = categories;
        _tags = tags;
        _series = series;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = new SidebarViewModel
        {
            Daily = await _posts.GetRankingAsync(RankPeriod.Daily, 5),
            Weekly = await _posts.GetRankingAsync(RankPeriod.Weekly, 5),
            Monthly = await _posts.GetRankingAsync(RankPeriod.Monthly, 5),
            Categories = await _categories.GetAllAsync(showHidden: false),
            TopTags = await _tags.GetTopAsync(20),
            Series = await _series.GetAllAsync()
        };
        return View(model);
    }
}
