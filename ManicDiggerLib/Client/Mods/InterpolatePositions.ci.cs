/// <summary>
/// Interpolates network entity positions each frame for smooth remote player movement.
/// </summary>
public class ModInterpolatePositions : ModBase
{
    private const int ExtrapolationTimeMs = 300;
    private const int MinDelayMs = 100;

    private readonly IGameClient game;
    private readonly IGamePlatform platform;

    public ModInterpolatePositions(IGameClient game, IGamePlatform platform)
    {
        this.game = game;
        this.platform = platform;
    }

    public override void OnNewFrame(float args)
    {
        InterpolatePositions(args);
    }

    internal void InterpolatePositions(float dt)
    {
        for (int i = 0; i < game.Entities.Count; i++)
        {
            Entity e = game.Entities[i];
            if (e?.networkPosition == null) continue;
            if (i == game.LocalPlayerId) continue;
            if (!e.networkPosition.PositionLoaded) continue;

            e.playerDrawInfo ??= new PlayerDrawInfo();
            EnsureInterpolation(e);

            e.playerDrawInfo.interpolation.DELAYMILLISECONDS =
                Math.Max(MinDelayMs, game.ServerInfo.ServerPing.RoundtripMilliseconds);

            UpdateInterpolation(i, e);
        }
    }

    /// <summary>Initializes the network interpolation state for an entity if not already set up.</summary>
    private void EnsureInterpolation(Entity e)
    {
        if (e.playerDrawInfo.interpolation != null) return;

        e.playerDrawInfo.interpolation = new NetworkInterpolation
        {
            req = new PlayerInterpolate { platform = platform },
            DELAYMILLISECONDS = 500,
            EXTRAPOLATE = false,
            EXTRAPOLATION_TIMEMILLISECONDS = ExtrapolationTimeMs
        };
    }

    private void UpdateInterpolation(int entityId, Entity e)
    {
        PlayerDrawInfo info = e.playerDrawInfo;
        EntityPosition_ net = e.networkPosition;

        float netX = net.x, netY = net.y, netZ = net.z;

        // Feed a new state packet when network position or rotation has changed
        bool posChanged = !(netX == info.lastnetworkposX && netY == info.lastnetworkposY && netZ == info.lastnetworkposZ);
        bool rotChanged = net.rotx != info.lastnetworkrotx || net.roty != info.lastnetworkroty || net.rotz != info.lastnetworkrotz;

        if (posChanged || rotChanged)
        {
            info.interpolation.AddNetworkPacket(new PlayerInterpolationState
            {
                positionX = netX,
                positionY = netY,
                positionZ = netZ,
                rotx = net.rotx,
                roty = net.roty,
                rotz = net.rotz
            }, game.TotalTimeMilliseconds);
        }

        PlayerInterpolationState cur =
            platform.CastToPlayerInterpolationState(info.interpolation.InterpolatedState(game.TotalTimeMilliseconds))
            ?? new PlayerInterpolationState();

        // Bypass interpolation if the game world is controlling this entity's position
        if (Game.EnablePlayerUpdatePositionContainsKey(entityId) && !Game.EnablePlayerUpdatePosition(entityId))
        {
            cur.positionX = net.x;
            cur.positionY = net.y;
            cur.positionZ = net.z;
        }

        info.Velocity = new (
            cur.positionX - info.lastcurposX,
            cur.positionY - info.lastcurposY,
            cur.positionZ - info.lastcurposZ
        );
        info.moves = !(cur.positionX == info.lastcurposX && cur.positionY == info.lastcurposY && cur.positionZ == info.lastcurposZ);
        info.lastcurposX = cur.positionX;
        info.lastcurposY = cur.positionY;
        info.lastcurposZ = cur.positionZ;
        info.lastnetworkposX = netX; info.lastnetworkposY = netY; info.lastnetworkposZ = netZ;
        info.lastnetworkrotx = net.rotx; info.lastnetworkroty = net.roty; info.lastnetworkrotz = net.rotz;

        e.position.x = cur.positionX;
        e.position.y = cur.positionY;
        e.position.z = cur.positionZ;
        e.position.rotx = cur.rotx;
        e.position.roty = cur.roty;
        e.position.rotz = cur.rotz;
    }
}