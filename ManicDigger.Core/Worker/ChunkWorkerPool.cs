using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ManicDigger.Worker;

/// <summary>
/// Reads from <see cref="IChunkWorkQueue"/> using a configurable number of parallel
/// workers — one <see cref="Task"/> per worker, each running on the thread pool.
///
/// Items are dequeued in ascending <see cref="ChunkWorkItem.Priority"/> order
/// (lower value = nearer to player = processed first). Priority is supplied by
/// the enqueuer at submission time as squared chunk-space distance to the player.
///
/// The queue is unbounded. Backpressure is not needed because the dirty-flag claim
/// in <see cref="Chunk.TryClaimBaseLightDirty"/> ensures each chunk is enqueued
/// at most once per dirty cycle, bounding live queue depth to the number of
/// loaded chunks.
/// </summary>
public class ChunkWorkerPool : BackgroundService, IChunkWorkQueue
{
    // How many parallel workers process chunk jobs.
    // Default: leave one logical core free for the main/render thread.
    public static int DefaultWorkerCount =>
        Math.Max(1, Environment.ProcessorCount - 1);

    private readonly ConcurrentQueue<TerrainRendererRedraw> _results = new();
    private readonly PriorityQueue<ChunkWorkItem, int> _queue = new();
    private readonly object _queueLock = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly IChunkWorkDispatcher _dispatcher;
    private readonly IGameLogger _logger;
    private readonly int _workerCount;

    // Exposed so callers can monitor queue depth (HUD, diagnostics).
    public int PendingCount { get { lock (_queueLock) return _queue.Count; } }

    public ChunkWorkerPool(
        IChunkWorkDispatcher dispatcher,
        IGameLogger logger,
        int workerCount = 0,        // 0 = use DefaultWorkerCount
        int channelCapacity = 512)  // kept for API compatibility, no longer used
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _workerCount = workerCount > 0 ? workerCount : DefaultWorkerCount;

        _logger.Client.Information(
            "ChunkWorkerPool: {Workers} workers, priority queue (unbounded)",
            _workerCount);
    }

    // IChunkWorkQueue ──────────────────────────────────────────────────────────

    public ValueTask EnqueueAsync(ChunkWorkItem item, CancellationToken ct = default)
    {
        lock (_queueLock)
            _queue.Enqueue(item, item.Priority);

        _signal.Release();
        return ValueTask.CompletedTask;
    }

    public void EnqueueResult(TerrainRendererRedraw redraw)
        => _results.Enqueue(redraw);

    public bool TryDequeueResult(out TerrainRendererRedraw redraw)
        => _results.TryDequeue(out redraw);

    // BackgroundService ────────────────────────────────────────────────────────

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Task[] workers = new Task[_workerCount];
        for (int i = 0; i < _workerCount; i++)
        {
            int workerId = i;
            workers[i] = Task.Run(
                () => WorkerLoopAsync(workerId, stoppingToken), stoppingToken);
        }

        return Task.WhenAll(workers);
    }

    private async Task WorkerLoopAsync(int workerId, CancellationToken ct)
    {
        _logger.Client.Debug("Chunk worker {Id} started", workerId);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            ChunkWorkItem? item;
            lock (_queueLock)
                _queue.TryDequeue(out item, out _);

            if (item == null) continue;

            try
            {
                await _dispatcher.DispatchAsync(item, ct);
                item.Completion?.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                item.Completion?.TrySetCanceled(ct);
                break;
            }
            catch (Exception ex)
            {
                _logger.Client.Error(ex,
                    "Chunk worker {Id} failed on {Type} ({X},{Y},{Z})",
                    workerId, item.Type, item.ChunkX, item.ChunkY, item.ChunkZ);

                item.Completion?.TrySetException(ex);
                // Worker continues — one bad chunk should not kill the pool.
            }
        }

        _logger.Client.Debug("Chunk worker {Id} stopped", workerId);
    }
}