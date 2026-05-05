using Microsoft.Extensions.Logging;

namespace ManicDigger.Worker;

/// <summary>
/// Manages the lifetime of the simulation loop, chunk worker pool, and periodic
/// task scheduler for a single game session.
///
/// Registered as a singleton. Nothing starts at application launch.
/// Call <see cref="StartAsync"/> from <c>Connect()</c> when entering the game screen.
/// Call <see cref="StopAsync"/> on exit — or let <see cref="IGameExit"/> trigger it.
/// </summary>
public sealed class WorkerHost : IAsyncDisposable
{
    private readonly SimulationLoop _simulationLoop;
    private readonly ChunkWorkerPool _chunkWorkerPool;
    private readonly PeriodicTaskScheduler _periodicScheduler;
    private readonly ISinglePlayerService _singlePlayerService;
    private readonly ServerLifetime _lifetime;
    private readonly ILogger<WorkerHost> _logger;

    private Task? _allWorkers;
    private bool _started;

    public WorkerHost(
        SimulationLoop simulationLoop,
        ChunkWorkerPool chunkWorkerPool,
        PeriodicTaskScheduler periodicScheduler,
        ISinglePlayerService singlePlayerService,
        ServerLifetime lifetime,
        ILogger<WorkerHost> logger)
    {
        _simulationLoop = simulationLoop;
        _chunkWorkerPool = chunkWorkerPool;
        _periodicScheduler = periodicScheduler;
        _singlePlayerService = singlePlayerService;
        _lifetime = lifetime;
        _logger = logger;
    }

    // ── Start ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts all workers for this game session.
    /// Safe to call only once per session — guard with <see cref="_started"/>.
    /// </summary>
    public async Task StartAsync()
    {
        if (_started)
        {
            _logger.LogWarning("WorkerHost.StartAsync called twice — ignoring");
            return;
        }

        _started = true;
        CancellationToken ct = _lifetime.Token;

        _logger.LogInformation("WorkerHost: starting workers");

        // BackgroundService.StartAsync launches the internal Task and returns
        // quickly — it does NOT block until the work is done.
        await _simulationLoop.StartAsync(ct);
        await _chunkWorkerPool.StartAsync(ct);
        await _periodicScheduler.StartAsync(ct);

        // Collect the running tasks so StopAsync can await clean shutdown.
        _allWorkers = Task.WhenAll(
            _simulationLoop.ExecuteTask ?? Task.CompletedTask,
            _chunkWorkerPool.ExecuteTask ?? Task.CompletedTask,
            _periodicScheduler.ExecuteTask ?? Task.CompletedTask);

        // Signal to any code still polling this flag that the server is live.
        _singlePlayerService.SinglePlayerServerLoaded = true;

        _logger.LogInformation("WorkerHost: all workers running");
    }

    // ── Stop ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cancels all workers and waits for them to drain cleanly.
    /// Idempotent — safe to call even if <see cref="StartAsync"/> was never called.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_started)
            return;

        _logger.LogInformation("WorkerHost: stopping workers");

        // If stop was not already signalled by ServerSimulationStep, signal now.
        _lifetime.SignalStop();

        // Give BackgroundService implementations a chance to drain.
        await _simulationLoop.StopAsync(CancellationToken.None);
        await _chunkWorkerPool.StopAsync(CancellationToken.None);
        await _periodicScheduler.StopAsync(CancellationToken.None);

        if (_allWorkers is not null)
        {
            try 
            { 
                await _allWorkers;
            }
            catch (OperationCanceledException) { /* expected on cancel */ }
        }

        _started = false;
        _singlePlayerService.SinglePlayerServerLoaded = false;

        _logger.LogInformation("WorkerHost: all workers stopped");
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}