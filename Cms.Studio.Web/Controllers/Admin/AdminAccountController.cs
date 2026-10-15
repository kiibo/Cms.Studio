using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Cms.Studio.Web.Infrastructure;
using Cms.Studio.Web.Models;

namespace Cms.Studio.Web.Controllers.Admin;

public class AdminAccountController : Controller
{
    private readonly AdminAccountService _accounts;

    public AdminAccountController(AdminAccountService accounts)
    {
        _accounts = accounts;
    }

    [HttpGet("/admin/login")]
    [EnableRateLimiting("auth")]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Dashboard", "AdminDashboard");
        return View("~/Views/Admin/Login.cshtml", new LoginViewModel());
    }

    [HttpPost("/admin/login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // Generic error + timing-safe comparison: never reveal which half was wrong.
        var valid = await _accounts.VerifyAsync(model.Username, model.Password);
        if (!valid)
        {
            model.Error = "Invalid username or password.";
            return View("~/Views/Admin/Login.cshtml", model);
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, _accounts.Username) };
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
