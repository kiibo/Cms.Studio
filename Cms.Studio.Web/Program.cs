using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Cms.Studio.Core.Data;
using Cms.Studio.Core.Services;
using Cms.Studio.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------- reverse-proxy awareness (nginx / load balancers) ----------
// Without this, Connection.RemoteIpAddress is the proxy (e.g. 127.0.0.1) and the real
// visitor IP from X-Forwarded-For is ignored. The default KnownProxies list only contains
// IPv6 loopback (::1) — on Linux nginx connects via 127.0.0.1, so it must be added here.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1; // one reverse proxy in front
    options.KnownProxies.Add(System.Net.IPAddress.Loopback);                       // nginx on the same host (127.0.0.1)
    options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);                   // ::1 (default, kept explicitly)
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(System.Net.IPAddress.Parse("192.168.0.0"), 16));
});

// ---------- request body ceiling (uploads are capped at 10 MB per action) ----------
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 16 * 1024 * 1024);

// ---------- response compression (brotli + gzip for all text payloads) ----------
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = new[]
    {
        "text/html", "text/plain", "text/markdown", "text/css", "text/xml",
        "text/javascript", "application/javascript", "application/json",
        "application/xml", "application/rss+xml", "image/svg+xml"
    };
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// ---------- caching ----------
builder.Services.AddMemoryCache();

// ---------- visit analytics queue (page responses never wait on the log INSERT) ----------
builder.Services.AddSingleton<VisitQueue>();
builder.Services.AddHostedService<VisitWriterService>();
builder.Services.AddHostedService<UploadThumbnailBackfillService>();

// ---------- multi-database (nopCommerce-style provider selection) ----------
var dataProvider = builder.Configuration.GetSection("DataProvider").Get<DataProviderSettings>() ?? new DataProviderSettings();
builder.Services.AddSingleton(dataProvider);
builder.Services.AddDbContext<CmsDbContext>(options => dataProvider.ApplyTo(options));

// ---------- services ----------
builder.Services.AddSingleton<ContentService>();
builder.Services.AddSingleton(sp =>
{
    var settings = new SmtpSettings();
    builder.Configuration.GetSection("Smtp").Bind(settings);
    return settings;
});
builder.Services.AddSingleton<EmailSender>();
builder.Services.AddSingleton<EmailVerificationService>();
builder.Services.AddScoped<SettingService>();
builder.Services.AddScoped<PostService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<SeriesService>();
builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<CommentService>();

// ---------- security ----------
builder.Services.AddScoped<AdminAccountService>();   // credential verification / password changes

// ---------- web ----------
builder.Services.AddControllersWithViews();
// The editor uploads images via fetch/XHR, so accept the antiforgery token as a header too.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "cmsstudio.xsrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.LogoutPath = "/admin/logout";
        options.AccessDeniedPath = "/admin/login";
        options.Cookie.Name = "cmsstudio.admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// ---------- rate limiting (anti brute-force / anti flood) ----------
static string ClientPartition(HttpContext ctx) =>
    ctx.Connection.RemoteIpAddress?.ToString() ?? ctx.Connection.Id;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsync("Too many requests. Please slow down.", ct);
    };

    // Login attempts: 10/min per IP — credential brute-force becomes pointless
    // (≈14k attempts/day/IP against a 10+ character password is noise).
    options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        "auth:" + ClientPartition(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // Comment submit / verification-code requests: 10 per 10 min per IP.
    options.AddPolicy("comment", ctx => RateLimitPartition.GetFixedWindowLimiter(
        "comment:" + ClientPartition(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));

    // Search: cheap per request but the query scans post bodies — keep it to 30/min per IP.
    options.AddPolicy("search", ctx => RateLimitPartition.GetFixedWindowLimiter(
        "search:" + ClientPartition(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // Value feedback voting: 20/min per IP.
    options.AddPolicy("vote", ctx => RateLimitPartition.GetFixedWindowLimiter(
        "vote:" + ClientPartition(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // Generous global ceiling per IP so no single client can saturate the app.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientPartition(ctx),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var forceHttps = builder.Configuration.GetValue<bool>("Security:ForceHttps");

var app = builder.Build();

DbInitializer.Initialize(app.Services);

// ---------- startup security notice ----------
{
    var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Cms.Studio.Security");
    using var scope = app.Services.CreateScope();
    var accounts = scope.ServiceProvider.GetRequiredService<AdminAccountService>();
    if (accounts.IsUsingDefaultPasswordAsync().GetAwaiter().GetResult())
        log.LogWarning("The admin account still uses the default password. " +
                       "Change it now on the settings page: /admin/settings");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

// Must run first so everything downstream sees the real client IP and scheme.
app.UseForwardedHeaders();

app.UseMiddleware<SecurityHeadersMiddleware>();

if (forceHttps)
{
    app.UseHttpsRedirection();
}

app.UseResponseCompression();

// Static assets: long-lived caching (upload filenames are GUIDs → safe to treat as immutable).
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var isUpload = ctx.Context.Request.Path.StartsWithSegments("/uploads");
        ctx.Context.Response.Headers.CacheControl = isUpload
            ? "public, max-age=31536000, immutable"
            : "public, max-age=604800";
    }
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<Cms.Studio.Web.Infrastructure.VisitTrackingMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
