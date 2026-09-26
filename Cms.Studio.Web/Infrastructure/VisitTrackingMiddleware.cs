using Cms.Studio.Core.Data;
using Cms.Studio.Core.Domain;
using Cms.Studio.Core.Services;

namespace Cms.Studio.Web.Infrastructure;

/// <summary>
/// Records every page request (IP, User-Agent, device/browser/platform, path, referer) into the
/// VisitRecord table. Static assets, admin pages and failed requests are skipped; recording never
/// breaks the response.
/// </summary>
public class VisitTrackingMiddleware
{
    private static readonly string[] SkipPrefixes =
    {
        "/admin", "/css", "/vendor", "/uploads", "/favicon", "/error"
    };

    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;

    public VisitTrackingMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (!ShouldTrack(context))
            return;

        try
        {
            var request = context.Request;
            var ip = ResolveIp(context);
            if (string.IsNullOrEmpty(ip))
                return;

            var ua = request.Headers.UserAgent.ToString();
            var client = UserAgentParser.Parse(ua);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            db.VisitRecords.Add(new VisitRecord
            {
                Ip = ip,
                UserAgent = ua.Length > 500 ? ua[..500] : ua,
                Browser = Truncate(client.Browser, 60),
                Platform = Truncate(client.Platform, 60),
                Device = Truncate(client.Device, 20),
                Path = Truncate(request.Path + request.QueryString.Value, 300),
                Referer = Truncate(request.Headers.Referer.ToString(), 300),
                VisitedOnUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Tracking must never break page rendering.
            // Log the exception but don't rethrow to ensure the request completes normally.
            System.Diagnostics.Debug.WriteLine($"VisitTracking error: {ex.Message}");
        }
    }

    private static bool ShouldTrack(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
            return false;
        if (context.Response.StatusCode >= 400)
            return false;

        var path = context.Request.Path.Value ?? string.Empty;
        return !SkipPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveIp(HttpContext context)
    {
        // ForwardedHeaders middleware (Program.cs) has already rewritten RemoteIpAddress
        // from X-Forwarded-For when the request came through a trusted proxy. Reading the
        // header directly here would let anyone spoof their IP with a forged header.
        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? null : value.Length <= max ? value : value[..max];
}
