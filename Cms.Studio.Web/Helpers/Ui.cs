using System.Globalization;
using System.Text;
using System.Text.Json;
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
