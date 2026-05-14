using MeinKraft;
using MeinKraft.Worker;

public static class WorkerServerExtensions
{
    /// <summary>
    /// Registers the simulation loop, periodic task scheduler, and WorkerHost as singletons.
    /// Usage:
    /// <code>
    /// services.AddWorkerInfrastructure()
    ///         .AddScheduledTask&lt;SaveGameTask&gt;()
    ///         .AddScheduledTask&lt;SeasonBroadcastTask&gt;();
    /// </code>
    /// </summary>
    public static IServiceCollection AddServerWorkerInfrastructure(
        this IServiceCollection services,
        TimeSpan simulationTickInterval = default)
    {
        // ── Simulation loop ───────────────────────────────────────────────────

        services.AddSingleton(sp => new SimulationLoop(
            sp.GetRequiredService<ISimulationStep>(),
            sp.GetRequiredService<ILogger<SimulationLoop>>(),
            simulationTickInterval));

        // ── Periodic scheduler ────────────────────────────────────────────────

        services.AddSingleton<PeriodicTaskScheduler>();

        // ── WorkerHost — started manually from Connect() ──────────────────────

        services.AddSingleton<ServerWorkerHost>();

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