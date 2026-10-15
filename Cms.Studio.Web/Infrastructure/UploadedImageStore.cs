using Microsoft.AspNetCore.StaticFiles;

namespace Cms.Studio.Web.Infrastructure;

/// <summary>
/// Shared save pipeline for uploaded images (admin editor + machine API):
/// extension allow-list, magic-byte sniffing, GUID file names under /uploads/yyyy/MM/.
/// Never trusts the file name alone — SVG/HTML are rejected so uploads cannot carry scripts.
/// </summary>
public static class UploadedImageStore
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif", ".bmp"
    };

    private const long MaxBytes = 10 * 1024 * 1024;

    public static bool IsAllowedExtension(string? extension) =>
        !string.IsNullOrEmpty(extension) && AllowedExtensions.Contains(extension);

    /// <summary>Validates and stores an image. Returns the public relative URL, or an error message.</summary>
    public static async Task<(bool Ok, string? Url, string? Error)> SaveAsync(
        IWebHostEnvironment env, string? fileName, Stream source, long length, CancellationToken ct = default)
    {
        if (length == 0)
            return (false, null, "No file received.");
        if (length > MaxBytes)
            return (false, null, "Image is larger than 10 MB.");
        if (string.IsNullOrWhiteSpace(fileName))
            return (false, null, "File name is empty.");

        var ext = Path.GetExtension(fileName);
        if (!IsAllowedExtension(ext))
            return (false, null, $"Unsupported image type '{ext}'.");

        // Magic-byte sniff: never trust the extension alone.
        var header = new byte[12];
        var read = 0;
        while (read < header.Length)
        {
            var n = await source.ReadAsync(header.AsMemory(read), ct);
            if (n == 0)
                break;
            read += n;
        }
        if (!HasValidImageHeader(header, ext))
            return (false, null, "File content does not look like a valid image.");

        var webRoot = env.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
            return (false, null, "WebRoot path is not configured.");

        var relative = $"/uploads/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var absolute = Path.Combine(webRoot, relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(absolute);
        if (string.IsNullOrEmpty(directory))
            return (false, null, "Invalid directory path.");

        Directory.CreateDirectory(directory);

        source.Position = 0;
        await using (var target = File.Create(absolute))
        {
            await source.CopyToAsync(target, ct);
        }

        return (true, relative, null);
    }

    /// <summary>Header check for the supported image formats (shared by admin + API uploads).</summary>
    public static bool HasValidImageHeader(byte[] h, string ext)
    {
        if (h.Length < 12)
            return false;
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
