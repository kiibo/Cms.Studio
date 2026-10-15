using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cms.Studio.Core.Services;
using Cms.Studio.Web.Infrastructure;

namespace Cms.Studio.Web.Controllers.Admin;

/// <summary>
/// Admin reference page for the machine API (System → API): the seven endpoints,
/// authentication, request/response structures and copy-paste examples for AI agents.
/// </summary>
[Authorize]
[Route("admin/api")]
public class AdminApiController : Controller
{
    private readonly ApiTokenService _apiTokens;
    private readonly SettingService _settings;

    public AdminApiController(ApiTokenService apiTokens, SettingService settings)
    {
        _apiTokens = apiTokens;
        _settings = settings;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.ApiToken = await _apiTokens.GetOrCreateAsync();

        var baseUrl = (await _settings.GetBaseUrlAsync()).TrimEnd('/');
        ViewBag.BaseUrl = string.IsNullOrEmpty(baseUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : baseUrl;

        return View("~/Views/Admin/ApiDocs.cshtml");
    }
}
