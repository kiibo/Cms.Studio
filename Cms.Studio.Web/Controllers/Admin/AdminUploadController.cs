using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Studio.Web.Controllers.Admin;

/// <summary>
/// Image upload for the post editor (used by Vditor's upload button and drag &amp; paste).
/// Files land in wwwroot/uploads/{yyyy/MM}/{guid}.{ext} and are served as static content.
/// </summary>
[Authorize]
[Route("admin/upload")]
public class AdminUploadController : Controller
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif", ".bmp"
    };

    private const long MaxBytes = 10 * 1024 * 1024;

    private readonly IWebHostEnvironment _env;

    public AdminUploadController(IWebHostEnvironment env)
    {
        _env = env;
    }

    [HttpPost("image")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> Image(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file received." });
        if (file.Length > MaxBytes)
            return BadRequest(new { error = "Image is larger than 10 MB." });

        var ext = Path.GetExtension(file.FileName ?? string.Empty);
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { error = $"Unsupported image type '{ext}'." });

        // Magic-byte sniff: never trust the extension alone.
        var header = new byte[12];
        await using (var s = file.OpenReadStream())
        {
            _ = await s.ReadAsync(header);
        }
        if (!IsImageHeader(header, ext))
            return BadRequest(new { error = "File content does not look like a valid image." });

        var relative = $"/uploads/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var absolute = Path.Combine(_env.WebRootPath,
            relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        await using (var target = System.IO.File.Create(absolute))
        {
            await file.CopyToAsync(target);
        }

        // { url } — mapped to Vditor's response shape by the editor's format() hook.
        return Ok(new { url = relative });
    }

    private static bool IsImageHeader(byte[] h, string ext)
    {
        // JPEG
        if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            return h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;
        // PNG
        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            return h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47;
        // GIF
        if (ext.Equals(".gif", StringComparison.OrdinalIgnoreCase))
            return h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46;
        // BMP
        if (ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
            return h[0] == 0x42 && h[1] == 0x4D;
        // WEBP: "RIFF"...."WEBP"
        if (ext.Equals(".webp", StringComparison.OrdinalIgnoreCase))
            return h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
                   && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50;
        // AVIF: "....ftypavif"
        if (ext.Equals(".avif", StringComparison.OrdinalIgnoreCase))
            return h[4] == 0x66 && h[5] == 0x74 && h[6] == 0x79 && h[7] == 0x70;
        return false;
    }
}
