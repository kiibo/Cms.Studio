using Microsoft.EntityFrameworkCore;
using Cms.Studio.Core.Data;
using Cms.Studio.Core.Domain;

namespace Cms.Studio.Core.Services;

/// <summary>Small stats service for the admin dashboard counters.</summary>
public class StatsService
{
    private readonly CmsDbContext _db;

    public StatsService(CmsDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardStats> GetAsync()
    {
        var totalPosts = await _db.Posts.CountAsync();
        var published = await _db.Posts.CountAsync(p => p.Status == PostStatus.Published);
        var totalViews = await _db.Posts.SumAsync(p => (int?)p.ViewCount) ?? 0;
        var totalCategories = await _db.Categories.CountAsync();
        var totalTags = await _db.Tags.CountAsync();
        var totalSeries = await _db.Series.CountAsync();
        var pendingComments = await _db.Comments.CountAsync(c => !c.IsApproved);
        var totalComments = await _db.Comments.CountAsync();

        return new DashboardStats
        {
            TotalPosts = totalPosts,
            PublishedPosts = published,
            DraftPosts = totalPosts - published,
            TotalViews = totalViews,
            TotalCategories = totalCategories,
            TotalTags = totalTags,
            TotalSeries = totalSeries,
            PendingComments = pendingComments,
            TotalComments = totalComments
        };
    }
}

public class DashboardStats
{
    public int TotalPosts { get; set; }
    public int PublishedPosts { get; set; }
    public int DraftPosts { get; set; }
    public int TotalViews { get; set; }
    public int TotalCategories { get; set; }
    public int TotalTags { get; set; }
    public int TotalSeries { get; set; }
    public int TotalComments { get; set; }
    public int PendingComments { get; set; }
}
