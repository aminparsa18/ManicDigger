using ManicDigger.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ManicDigger.Extensions;

public static class WorkerInfrastructureExtensions
{
    /// <summary>
    /// Registers the simulation loop, chunk worker pools (lighting + tessellation),
    /// periodic task scheduler, and WorkerHost as singletons.
    ///
    /// Lighting pool:      workerCount=1  — sequential, eliminates lighting races
    /// Tessellation pool:  workerCount=N  — parallel geometry factory
    ///
    /// Usage:
    /// <code>
    /// services.AddWorkerInfrastructure()
    ///         .AddScheduledTask&lt;SaveGameTask&gt;()
    ///         .AddScheduledTask&lt;SeasonBroadcastTask&gt;();
    /// </code>
    /// </summary>
    public static IServiceCollection AddWorkerInfrastructure(
        this IServiceCollection services,
        int workerCount = 0,
        int chunkChannelCapacity = 512,
        TimeSpan simulationTickInterval = default)
    {
        // Registers IPublisher<T> / ISubscriber<T> for all event types.
        // Must come before any service that injects IPublisher or ISubscriber.
        services.AddMessagePipe();

        // ── Tessellation pool ─────────────────────────────────────────────────
        // Registered as both concrete type (WorkerHost needs StartAsync/StopAsync)
        // and IChunkWorkQueue (ChunkLightingDispatcher enqueues into it).
        services.AddSingleton<ChunkTessellationDispatcher>();
        services.AddSingleton<IChunkWorkDispatcher>(sp =>
            sp.GetRequiredService<ChunkTessellationDispatcher>());

        services.AddSingleton(sp => new ChunkWorkerPool(
            sp.GetRequiredService<IChunkWorkDispatcher>(),
            sp.GetRequiredService<ILogger<ChunkWorkerPool>>(),
            workerCount,
            chunkChannelCapacity));

        services.AddSingleton<IChunkWorkQueue>(sp =>
            sp.GetRequiredService<ChunkWorkerPool>());

        // ── Lighting pool ─────────────────────────────────────────────────────
        // workerCount=1 makes lighting sequential — the same ChunkWorkerPool
        // infrastructure, just constrained to one worker so lighting state is
        // never touched concurrently.
        services.AddSingleton(sp => new ChunkLightingDispatcher(
            sp.GetRequiredService<IChunkWorkQueue>(),
            sp.GetRequiredService<IVoxelMap>(),
            sp.GetRequiredService<IBlockRegistry>()));

        services.AddSingleton(sp => new ChunkLightingPool(
            sp.GetRequiredService<ChunkLightingDispatcher>(),
            sp.GetRequiredService<ILogger<ChunkWorkerPool>>(),
            workerCount: 1,
            channelCapacity: chunkChannelCapacity));

        services.AddSingleton<ILightingWorkQueue>(sp =>
            sp.GetRequiredService<ChunkLightingPool>());

        // ── Simulation loop ───────────────────────────────────────────────────
        services.AddSingleton(sp => new SimulationLoop(
            sp.GetRequiredService<ISimulationStep>(),
            sp.GetRequiredService<ILogger<SimulationLoop>>(),
            simulationTickInterval));

        // ── Periodic scheduler ────────────────────────────────────────────────
        services.AddSingleton<PeriodicTaskScheduler>();

        // ── WorkerHost — started manually from Connect() ──────────────────────
        services.AddSingleton<WorkerHost>();

        services.AddSingleton<ServerLifetime>();
        services.AddSingleton<ISimulationStep, ServerSimulationStep>();
        services.AddScheduledTask<ServerAutoRestartTask>();
        services.AddScheduledTask<SaveGameTask>();
        services.AddScheduledTask<SeasonBroadcastTask>();

        return services;
    }

    public static IServiceCollection AddScheduledTask<T>(this IServiceCollection services)
        where T : class, IScheduledTask
    {
        services.AddSingleton<IScheduledTask, T>();
        return services;
    }
}

/// <summary>Enqueue a LightingChunkWorkItem here from ModDrawTerrain.</summary>
public interface ILightingWorkQueue : IChunkWorkQueue { }

/// <summary>
/// Single-worker ChunkWorkerPool for the lighting stage.
/// Implements ILightingWorkQueue so the DI container resolves it unambiguously
/// alongside the tessellation ChunkWorkerPool that implements IChunkWorkQueue.
/// </summary>
public sealed class ChunkLightingPool(
    IChunkWorkDispatcher dispatcher,
    ILogger<ChunkWorkerPool> logger,
    int workerCount,
    int channelCapacity)
    : ChunkWorkerPool(dispatcher, logger, workerCount, channelCapacity), ILightingWorkQueue;