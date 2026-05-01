/// <summary>
/// Client mod that routes entity lifecycle and position packets to their handlers
/// and drives networked-entity interpolation each frame.
/// </summary>
public class ModNetworkEntity : ModBase
{
    private readonly ClientPacketHandlerEntitySpawn _spawn;
    private readonly ClientPacketHandlerEntityPosition _position;
    private readonly ClientPacketHandlerEntityDespawn _despawn;

    /// <summary>True once the three packet handlers have been registered.</summary>
    private bool _handlersRegistered;

    public ModNetworkEntity(IGameService gameService, IVoxelMap voxelMap)
    {
        _spawn = new ClientPacketHandlerEntitySpawn(gameService, voxelMap);
        _position = new ClientPacketHandlerEntityPosition(gameService);
        _despawn = new ClientPacketHandlerEntityDespawn(gameService);
    }

    public override void OnNewFrame(IGame game, float args)
    {
        // Register once — previously wrote to the handler dictionary every frame.
        if (!_handlersRegistered)
        {
            game.PacketHandlers[(int)Packet_ServerIdEnum.EntitySpawn] = _spawn;
            game.PacketHandlers[(int)Packet_ServerIdEnum.EntityPosition] = _position;
            game.PacketHandlers[(int)Packet_ServerIdEnum.EntityDespawn] = _despawn;
            _handlersRegistered = true;
        }
    }
}