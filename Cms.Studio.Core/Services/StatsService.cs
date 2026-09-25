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

    // ---------- visitor analytics (VisitRecord table) ----------

    /// <summary>
    /// Per-day unique visitors for the dashboard curve.
    /// Rule: the same IP visiting several times on the same day counts once.
    /// </summary>
    public async Task<List<DailyVisits>> GetDailyUniqueVisitorsAsync(int days)
    {
        var today = DateTime.UtcNow.Date;
        var from = today.AddDays(-(days - 1));

        var rows = await _db.VisitRecords.AsNoTracking()
            .Where(v => v.VisitedOnUtc >= from)
            .GroupBy(v => v.VisitedOnUtc.Date)
            .Select(g => new { Day = g.Key, Visitors = g.Select(x => x.Ip).Distinct().Count() })
            .ToListAsync();

        var byDay = rows.ToDictionary(r => r.Day, r => r.Visitors);
        var result = new List<DailyVisits>(days);
        for (var day = from; day <= today; day = day.AddDays(1))
            result.Add(new DailyVisits { Day = day, Visitors = byDay.TryGetValue(day, out var v) ? v : 0 });
        return result;
    }

    /// <summary>Aggregate visitor stats for a window: PV, UV, device/browser/platform split, top IPs.</summary>
    public async Task<VisitorStats> GetVisitorStatsAsync(int days)
    {
        var today = DateTime.UtcNow.Date;
        var from = today.AddDays(-(days - 1));
        var q = _db.VisitRecords.AsNoTracking().Where(v => v.VisitedOnUtc >= from);

        return new VisitorStats
        {
            Pv = await q.CountAsync(),
            Uv = await q.Select(v => v.Ip).Distinct().CountAsync(),
            Devices = await SliceAsync(q, v => v.Device ?? "Unknown"),
            Browsers = await SliceAsync(q, v => v.Browser ?? "Unknown"),
            Platforms = await SliceAsync(q, v => v.Platform ?? "Unknown"),
            TopIps = await q.GroupBy(v => v.Ip)
                .Select(g => new IpStat { Ip = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.Ip)
                .Take(10)
                .ToListAsync()
        };
    }

    private static async Task<List<StatSlice>> SliceAsync(IQueryable<VisitRecord> query, System.Linq.Expressions.Expression<Func<VisitRecord, string>> key)
    {
        return await query.GroupBy(key)
            .Select(g => new StatSlice { Key = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Key)
            .Take(10)
            .ToListAsync();
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

/// <summary>Unique visitors of one day (same IP + same day counts once).</summary>
public class DailyVisits
{
    public DateTime Day { get; set; }
    public int Visitors { get; set; }
}

public class StatSlice
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class IpStat
{
    public string Ip { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class VisitorStats
{
    /// <summary>Total page requests in the window.</summary>
    public int Pv { get; set; }
    /// <summary>Distinct IPs in the window.</summary>
    public int Uv { get; set; }
    public List<StatSlice> Devices { get; set; } = new();
    public List<StatSlice> Browsers { get; set; } = new();
    public List<StatSlice> Platforms { get; set; } = new();
    public List<IpStat> TopIps { get; set; } = new();
}
