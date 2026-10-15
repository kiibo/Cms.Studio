using System.Threading.Channels;
using Cms.Studio.Core.Data;
using Cms.Studio.Core.Domain;

namespace Cms.Studio.Web.Infrastructure;

/// <summary>
/// Bounded in-memory queue between the request pipeline and the visit-log writer, so page
/// responses never wait for the analytics INSERT. Under pressure the oldest rows are dropped
/// instead of growing memory without bound.
/// </summary>
public sealed class VisitQueue
{
    private readonly Channel<VisitRecord> _channel = Channel.CreateBounded<VisitRecord>(
        new BoundedChannelOptions(2000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryEnqueue(VisitRecord record) => _channel.Writer.TryWrite(record);

    public ChannelReader<VisitRecord> Reader => _channel.Reader;
}

/// <summary>Flushes queued visit records to the database in small batches.</summary>
public sealed class VisitWriterService : BackgroundService
{
    private const int BatchSize = 64;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

    private readonly VisitQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;

    public VisitWriterService(VisitQueue queue, IServiceScopeFactory scopeFactory)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<VisitRecord>(BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                batch.Clear();

                // Wait for the first row, then drain whatever else is ready (bounded by time).
                using (var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
                {
                    wait.CancelAfter(FlushInterval);
                    try
                    {
                        await _queue.Reader.WaitToReadAsync(wait.Token);
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                    {
                        // idle flush interval — fall through and loop
                    }
                }

                while (batch.Count < BatchSize && _queue.Reader.TryRead(out var record))
                    batch.Add(record);

                if (batch.Count == 0)
                    continue;

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
                db.VisitRecords.AddRange(batch);
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // The visit log must never take the site down; drop the batch on failure.
                batch.Clear();
                await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
            }
        }
    }
}
