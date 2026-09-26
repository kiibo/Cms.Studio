using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Cms.Studio.Core.Data;
using Cms.Studio.Core.Services;

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
    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("10.0.0.0"), 8));
    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("172.16.0.0"), 12));
    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse("192.168.0.0"), 16));
});

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

// ---------- web ----------
builder.Services.AddControllersWithViews();
// The editor uploads images via fetch/XHR, so accept the antiforgery token as a header too.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.LogoutPath = "/admin/logout";
        options.AccessDeniedPath = "/admin/login";
        options.Cookie.Name = "cmsstudio.admin";
        options.Cookie.HttpOnly = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

DbInitializer.Initialize(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

// Must run first so everything downstream sees the real client IP and scheme.
app.UseForwardedHeaders();

app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<Cms.Studio.Web.Infrastructure.VisitTrackingMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
