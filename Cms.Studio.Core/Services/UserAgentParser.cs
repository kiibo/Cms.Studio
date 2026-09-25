using System.Text.RegularExpressions;

namespace Cms.Studio.Core.Services;

/// <summary>
/// Lightweight User-Agent parsing (browser / platform / device), adapted from the approach used by
/// Masuit.MyBlogs (regex keyword tables). Good enough for dashboards — not for forensic work.
/// </summary>
public static class UserAgentParser
{
    public sealed record ClientInfo(string Device, string? Browser, string? Platform, bool IsBot);

    private static readonly string[] BotKeywords =
    {
        "bot", "spider", "crawl", "slurp", "curl", "wget", "python-requests", "httpclient",
        "feedfetcher", "headless", "monitor", "uptime", "pingdom", "lighthouse", "ahrefs",
        "semrush", "yandex", "baiduspider", "sogou", "facebookexternalhit", "telegrambot", "wechat"
    };

    // Order matters: Edge/Opera/QQ embed Chromium tokens.
    private static readonly (string Pattern, string Name)[] Browsers =
    {
        ("Edg[A-Za-z]*", "Edge"),
        ("OPR|Opera", "Opera"),
        ("MicroMessenger", "WeChat"),
        ("MQQBrowser|QQBrowser", "QQ Browser"),
        ("SamsungBrowser", "Samsung Internet"),
        ("Baidu", "Baidu Browser"),
        ("FxiOS|Firefox", "Firefox"),
        ("Chrome|CriOS", "Chrome"),
        ("Safari", "Safari"),
        ("MSIE|Trident", "Internet Explorer"),
        ("Applebot", "AppleBot"),
        ("Googlebot", "Googlebot"),
        ("Bingbot|BingPreview", "Bingbot"),
    };

    private static readonly (string Pattern, string Name)[] Platforms =
    {
        ("Windows NT 10.0", "Windows 10/11"),
        ("Windows NT 6.3", "Windows 8.1"),
        ("Windows NT 6.[12]", "Windows 7/8"),
        ("Windows NT", "Windows"),
        ("Windows Phone", "Windows Phone"),
        ("Android", "Android"),
        ("iPhone|iPad|iPod|iOS", "iOS"),
        ("Mac OS X|Macintosh", "macOS"),
        ("CrOS", "Chrome OS"),
        ("Linux|X11|Ubuntu", "Linux"),
    };

    public static ClientInfo Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return new ClientInfo("Unknown", null, null, false);

        var ua = userAgent.Length > 500 ? userAgent[..500] : userAgent;
        var lower = ua.ToLowerInvariant();

        if (BotKeywords.Any(k => lower.Contains(k)))
        {
            var botName = Browsers.FirstOrDefault(b => Regex.IsMatch(ua, b.Pattern, RegexOptions.IgnoreCase)).Name;
            return new ClientInfo("Bot", string.IsNullOrEmpty(botName) ? "Robot" : botName, MatchPlatform(ua), IsBot: true);
        }

        var browser = MatchBrowser(ua);
        var platform = MatchPlatform(ua);
        var device = MatchDevice(ua, platform);
        return new ClientInfo(device, browser, platform, IsBot: false);
    }

    private static string? MatchBrowser(string ua) =>
        Browsers.FirstOrDefault(b => Regex.IsMatch(ua, b.Pattern, RegexOptions.IgnoreCase)).Name;

    private static string? MatchPlatform(string ua) =>
        Platforms.FirstOrDefault(p => Regex.IsMatch(ua, p.Pattern, RegexOptions.IgnoreCase)).Name;

    private static string MatchDevice(string ua, string? platform)
    {
        if (Regex.IsMatch(ua, @"iPad|Tablet|Nexus (?:7|9|10)|Kindle|Silk", RegexOptions.IgnoreCase))
            return "Tablet";
        if (Regex.IsMatch(ua, @"Mobile|iPhone|iPod|Android|Windows Phone|BlackBerry", RegexOptions.IgnoreCase))
            return "Mobile";
        return platform == "Android" ? "Tablet" : "Desktop";
    }
}
