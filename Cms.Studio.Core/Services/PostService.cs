using Microsoft.EntityFrameworkCore;
using Cms.Studio.Core.Data;
using Cms.Studio.Core.Domain;

namespace Cms.Studio.Core.Services;

public class PostService
{
    private readonly CmsDbContext _db;
    private readonly ContentService _content;

    public PostService(CmsDbContext db, ContentService content)
    {
        _db = db;
        _content = content;
    }

    private IQueryable<Post> Published() =>
        _db.Posts.Where(p => p.Status == PostStatus.Published);

    private static IOrderedQueryable<Post> FeedOrder(IQueryable<Post> query) =>
        query.OrderByDescending(p => p.IsFixedTop).ThenByDescending(p => p.PublishedOnUtc);

    // ---------- public reads ----------

    public async Task<PagedResult<Post>> GetFeedAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        var query = FeedOrder(Published().AsNoTracking());
        var total = await query.CountAsync();
        var items = await query
            .Include(p => p.Category)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return new PagedResult<Post>(items, page, pageSize, total);
    }

    public Task<Post?> GetBySlugAsync(string slug) =>
        Published().AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.PostSeries).ThenInclude(ps => ps.Series)
            .FirstOrDefaultAsync(p => p.Slug == slug);

    public Task<Post?> GetBySlugAdminAsync(string slug) =>
        _db.Posts.Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.PostSeries)
            .FirstOrDefaultAsync(p => p.Slug == slug);

    /// <summary>301 target for a retired slug, or null.</summary>
    public async Task<string?> GetRedirectSlugAsync(string oldSlug)
    {
        var history = await _db.SlugHistory.AsNoTracking().FirstOrDefaultAsync(h => h.OldSlug == oldSlug);
        if (history == null)
            return null;
        var post = await _db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == history.PostId);
        return post?.Slug;
    }

    public async Task<PagedResult<Post>> GetByCategoryAsync(int categoryId, int page, int pageSize) =>
        await PageAsync(Published().Where(p => p.CategoryId == categoryId), page, pageSize);

    public async Task<PagedResult<Post>> GetByTagAsync(int tagId, int page, int pageSize) =>
        await PageAsync(Published().Where(p => p.PostTags.Any(pt => pt.TagId == tagId)), page, pageSize);

    public async Task<PagedResult<Post>> GetBySeriesAsync(int seriesId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        var query = Published().Where(p => p.PostSeries.Any(ps => ps.SeriesId == seriesId));
        var total = await query.CountAsync();
        var items = await query
            .Include(p => p.Category)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.PostSeries).ThenInclude(ps => ps.Series)
            .OrderBy(p => p.PostSeries.Where(ps => ps.SeriesId == seriesId).Select(ps => (int?)ps.DisplayOrder).Min())
            .ThenByDescending(p => p.PublishedOnUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return new PagedResult<Post>(items, page, pageSize, total);
    }

    public async Task<PagedResult<Post>> GetByMonthAsync(int year, int month, int page, int pageSize)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);
        return await PageAsync(Published().Where(p => p.PublishedOnUtc >= from && p.PublishedOnUtc < to), page, pageSize);
    }

    public async Task<PagedResult<Post>> SearchAsync(string query, int page, int pageSize)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new PagedResult<Post>(new List<Post>(), page, pageSize, 0);

        var q = query.Trim();
        return await PageAsync(Published().Where(p =>
            EF.Functions.Like(p.Title, $"%{q}%") ||
            EF.Functions.Like(p.Summary, $"%{q}%") ||
            EF.Functions.Like(p.ContentMd, $"%{q}%")), page, pageSize);
    }

    private async Task<PagedResult<Post>> PageAsync(IQueryable<Post> query, int page, int pageSize)
    {
        page = Math.Max(1, page);
        var total = await query.CountAsync();
        var items = await FeedOrder(query.AsNoTracking())
            .Include(p => p.Category)
            .Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return new PagedResult<Post>(items, page, pageSize, total);
    }

    /// <summary>Related posts: same category first, then shared tags, newest first.</summary>
    public Task<List<Post>> GetRelatedAsync(Post post, int count) =>
        Published().AsNoTracking()
            .Where(p => p.Id != post.Id &&
                        (p.CategoryId == post.CategoryId ||
                         p.PostTags.Any(pt => post.PostTags.Select(t => t.TagId).Contains(pt.TagId))))
            .OrderByDescending(p => p.PublishedOnUtc)
            .Include(p => p.Category)
            .Take(count)
            .ToListAsync();

    public Task<List<ArchiveMonth>> GetArchiveMonthsAsync() =>
        Published().AsNoTracking()
            .GroupBy(p => new { p.PublishedOnUtc!.Value.Year, p.PublishedOnUtc!.Value.Month })
            .Select(g => new ArchiveMonth { Year = g.Key.Year, Month = g.Key.Month, Count = g.Count() })
            .OrderByDescending(m => m.Year).ThenByDescending(m => m.Month)
            .ToListAsync();

    // ---------- view tracking & rankings ----------

    public async Task RegisterViewAsync(int postId)
    {
        var today = DateTime.UtcNow.Date;
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == postId);
        if (post == null)
            return;

        post.ViewCount += 1;

        var row = await _db.PostViewDaily.FindAsync(postId, today);
        if (row == null)
            _db.PostViewDaily.Add(new PostViewDaily { PostId = postId, Date = today, Views = 1 });
        else
            row.Views += 1;

        await _db.SaveChangesAsync();
    }

    /// <summary>Daily = today; Weekly = last 7 days; Monthly = last 30 days.</summary>
    public async Task<List<RankItem>> GetRankingAsync(RankPeriod period, int count)
    {
        var today = DateTime.UtcNow.Date;
        var from = period switch
        {
            RankPeriod.Daily => today,
            RankPeriod.Weekly => today.AddDays(-6),
            RankPeriod.Monthly => today.AddDays(-29),
            _ => today
        };

        var top = await _db.PostViewDaily.AsNoTracking()
            .Where(v => v.Date >= from && v.Date <= today)
            .GroupBy(v => v.PostId)
            .Select(g => new { PostId = g.Key, Views = g.Sum(x => x.Views) })
            .OrderByDescending(x => x.Views)
            .ThenByDescending(x => x.PostId)
            .Take(count)
            .ToListAsync();

        if (top.Count == 0)
            return new List<RankItem>();

        var ids = top.Select(t => t.PostId).ToList();
        var posts = await Published().AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Include(p => p.Category)
            .ToDictionaryAsync(p => p.Id);

        return top
            .Where(t => posts.ContainsKey(t.PostId))
            .Select(t => new RankItem { Post = posts[t.PostId], Views = t.Views })
            .ToList();
    }

    /// <summary>Per-day total views for the dashboard trend.</summary>
    public async Task<List<DailyViews>> GetViewsTrendAsync(int days)
    {
        var today = DateTime.UtcNow.Date;
        var from = today.AddDays(-(days - 1));

        var rows = await _db.PostViewDaily.AsNoTracking()
            .Where(v => v.Date >= from)
            .GroupBy(v => v.Date)
            .Select(g => new { Day = g.Key, Views = g.Sum(x => x.Views) })
            .ToListAsync();

        var byDay = rows.ToDictionary(r => r.Day, r => r.Views);
        var result = new List<DailyViews>(days);
        for (var day = from; day <= today; day = day.AddDays(1))
            result.Add(new DailyViews { Day = day, Views = byDay.TryGetValue(day, out var v) ? v : 0 });
        return result;
    }

    // ---------- admin ----------

    public async Task<PagedResult<Post>> AdminSearchAsync(string? keyword, PostStatus? status, int page, int pageSize)
    {
        page = Math.Max(1, page);
        var query = _db.Posts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var q = keyword.Trim();
            query = query.Where(p => EF.Functions.Like(p.Title, $"%{q}%"));
        }
        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var total = await query.CountAsync();
        var items = await query
            .Include(p => p.Category)
            .OrderByDescending(p => p.CreatedOnUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();
        return new PagedResult<Post>(items, page, pageSize, total);
    }

    public Task<Post?> GetByIdAdminAsync(int id) =>
        _db.Posts.Include(p => p.PostTags).ThenInclude(pt => pt.Tag)
            .Include(p => p.PostSeries)
            .FirstOrDefaultAsync(p => p.Id == id);

    /// <summary>Create or update a post, syncing tags/series and recording slug history for 301s.</summary>
    public async Task SaveAsync(Post post, string? tagsInput, IEnumerable<int> seriesIds)
    {
        var isNew = post.Id == 0;

        if (isNew)
            post.CreatedOnUtc = DateTime.UtcNow;
        else
            post.ModifiedOnUtc = DateTime.UtcNow;

        // slug handling
        if (string.IsNullOrWhiteSpace(post.Slug))
            post.Slug = _content.Slugify(post.Title);
        else
            post.Slug = _content.Slugify(post.Slug);

        post.Slug = await EnsureUniqueSlugAsync(post.Slug, post.Id);

        if (post.Status == PostStatus.Published && post.PublishedOnUtc == null)
            post.PublishedOnUtc = DateTime.UtcNow;

        string? oldSlug = null;
        if (!isNew)
        {
            // read the stored slug first — the caller may already have mutated the tracked entity
            oldSlug = await _db.Posts.AsNoTracking()
                .Where(p => p.Id == post.Id)
                .Select(p => p.Slug)
                .FirstOrDefaultAsync();

            var existing = await _db.Posts.Include(p => p.PostTags).Include(p => p.PostSeries)
                .FirstOrDefaultAsync(p => p.Id == post.Id);
            if (existing == null)
                return;

            oldSlug = oldSlug != post.Slug ? oldSlug : null;

            existing.Title = post.Title;
            existing.Slug = post.Slug;
            existing.Summary = post.Summary;
            existing.ContentMd = post.ContentMd;
            existing.CoverImageUrl = post.CoverImageUrl;
            existing.AuthorName = post.AuthorName;
            existing.CategoryId = post.CategoryId;
            existing.Status = post.Status;
            existing.IsFixedTop = post.IsFixedTop;
            existing.MetaTitle = post.MetaTitle;
            existing.MetaDescription = post.MetaDescription;
            existing.PublishedOnUtc = post.PublishedOnUtc;
            existing.ModifiedOnUtc = post.ModifiedOnUtc;

            post = existing;
        }
        else
        {
            _db.Posts.Add(post);
        }

        await _db.SaveChangesAsync();

        // tags
        var names = _content.ParseTags(tagsInput);
        var desired = new List<Tag>();
        foreach (var name in names)
            desired.Add(await ResolveTagAsync(name));

        var current = await _db.PostTags.Where(pt => pt.PostId == post.Id).ToListAsync();
        _db.PostTags.RemoveRange(current.Where(c => desired.All(d => d.Id != c.TagId)));
        foreach (var tag in desired.Where(t => current.All(c => c.TagId != t.Id)))
            _db.PostTags.Add(new PostTag { PostId = post.Id, TagId = tag.Id });

        // series
        var wantedIds = seriesIds?.Distinct().ToList() ?? new List<int>();
        var currentSeries = await _db.PostSeries.Where(ps => ps.PostId == post.Id).ToListAsync();
        _db.PostSeries.RemoveRange(currentSeries.Where(c => wantedIds.All(id => id != c.SeriesId)));
        var order = 1;
        foreach (var id in wantedIds)
        {
            var link = currentSeries.FirstOrDefault(c => c.SeriesId == id);
            if (link == null)
                _db.PostSeries.Add(new PostSeries { PostId = post.Id, SeriesId = id, DisplayOrder = order });
            else
                link.DisplayOrder = order;
            order++;
        }

        // slug history → 301
        if (oldSlug != null)
        {
            var duplicate = await _db.SlugHistory.FirstOrDefaultAsync(h => h.OldSlug == oldSlug);
            if (duplicate != null)
                _db.SlugHistory.Remove(duplicate);
            _db.SlugHistory.Add(new SlugHistory { PostId = post.Id, OldSlug = oldSlug, ChangedOnUtc = DateTime.UtcNow });
            // the new slug is no longer a redirect target
            await _db.SlugHistory.Where(h => h.OldSlug == post.Slug && h.PostId != post.Id).ExecuteDeleteAsync();
        }

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
            return;
        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
    }

    private async Task<Tag> ResolveTagAsync(string name)
    {
        var slug = _content.Slugify(name);
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug);
        if (tag != null)
            return tag;

        tag = new Tag { Name = name, Slug = slug };
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();
        return tag;
    }

    private async Task<string> EnsureUniqueSlugAsync(string slug, int excludeId)
    {
        var candidate = slug;
        var i = 2;
        while (await _db.Posts.AnyAsync(p => p.Slug == candidate && p.Id != excludeId))
        {
            candidate = $"{slug}-{i}";
            i++;
        }
        return candidate;
    }
}

public class RankItem
{
    public Post Post { get; set; } = null!;
    public int Views { get; set; }
}

public class DailyViews
{
    public DateTime Day { get; set; }
    public int Views { get; set; }
}

public class ArchiveMonth
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Count { get; set; }
}
