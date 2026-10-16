namespace Cms.Studio.Web.Infrastructure;

/// <summary>
/// Baseline security headers for every response. The CSP blocks external script/asset origins
/// (everything is self-hosted except the admin editor's jsDelivr build), inline event handlers
/// that load remote code, <c>&lt;object&gt;</c> plugins and clickjacking via <c>frame-ancestors</c>.
/// HSTS is emitted only when the request already arrived over HTTPS, so enabling this can never
/// lock anyone out of a plain-HTTP deployment.
/// </summary>
public class SecurityHeadersMiddleware
{
    /// <summary>
    /// Conservative default. The admin editor (Vditor) loads from cdn.jsdelivr.net — all of its
    /// sub-assets are pinned to the same origin via its <c>cdn</c> option. Override per deployment
    /// with <c>Security:ContentSecurityPolicy</c> (set it to <c>off</c> to disable the CSP entirely).
    /// </summary>
    public const string DefaultContentSecurityPolicy =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'self'; " +
        "form-action 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "img-src 'self' data: blob: https:; " +
        "media-src 'self' https:; " +
        "font-src 'self' data: https:; " +
        "connect-src 'self'; " +
        "frame-src https:; " +
        "worker-src 'self' blob:";

    private readonly RequestDelegate _next;
    private readonly string? _csp;

    public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        var configured = configuration["Security:ContentSecurityPolicy"]?.Trim();
        _csp = string.IsNullOrEmpty(configured)
            ? DefaultContentSecurityPolicy
            : string.Equals(configured, "off", StringComparison.OrdinalIgnoreCase) ? null : configured;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        if (!headers.ContainsKey("X-Content-Type-Options"))
            headers["X-Content-Type-Options"] = "nosniff";
        if (!headers.ContainsKey("X-Frame-Options"))
            headers["X-Frame-Options"] = "SAMEORIGIN";
        if (!headers.ContainsKey("Referrer-Policy"))
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        if (!headers.ContainsKey("Permissions-Policy"))
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        if (_csp != null && !headers.ContainsKey("Content-Security-Policy"))
            headers["Content-Security-Policy"] = _csp;

        if (context.Request.IsHttps && !headers.ContainsKey("Strict-Transport-Security"))
            headers["Strict-Transport-Security"] = "max-age=15552000";

        await _next(context);
    }
}
