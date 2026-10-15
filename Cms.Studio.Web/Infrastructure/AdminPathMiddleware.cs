namespace Cms.Studio.Web.Infrastructure;

/// <summary>
/// Keeps the admin console hidden:
///   1. rewrites <c>/{hidden-segment}/…</c> to the internal <c>/admin/…</c> routes,
///   2. answers plain 404 for <c>/admin/…</c> (probing the old path reveals nothing),
///   3. rewrites redirect Location headers so MVC redirects never leak <c>/admin</c>,
///   4. marks admin responses <c>X-Robots-Tag: noindex, nofollow</c>.
/// </summary>
public class AdminPathMiddleware
{
    private readonly RequestDelegate _next;

    public AdminPathMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AdminPathProvider adminPath)
    {
        var hiddenRoot = "/" + adminPath.Segment;
        var path = context.Request.Path.Value ?? string.Empty;

        if (HasSegmentPrefix(path, hiddenRoot, out var rest))
        {
            context.Request.Path = "/admin" + rest;
            context.Response.OnStarting(static state =>
            {
                var ctx = (HttpContext)state!;
                if (!ctx.Response.Headers.ContainsKey("X-Robots-Tag"))
                    ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
                return Task.CompletedTask;
            }, context);
        }
        else if (HasSegmentPrefix(path, "/admin", out _))
        {
            // The old public path is gone — make it indistinguishable from any missing page.
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Redirects produced by MVC (RedirectToAction etc.) still contain the internal
        // /admin prefix — translate them back before the client ever sees it.
        var hidden = hiddenRoot;
        context.Response.OnStarting(static state =>
        {
            var (ctx, root) = ((HttpContext, string))state!;
            var location = ctx.Response.Headers.Location.ToString();
            if (!string.IsNullOrEmpty(location) && HasSegmentPrefix(location, "/admin", out var rest))
                ctx.Response.Headers.Location = root + rest;
            return Task.CompletedTask;
        }, (context, hidden));

        await _next(context);
    }

    /// <summary>Case-insensitive prefix match on a whole path segment ("/admin" does not match "/administrator").</summary>
    internal static bool HasSegmentPrefix(string path, string prefix, out string rest)
    {
        rest = string.Empty;
        if (path.Length < prefix.Length || !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.Length == prefix.Length)
            return true;

        var next = path[prefix.Length];
        if (next is '/' or '?' or '#')
        {
            rest = path[prefix.Length..];
            return true;
        }
        return false;
    }
}
