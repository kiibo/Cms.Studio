using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cms.Studio.Core.Domain;

namespace Cms.Studio.Web.Helpers;

/// <summary>Small UI / SEO helpers shared by views and controllers.</summary>
public static class Ui
{
    public static string Date(DateTime? utc)
    {
        return utc == null ? "-" : utc.Value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    public static string DateTimeShort(DateTime? utc)
    {
        return utc == null ? "-" : utc.Value.ToString("MMM d, yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    public static string Iso(DateTime? utc)
    {
        return utc == null ? "" : utc.Value.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    public static string Absolute(string baseUrl, string relativePath)
    {
        return baseUrl.TrimEnd('/') + "/" + relativePath.TrimStart('/');
    }

    private static readonly Regex MdImage = new(@"!\[[^\]]*\]\((?<url>[^\s\)]+)", RegexOptions.Compiled);
    private static readonly Regex HtmlImage = new(@"<img[^>]+src=[""'](?<url>[^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Cover image for cards / hero / og:image. Explicit CoverImageUrl wins,
    /// otherwise the first image found in the Markdown body (Masuit.MyBlogs style) is used.
    /// </summary>
    public static string? CoverUrl(Post post)
    {
        if (!string.IsNullOrWhiteSpace(post.CoverImageUrl))
            return post.CoverImageUrl;
        if (string.IsNullOrWhiteSpace(post.ContentMd))
            return null;

        var m = MdImage.Match(post.ContentMd);
        if (!m.Success)
            m = HtmlImage.Match(post.ContentMd);
        return m.Success ? m.Groups["url"].Value : null;
    }

    // ---- inline nav icons (gizmodo-style, self-hosted SVG, no icon-font/CDN) ----
    private static string Ico(string inner) =>
        "<svg class='nav-ic-svg' width='18' height='18' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' aria-hidden='true'>" + inner + "</svg>";

    /// <summary>Section icon for a nav item, keyed by category slug or a fixed key
    /// (home / daily / weekly / monthly). Unknown keys fall back to a neutral icon.</summary>
    public static string NavIcon(string? key) => (key ?? "").Trim().ToLowerInvariant() switch
    {
        "home" => Ico("<path d='M3 9.5 12 3l9 6.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z'/><path d='M9 21v-6h6v6'/>") ,
        "smartphones" or "smartphone" or "phones" or "phone" or "mobile" or "mobiles" =>
            Ico("<rect x='6' y='2' width='12' height='20' rx='2.5'/><line x1='11' y1='18.5' x2='13' y2='18.5'/>") ,
        "computers" or "computer" or "laptops" or "laptop" or "pcs" or "pc" or "desktop" or "desktops" =>
            Ico("<rect x='3' y='4' width='18' height='12' rx='2'/><line x1='2' y1='20' x2='22' y2='20'/>") ,
        "smart-devices" or "smartdevices" or "devices" or "wearables" or "watch" or "watches" or "gadgets" =>
            Ico("<rect x='7' y='6' width='10' height='12' rx='3'/><path d='M9 6V3h6v3M9 18v3h6v-3'/>") ,
        "daily" or "daily-top" or "hot" or "trending" =>
            Ico("<path d='M12 2c1 3 4 4.5 4 8a4 4 0 0 1-8 0c0-1 .3-2 .8-2.8C9 8.2 8.5 9.6 8.5 11a3.5 3.5 0 1 0 7 0c0-3-2-5-3.5-9z'/>") ,
        "weekly" or "weekly-top" =>
            Ico("<rect x='3' y='4' width='18' height='18' rx='2'/><line x1='16' y1='2' x2='16' y2='6'/><line x1='8' y1='2' x2='8' y2='6'/><line x1='3' y1='10' x2='21' y2='10'/>") ,
        "monthly" or "monthly-top" or "top" or "award" =>
            Ico("<circle cx='12' cy='8' r='6'/><polyline points='8.2 13.9 7 22.5 12 19.5 17 22.5 15.8 13.9'/>") ,
        _ => Ico("<line x1='4' y1='9' x2='20' y2='9'/><line x1='4' y1='15' x2='20' y2='15'/><line x1='10' y1='3' x2='8' y2='21'/><line x1='16' y1='3' x2='14' y2='21'/>")
    };

    /// <summary>Sidebar icon for the admin console, keyed by menu key.</summary>
    public static string AdminIcon(string? key) => (key ?? "").Trim().ToLowerInvariant() switch
    {
        "dashboard" => Ico("<rect x='3' y='3' width='7' height='9' rx='1.5'/><rect x='14' y='3' width='7' height='5' rx='1.5'/><rect x='14' y='12' width='7' height='9' rx='1.5'/><rect x='3' y='16' width='7' height='5' rx='1.5'/>") ,
        "posts" => Ico("<path d='M12 20h9'/><path d='M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z'/>") ,
        "comments" => Ico("<path d='M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z'/>") ,
        "categories" => Ico("<path d='M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z'/>") ,
        "series" => Ico("<path d='M4 19.5A2.5 2.5 0 0 1 6.5 17H20'/><path d='M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z'/>") ,
        "tags" => Ico("<path d='M20.59 13.41 13.42 20.58a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.83z'/><line x1='7' y1='7' x2='7.01' y2='7'/>") ,
        "settings" => Ico("<circle cx='12' cy='12' r='3'/><path d='M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09a1.65 1.65 0 0 0-1-1.51 1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09a1.65 1.65 0 0 0 1.51-1 1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33h.01a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51h.01a1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82v.01a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z'/>") ,
        _ => Ico("<circle cx='12' cy='12' r='9'/>")
    };
}

/// <summary>schema.org JSON-LD builders for rich results and LLM understanding.</summary>
public static class JsonLd
{
    public static string BlogPosting(Post post, string url, string siteTitle, string siteDescription)
    {
        var payload = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BlogPosting",
            ["headline"] = post.MetaTitle ?? post.Title,
            ["description"] = post.MetaDescription ?? post.Summary,
            ["datePublished"] = Ui.Iso(post.PublishedOnUtc ?? post.CreatedOnUtc),
            ["dateModified"] = Ui.Iso(post.ModifiedOnUtc ?? post.PublishedOnUtc ?? post.CreatedOnUtc),
            ["url"] = url,
            ["mainEntityOfPage"] = new Dictionary<string, object?> { ["@type"] = "WebPage", ["@id"] = url },
            ["author"] = new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = string.IsNullOrWhiteSpace(post.AuthorName) ? "Admin" : post.AuthorName },
            ["publisher"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["name"] = siteTitle,
                ["description"] = siteDescription
            },
            ["keywords"] = string.Join(",", post.PostTags.Select(pt => pt.Tag.Name)),
            ["inLanguage"] = "en"
        };
        return JsonSerializer.Serialize(payload);
    }

    public static string WebSite(string url, string siteTitle, string siteDescription)
    {
        var payload = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebSite",
            ["name"] = siteTitle,
            ["description"] = siteDescription,
            ["url"] = url,
            ["inLanguage"] = "en",
            ["potentialAction"] = new Dictionary<string, object?>
            {
                ["@type"] = "SearchAction",
                ["target"] = url.TrimEnd('/') + "/search?q={search_term_string}",
                ["query-input"] = "required name=search_term_string"
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    public static string Breadcrumb(string siteTitle, string baseUrl, params (string Name, string Url)[] items)
    {
        var list = new List<object> { new Dictionary<string, object?> { ["@type"] = "ListItem", ["position"] = 1, ["name"] = siteTitle, ["item"] = baseUrl.TrimEnd('/') + "/" } };
        var pos = 2;
        foreach (var (name, url) in items)
        {
            list.Add(new Dictionary<string, object?> { ["@type"] = "ListItem", ["position"] = pos++, ["name"] = name, ["item"] = Ui.Absolute(baseUrl, url) });
        }
        var payload = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = list
        };
        return JsonSerializer.Serialize(payload);
    }
}
