using Microsoft.EntityFrameworkCore;
using Cms.Studio.Core.Data;
using Cms.Studio.Core.Domain;

namespace Cms.Studio.Core.Services;

/// <summary>Site settings stored in the Setting table with a small in-memory cache.</summary>
public class SettingService
{
    public const string SiteTitleKey = "SiteTitle";
    public const string SiteDescriptionKey = "SiteDescription";
    public const string SiteBaseUrlKey = "SiteBaseUrl";
    public const string PostsPerPageKey = "PostsPerPage";
    public const string AuthorNameKey = "AuthorName";

    private readonly CmsDbContext _db;
    private static readonly Dictionary<string, string> Cache = new();
    private static readonly object Lock = new();

    public SettingService(CmsDbContext db)
    {
        _db = db;
    }

    public async Task<string> GetAsync(string key, string defaultValue = "")
    {
        lock (Lock)
        {
            if (Cache.TryGetValue(key, out var cached))
                return cached;
        }

        var setting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        var value = setting?.Value ?? defaultValue;

        lock (Lock)
        {
            Cache[key] = value;
        }
        return value;
    }

    public async Task SetAsync(string key, string value)
    {
        var setting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
            _db.Settings.Add(new Setting { Key = key, Value = value });
        else
            setting.Value = value;

        await _db.SaveChangesAsync();

        lock (Lock)
        {
            Cache[key] = value;
        }
    }

    public Task<string> GetSiteTitleAsync() => GetAsync(SiteTitleKey, "Cms.Studio");
    public Task<string> GetSiteDescriptionAsync() => GetAsync(SiteDescriptionKey, "");
    public Task<string> GetBaseUrlAsync() => GetAsync(SiteBaseUrlKey, "");
    public Task<string> GetAuthorNameAsync() => GetAsync(AuthorNameKey, "Admin");

    public async Task<int> GetPostsPerPageAsync()
    {
        var raw = await GetAsync(PostsPerPageKey, "10");
        return int.TryParse(raw, out var size) && size > 0 ? size : 10;
    }
}
