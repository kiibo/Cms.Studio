using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Cms.Studio.Web.Infrastructure;

/// <summary>
/// Upload image processing: downscales oversized photos and generates small thumbnails so
/// list pages never download full-size originals. JPEG / PNG / WebP are re-encoded (capped at
/// 1920 px wide); GIF / BMP keep their original bytes (animated GIFs must not be flattened)
/// but still get a thumbnail; AVIF is stored as-is (no decoder available).
/// </summary>
public static class ImagePipeline
{
    /// <summary>Longest edge accepted before the photo is downscaled on upload.</summary>
    public const int MaxWidth = 1920;

    /// <summary>Thumbnail width used by feed cards / hero side cards.</summary>
    public const int ThumbWidth = 480;

    public const string ThumbSuffix = ".thumb.jpg";

    private static readonly HashSet<string> ReEncodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> ThumbnailExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
    };

    public static bool CanReEncode(string ext) => ReEncodeExtensions.Contains(ext);

    public static bool CanThumbnail(string ext) => ThumbnailExtensions.Contains(ext);

    /// <summary>Path of the sidecar thumbnail for an upload, e.g. "a.jpg" → "a.thumb.jpg".</summary>
    public static string ThumbPathFor(string absoluteImagePath) =>
        absoluteImagePath[..^Path.GetExtension(absoluteImagePath).Length] + ThumbSuffix;

    /// <summary>
    /// Writes the (possibly downscaled) original plus its thumbnail.
    /// Returns false when the original is kept untouched but no thumbnail could be produced.
    /// </summary>
    public static async Task<bool> SaveAsync(Stream source, string extension, string targetPath, CancellationToken ct = default)
    {
        if (CanThumbnail(extension))
        {
            using var image = await Image.LoadAsync(source, ct);
            image.Mutate(x => x.AutoOrient());

            if (CanReEncode(extension))
            {
                if (image.Width > MaxWidth)
                    image.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(MaxWidth, 0) }));
                await SaveOriginalAsync(image, extension, targetPath, ct);
            }
            else
            {
                // GIF / BMP keep their original bytes (animated GIFs must not be flattened).
                source.Position = 0;
                await using var copy = File.Create(targetPath);
                await source.CopyToAsync(copy, ct);
            }

            using var thumb = image.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(ThumbWidth, 0) }));
            await thumb.SaveAsJpegAsync(ThumbPathFor(targetPath), new JpegEncoder { Quality = 80 }, ct);
            return true;
        }

        // AVIF & friends: store the bytes untouched, no thumbnail.
        source.Position = 0;
        await using var target = File.Create(targetPath);
        await source.CopyToAsync(target, ct);
        return false;
    }

    /// <summary>Generates only the thumbnail (used to backfill existing uploads).</summary>
    public static async Task<bool> TryCreateThumbAsync(string imagePath, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(imagePath);
        if (!CanThumbnail(ext))
            return false;

        using var image = await Image.LoadAsync(imagePath, ct);
        image.Mutate(x => x.AutoOrient());
        using var thumb = image.Clone(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(ThumbWidth, 0) }));
        await thumb.SaveAsJpegAsync(ThumbPathFor(imagePath), new JpegEncoder { Quality = 80 }, ct);
        return true;
    }

    private static async Task SaveOriginalAsync(Image image, string extension, string targetPath, CancellationToken ct)
    {
        switch (extension.ToLowerInvariant())
        {
            case ".png":
                await image.SaveAsPngAsync(targetPath, new PngEncoder { CompressionLevel = PngCompressionLevel.BestSpeed }, ct);
                break;
            case ".webp":
                await image.SaveAsWebpAsync(targetPath, new WebpEncoder { Quality = 85 }, ct);
                break;
            default: // .jpg / .jpeg
                await image.SaveAsJpegAsync(targetPath, new JpegEncoder { Quality = 85 }, ct);
                break;
        }
    }
}
