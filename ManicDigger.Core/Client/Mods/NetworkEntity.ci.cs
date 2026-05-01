/// <summary>
/// Client mod that routes entity lifecycle and position packets to their handlers
/// and drives networked-entity interpolation each frame.
/// </summary>
public class ModNetworkEntity(IGameService gameService, IVoxelMap voxelMap, IGame game, IBlockTypeRegistry blockTypeRegistry) : ModBase(game)
{
    private readonly ClientPacketHandlerEntitySpawn _spawn = new ClientPacketHandlerEntitySpawn(gameService, voxelMap, blockTypeRegistry, game);
    private readonly ClientPacketHandlerEntityPosition _position = new ClientPacketHandlerEntityPosition(gameService, game);
    private readonly ClientPacketHandlerEntityDespawn _despawn = new ClientPacketHandlerEntityDespawn(gameService, game);

    /// <summary>True once the three packet handlers have been registered.</summary>
    private bool _handlersRegistered;

    public override void OnNewFrame(float args)
    {
        // Register once — previously wrote to the handler dictionary every frame.
        if (!_handlersRegistered)
        {
            Game.PacketHandlers[(int)Packet_ServerIdEnum.EntitySpawn] = _spawn;
            Game.PacketHandlers[(int)Packet_ServerIdEnum.EntityPosition] = _position;
            Game.PacketHandlers[(int)Packet_ServerIdEnum.EntityDespawn] = _despawn;
            _handlersRegistered = true;
        }
    }
}