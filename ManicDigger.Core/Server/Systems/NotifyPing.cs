/// <summary>
/// Sends a ping packet to every connected client once per second and disconnects
/// any client that fails to respond within the timeout window.
/// Half-dropped connections (where the TCP socket appears open but the client is
/// unresponsive) are detected this way.
/// </summary>
public class ServerSystemNotifyPing : ServerSystem
{
    private readonly Timer pingTimer = new() { INTERVAL = 1, MaxDeltaTime = 5 };
    private readonly IGameService gameService;
    private readonly IGameExit gameExit;

    public ServerSystemNotifyPing(IGameService gameService, IGameExit gameExit)
    {
        this.gameService = gameService;
        this.gameExit = gameExit;
    }

    /// <inheritdoc/>
    protected override void OnUpdate(Server server, float dt)
    {
        pingTimer.Update(() =>
        {
            if (gameExit.Exit)
            {
                return;
            }

            var timedOut = new List<int>();

            foreach (var (clientId, client) in server.Clients)
            {
                if (!client.Ping.Send(gameService.TimeMillisecondsFromStart))
                {
                    if (client.Ping.CheckTimeout(gameService.TimeMillisecondsFromStart))
                    {
                        Console.WriteLine($"{clientId}: ping timeout. Disconnecting...");
                        timedOut.Add(clientId);
                    }
                }
                else
                {
                    server.SendPacket(clientId, ServerPackets.Ping());
                }
            }

            foreach (int clientId in timedOut)
            {
                server.KillPlayer(clientId);
            }
        });
    }
}