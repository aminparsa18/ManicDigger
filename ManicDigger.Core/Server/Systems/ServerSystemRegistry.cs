using ManicDigger;

public class ServerSystemRegistry
{
    public List<ServerSystem> Systems { get; }

    public ServerSystemRegistry(
        ServerSystemLoadFirst loadFirst,
        ServerSystemLoadConfig loadConfig,
        ServerSystemHeartbeat heartbeat,
        ServerSystemHttpServer httpServer,
        ServerSystemUnloadUnusedChunks unloadChunks,
        ServerSystemNotifyMap notifyMap,
        ServerSystemNotifyPing notifyPing,
        ServerSystemChunksSimulation chunksSimulation,
        ServerSystemBanList banList,
        ServerSystemModLoader modLoader,
        ServerSystemLoadServerClient loadServerClient,
        ServerSystemNotifyEntities notifyEntities,
        ServerSystemLoadLast loadLast)
    {
        Systems =
        [
            loadFirst,
            loadConfig,
            heartbeat,
            httpServer,
            unloadChunks,
            notifyMap,
            notifyPing,
            chunksSimulation,
            banList,
            modLoader,
            loadServerClient,
            notifyEntities,
            loadLast,
        ];
    }
}