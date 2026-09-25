using Microsoft.AspNetCore.Authentication.Cookies;
using Cms.Studio.Core.Data;
using Cms.Studio.Core.Services;

var builder = WebApplication.CreateBuilder(args);

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

app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<Cms.Studio.Web.Infrastructure.VisitTrackingMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
