using Cms.Studio.Web.Infrastructure;

namespace Cms.Studio.Web.Infrastructure;

/// <summary>
/// One-shot startup job: generate the missing <c>.thumb.jpg</c> sidecars for images that were
/// uploaded before thumbnail support existed, so list pages can use small thumbs everywhere.
/// Runs off the request path and never blocks startup.
/// </summary>
public sealed class UploadThumbnailBackfillService : BackgroundService
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UploadThumbnailBackfillService> _logger;

    public UploadThumbnailBackfillService(IWebHostEnvironment env, ILogger<UploadThumbnailBackfillService> logger)
    {
        _env = env;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var uploadsRoot = Path.Combine(_env.WebRootPath ?? string.Empty, "uploads");
        if (!Directory.Exists(uploadsRoot))
            return;

        var created = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(uploadsRoot, "*", SearchOption.AllDirectories))
            {
                stoppingToken.ThrowIfCancellationRequested();

                if (!ImageExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    continue;

                var thumb = ImagePipeline.ThumbPathFor(file);
                if (File.Exists(thumb))
                    continue;

                try
                {
                    if (await ImagePipeline.TryCreateThumbAsync(file, stoppingToken))
                        created++;
                }
                catch
                {
                    // A single corrupt image must not stop the backfill.
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (created > 0)
            _logger.LogInformation("Generated {Count} upload thumbnails", created);
    }
}
