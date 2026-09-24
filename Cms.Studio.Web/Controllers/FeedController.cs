using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Cms.Studio.Core.Domain;
using Cms.Studio.Core.Services;
using Cms.Studio.Web.Helpers;

namespace Cms.Studio.Web.Controllers;

/// <summary>Machine-facing endpoints: RSS, sitemap.xml, robots.txt, llms.txt, llms-full.txt.</summary>
public class FeedController : Controller
{
    private readonly PostService _posts;
    private readonly CategoryService _categories;
    private readonly TagService _tags;
    private readonly SeriesService _series;
    private readonly SettingService _settings;
    private readonly ContentService _content;

    public FeedController(PostService posts, CategoryService categories, TagService tags,
        SeriesService series, SettingService settings, ContentService content)
    {
        _posts = posts;
        _categories = categories;
        _tags = tags;
        _series = series;
        _settings = settings;
        _content = content;
    }

    [HttpGet("/rss")]
    [HttpGet("/feed.xml")]
    public async Task<IActionResult> Rss()
    {
        var baseUrl = await _settings.GetBaseUrlAsync();
        var siteTitle = await _settings.GetSiteTitleAsync();
        var siteDescription = await _settings.GetSiteDescriptionAsync();
        var feed = await _posts.GetFeedAsync(1, 20);

        XNamespace atom = "http://www.w3.org/2005/Atom";
        var channel = new XElement("channel",
            new XElement("title", siteTitle),
            new XElement("link", Ui.Absolute(baseUrl, "/")),
            new XElement("description", siteDescription),
            new XElement("language", "en"),
            new XElement("lastBuildDate", DateTime.UtcNow.ToString("R")),
            new XElement(atom + "link", new XAttribute("href", Ui.Absolute(baseUrl, "/rss")), new XAttribute("rel", "self"), new XAttribute("type", "application/rss+xml")));

        foreach (var post in feed.Items)
        {
            var url = Ui.Absolute(baseUrl, $"/blog/{post.Slug}");
            channel.Add(new XElement("item",
                new XElement("title", post.Title),
                new XElement("link", url),
                new XElement("guid", url),
                new XElement("pubDate", (post.PublishedOnUtc ?? post.CreatedOnUtc).ToString("R")),
                new XElement("description", post.Summary),
                new XElement("author", post.AuthorName)));
        }

        var rss = new XDocument(new XElement("rss", new XAttribute("version", "2.0"), channel));
        return Content(rss.ToString(), "application/rss+xml; charset=utf-8");
    }

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var baseUrl = await _settings.GetBaseUrlAsync();
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urlset = new XElement(ns + "urlset");

        void Add(string relative, DateTime? lastMod, string priority)
        {
            var element = new XElement(ns + "url", new XElement(ns + "loc", Ui.Absolute(baseUrl, relative)));
            if (lastMod != null)
                element.Add(new XElement(ns + "lastmod", lastMod.Value.ToString("yyyy-MM-dd")));
            element.Add(new XElement(ns + "changefreq", "weekly"));
            element.Add(new XElement(ns + "priority", priority));
            urlset.Add(element);
        }

        Add("/", DateTime.UtcNow, "1.0");
        var feed = await _posts.GetFeedAsync(1, 500);
        foreach (var post in feed.Items)
            Add($"/blog/{post.Slug}", post.ModifiedOnUtc ?? post.PublishedOnUtc, "0.8");

        foreach (var category in await _categories.GetAllAsync(showHidden: false))
            Add($"/category/{category.Slug}", null, "0.6");
        foreach (var tag in await _tags.GetAllAsync())
            Add($"/tag/{tag.Slug}", null, "0.4");
        foreach (var series in await _series.GetAllAsync())
            Add($"/series/{series.Slug}", null, "0.5");

        var sitemap = new XDocument(new XElement(ns + "urlset", urlset.Elements()));
        return Content(sitemap.ToString(), "application/xml; charset=utf-8");
    }

    [HttpGet("/robots.txt")]
    public async Task<IActionResult> Robots()
    {
        var baseUrl = (await _settings.GetBaseUrlAsync()).TrimEnd('/');
        var sb = new StringBuilder();
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Allow: /");
        sb.AppendLine("Disallow: /admin");
        sb.AppendLine();
        sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");
        sb.AppendLine();
        sb.AppendLine("# AI assistants: a structured summary lives at /llms.txt");
        sb.AppendLine("# and the full site content at /llms-full.txt");
        return Content(sb.ToString(), "text/plain; charset=utf-8");
    }

    /// <summary>llms.txt — curated site index for LLM crawlers (llmstxt.org).</summary>
    [HttpGet("/llms.txt")]
    public async Task<IActionResult> Llms()
    {
        var baseUrl = await _settings.GetBaseUrlAsync();
        var siteTitle = await _settings.GetSiteTitleAsync();
        var siteDescription = await _settings.GetSiteDescriptionAsync();
        var feed = await _posts.GetFeedAsync(1, 100);

        var sb = new StringBuilder();
        sb.AppendLine($"# {siteTitle}");
        sb.AppendLine();
        sb.AppendLine($"> {siteDescription}");
        sb.AppendLine();
        sb.AppendLine("This site publishes technical articles in English. Full article text is mirrored in Markdown at `/llms-full.txt` and per-article at `/blog/{slug}.md`.");
        sb.AppendLine();

        sb.AppendLine("## Categories");
        sb.AppendLine();
        foreach (var category in await _categories.GetAllAsync(showHidden: false))
            sb.AppendLine($"- [{category.Name}]({Ui.Absolute(baseUrl, $"/category/{category.Slug}")}): {category.Description}");
        sb.AppendLine();

        sb.AppendLine("## Articles");
        sb.AppendLine();
        foreach (var post in feed.Items)
            sb.AppendLine($"- [{post.Title}]({Ui.Absolute(baseUrl, $"/blog/{post.Slug}")}): {post.Summary}");

        return Content(sb.ToString(), "text/markdown; charset=utf-8");
    }

    /// <summary>llms-full.txt — complete article corpus in Markdown.</summary>
    [HttpGet("/llms-full.txt")]
    public async Task<IActionResult> LlmsFull()
    {
        var baseUrl = await _settings.GetBaseUrlAsync();
        var siteTitle = await _settings.GetSiteTitleAsync();
        var feed = await _posts.GetFeedAsync(1, 500);

        var sb = new StringBuilder();
        sb.AppendLine($"# {siteTitle} — full content");
        sb.AppendLine();
        sb.AppendLine($"> {await _settings.GetSiteDescriptionAsync()}");
        sb.AppendLine();

        foreach (var post in feed.Items)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine(post.ContentMd);
            sb.AppendLine();
            sb.AppendLine($"Originally published at: {Ui.Absolute(baseUrl, $"/blog/{post.Slug}")}");
            sb.AppendLine();
        }

        return Content(sb.ToString(), "text/markdown; charset=utf-8");
    }
}
