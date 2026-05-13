namespace MeinKraft.Worker;

public sealed class ServerSimulationStep : ISimulationStep
{
    private readonly ServerGameService _server;
    private readonly ServerLifetime _lifetime;

    public ServerSimulationStep(
        ServerGameService server,
        ServerLifetime lifetime)
    {
        _server = server;
        _lifetime = lifetime;
    }

    public void Tick(float dt)
    {
        // Exit — same logic as before, now cleanly isolated in one place.
        //if (_gameExit.Exit)
        //{
        //    _server.Stop();
        //    _gameExit.Exit = false;
        //    _lifetime.SignalStop();  // cancels the CancellationToken → SimulationLoop stops
        //    return;
        //}

        //// SinglePlayerServerExit — same as before.
        //if (_singlePlayerService.SinglePlayerServerExit)
        //{
        //    _server.Exit();
        //    _singlePlayerService.SinglePlayerServerExit = false;
        //    return;
        //}

        _server.Process(dt);
    }
}