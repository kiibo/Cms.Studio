using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Cms.Studio.Core.Data;
using Cms.Studio.Core.Domain;

namespace Cms.Studio.Web.Infrastructure;

/// <summary>
/// Owns the hidden admin console segment. The console lives at <c>/{segment}/…</c> instead of
/// the publicly known <c>/admin/…</c>, so visitors and scanners cannot discover the backend.
///
/// Resolution order:
///   1. <c>AdminAccount:AdminPath</c> in appsettings.json / environment variables (if valid),
///   2. the <c>AdminPath</c> row in the Setting table (survives restarts),
///   3. otherwise a random segment is generated and persisted on first run.
/// </summary>
public sealed class AdminPathProvider
{
    public const string SettingKey = "AdminPath";

    private static readonly Regex SegmentPattern =
        new("^[a-z0-9][a-z0-9-]{2,63}$", RegexOptions.Compiled);

    /// <summary>Segments that could collide with public routes / static folders.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "blog", "category", "tag", "tags", "series", "archive", "search", "rank",
        "rss", "feed", "feed.xml", "sitemap", "sitemap.xml", "robots", "robots.txt",
        "llms", "llms.txt", "llms-full", "llms-full.txt", "error", "css", "js", "vendor",
        "uploads", "favicon", "favicon.ico", "index", "home", "login", "logout"
    };

    private static AdminPathProvider? _current;

    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;

    public AdminPathProvider(IConfiguration configuration, IServiceScopeFactory scopeFactory)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _current = this;
    }

    /// <summary>The current hidden segment (no leading slash), e.g. <c>manage-a1b2c3d4</c>.</summary>
    public string Segment { get; private set; } = "admin";

    /// <summary>True once a hidden (non-default) segment is active.</summary>
    public bool IsHidden => !string.Equals(Segment, "admin", StringComparison.Ordinal);

    public static AdminPathProvider Current =>
        _current ?? throw new InvalidOperationException("AdminPathProvider has not been created yet.");

    /// <summary>Builds an admin URL for the current segment: <c>Ui.Admin("/posts")</c> → <c>/{segment}/posts</c>.</summary>
    public static string Url(string path = "") => BuildUrl(_current?.Segment ?? "admin", path);

    public string UrlFor(string path = "") => BuildUrl(Segment, path);

    private static string BuildUrl(string segment, string path)
    {
        path ??= string.Empty;
        if (path.Length > 0 && path[0] != '/')
            path = "/" + path;
        return "/" + segment + path;
    }

    /// <summary>Called once at startup, after the schema exists. Generates + persists when needed.</summary>
    public void Initialize()
    {
        var configured = Normalize(_configuration["AdminAccount:AdminPath"]);
        if (IsValid(configured))
        {
            Segment = configured!;
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var stored = Normalize(db.Settings.AsNoTracking().FirstOrDefault(s => s.Key == SettingKey)?.Value);
        if (IsValid(stored))
        {
            Segment = stored!;
            return;
        }

        Segment = GenerateSegment();
        db.Settings.Add(new Setting { Key = SettingKey, Value = Segment });
        db.SaveChanges();
    }

    /// <summary>Validates and applies a new segment (admin settings page). Persists to the Setting table.</summary>
    public bool TryChange(string? requested, out string? error)
    {
        var segment = Normalize(requested);
        if (!IsValid(segment))
        {
            error = "Use 3–64 characters: lowercase letters, digits and dashes (must start with a letter or digit), " +
                    "and pick something that is not a public route name.";
            return false;
        }

        if (string.Equals(segment, Segment, StringComparison.OrdinalIgnoreCase))
        {
            error = null;
            return true;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var row = db.Settings.FirstOrDefault(s => s.Key == SettingKey);
        if (row == null)
            db.Settings.Add(new Setting { Key = SettingKey, Value = segment! });
        else
            row.Value = segment!;
        db.SaveChanges();

        Segment = segment!;
        error = null;
        return true;
    }

    private static string? Normalize(string? value)
    {
        var s = (value ?? string.Empty).Trim().Trim('/').ToLowerInvariant();
        return s.Length == 0 ? null : s;
    }

    private static bool IsValid(string? segment) =>
        segment != null && SegmentPattern.IsMatch(segment) && !Reserved.Contains(segment);

    private static string GenerateSegment() =>
        "manage-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
}
