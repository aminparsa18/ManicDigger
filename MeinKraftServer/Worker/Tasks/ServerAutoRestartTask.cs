namespace MeinKraft.Worker;

public sealed class ServerAutoRestartTask : IScheduledTask
{
    public TimeSpan Interval => TimeSpan.FromMinutes(10); // check every 10 min, no need to check every second

    private readonly ServerGameService _server;
    private readonly IServerConfig _config;
    private readonly DateTimeOffset _startedAt;

    public ServerAutoRestartTask(ServerGameService server, IServerConfig config)
    {
        _server = server;
        _config = config;
        _startedAt = DateTimeOffset.UtcNow;
    }

    public Task ExecuteAsync(CancellationToken ct)
    {
        if (_config.AutoRestartCycle > 0 &&
            (DateTimeOffset.UtcNow - _startedAt).TotalHours >= _config.AutoRestartCycle)
        {
            _server.Restart();
        }

        return Task.CompletedTask;
    }
}