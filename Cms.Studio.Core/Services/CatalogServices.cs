using Microsoft.EntityFrameworkCore;
using Cms.Studio.Core.Data;
using Cms.Studio.Core.Domain;

namespace Cms.Studio.Core.Services;

/// <summary>Catalog services for categories, tags and series (lightweight CRUD).</summary>
public class CategoryService
{
    private readonly CmsDbContext _db;
    private readonly ContentService _content;

    public CategoryService(CmsDbContext db, ContentService content)
    {
        _db = db;
        _content = content;
    }

    public Task<List<Category>> GetAllAsync(bool showHidden = true) =>
        _db.Categories
            .Where(c => showHidden || c.ShowOnMenu)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();

    public Task<Category?> GetByIdAsync(int id) => _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
    public Task<Category?> GetBySlugAsync(string slug) => _db.Categories.FirstOrDefaultAsync(c => c.Slug == slug);

    public async Task SaveAsync(Category category)
    {
        category.Slug = string.IsNullOrWhiteSpace(category.Slug) ? _content.Slugify(category.Name) : _content.Slugify(category.Slug);
        if (category.Id == 0)
            _db.Categories.Add(category);
        else
            _db.Categories.Update(category);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null)
            return;
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
    }
}

public class TagService
{
    private readonly CmsDbContext _db;
    private readonly ContentService _content;

    public TagService(CmsDbContext db, ContentService content)
    {
        _db = db;
        _content = content;
    }

    public Task<List<Tag>> GetAllAsync() => _db.Tags.OrderBy(t => t.Name).ToListAsync();

    public Task<Tag?> GetBySlugAsync(string slug) => _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug);

    /// <summary>Tags ordered by published-post usage — powers the sidebar tag cloud.</summary>
    public Task<List<TagWithCount>> GetTopAsync(int count) =>
        _db.PostTags
            .Where(pt => pt.Post.Status == PostStatus.Published)
            .GroupBy(pt => pt.Tag)
            .Select(g => new TagWithCount { Tag = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Tag.Name)
            .Take(count)
            .ToListAsync();

    public async Task<Tag> GetOrCreateAsync(string name)
    {
        var slug = _content.Slugify(name);
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug);
        if (tag != null)
            return tag;

        tag = new Tag { Name = name.Trim(), Slug = slug };
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();
        return tag;
    }

    public async Task DeleteAsync(int id)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id);
        if (tag == null)
            return;
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();
    }
}

public class TagWithCount
{
    public Tag Tag { get; set; } = null!;
    public int Count { get; set; }
}

public class SeriesService
{
    private readonly CmsDbContext _db;
    private readonly ContentService _content;

    public SeriesService(CmsDbContext db, ContentService content)
    {
        _db = db;
        _content = content;
    }

    public Task<List<Series>> GetAllAsync() => _db.Series.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Title).ToListAsync();
    public Task<Series?> GetByIdAsync(int id) => _db.Series.FirstOrDefaultAsync(s => s.Id == id);
    public Task<Series?> GetBySlugAsync(string slug) => _db.Series.FirstOrDefaultAsync(s => s.Slug == slug);

    public async Task SaveAsync(Series series)
    {
        series.Slug = string.IsNullOrWhiteSpace(series.Slug) ? _content.Slugify(series.Title) : _content.Slugify(series.Slug);
        if (series.Id == 0)
            _db.Series.Add(series);
        else
            _db.Series.Update(series);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var series = await _db.Series.FirstOrDefaultAsync(s => s.Id == id);
        if (series == null)
            return;
        _db.Series.Remove(series);
        await _db.SaveChangesAsync();
    }
}
