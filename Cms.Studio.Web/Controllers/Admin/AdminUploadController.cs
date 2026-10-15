using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cms.Studio.Web.Infrastructure;

namespace Cms.Studio.Web.Controllers.Admin;

/// <summary>
/// Image upload for the post editor (used by Vditor's upload button and drag &amp; paste).
/// Files land in wwwroot/uploads/{yyyy/MM}/{guid}.{ext} and are served as static content.
/// </summary>
[Authorize]
[Route("admin/upload")]
public class AdminUploadController : Controller
{
    private const long MaxBytes = 10 * 1024 * 1024;

    private readonly IWebHostEnvironment _env;

    public AdminUploadController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpPost("image")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Image(IFormFile? file)
    {
        if (file == null)
            return BadRequest(new { error = "No file received." });

        await using var stream = file.OpenReadStream();
        var (ok, url, error) = await UploadedImageStore.SaveAsync(_env, file.FileName, stream, file.Length);
        if (!ok)
            return BadRequest(new { error });

        // { url } — mapped to Vditor's response shape by the editor's format() hook.
        return Ok(new { url });
    }
}
