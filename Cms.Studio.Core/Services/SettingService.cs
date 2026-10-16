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
    public const string SiteLogoKey = "SiteLogo";
    public const string SiteBrandTextKey = "SiteBrandText";
    public const string ArticleStyleKey = "ArticleStyle";
    public const string ArticleTemplateKey = "ArticleTemplate";

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

    /// <summary>Uploaded brand logo URL (empty → the default CSS mark is shown).</summary>
    public Task<string> GetSiteLogoAsync() => GetAsync(SiteLogoKey, "");

    /// <summary>Brand text in the header. Falls back to the classic "Cms.Studio" logo text when unset/blank.</summary>
    public async Task<string> GetSiteBrandTextAsync()
    {
        var value = await GetAsync(SiteBrandTextKey, "Cms.Studio");
        return string.IsNullOrWhiteSpace(value) ? "Cms.Studio" : value.Trim();
    }

    /// <summary>Article reading style preset: default | comfy | paper | large (classic template only).</summary>
    public async Task<string> GetArticleStyleAsync()
    {
        var value = await GetAsync(ArticleStyleKey, "default");
        return value is "comfy" or "paper" or "large" ? value : "default";
    }

    /// <summary>Article display template: classic (original site style) | huashu (花叔 editorial).</summary>
    public async Task<string> GetArticleTemplateAsync()
    {
        var value = await GetAsync(ArticleTemplateKey, "classic");
        return value == "huashu" ? "huashu" : "classic";
    }

    public async Task<int> GetPostsPerPageAsync()
    {
        var raw = await GetAsync(PostsPerPageKey, "10");
        return int.TryParse(raw, out var size) && size > 0 ? size : 10;
    }
}
