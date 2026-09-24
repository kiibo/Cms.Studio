using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cms.Studio.Web.Models;

namespace Cms.Studio.Web.Controllers.Admin;

public class AdminAccountController : Controller
{
    private readonly IConfiguration _configuration;

    public AdminAccountController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("/admin/login")]
    public IActionResult Login() => View("~/Views/Admin/Login.cshtml", new LoginViewModel());

    [HttpPost("/admin/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var username = _configuration["AdminAccount:Username"] ?? "admin";
        var password = _configuration["AdminAccount:Password"] ?? "change-me";

        if (model.Username != username || model.Password != password)
        {
            model.Error = "Invalid username or password.";
            return View("~/Views/Admin/Login.cshtml", model);
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, model.Username) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToAction("Dashboard", "AdminDashboard");
    }

    [Authorize]
    [HttpGet("/admin/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }
}
