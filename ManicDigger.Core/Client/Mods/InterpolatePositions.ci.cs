/// <summary>
/// Interpolates network entity positions each frame for smooth remote player movement.
/// </summary>
public class ModInterpolatePositions : ModBase
{
    private const int ExtrapolationTimeMs = 300;
    private const int MinDelayMs = 100;


    public ModInterpolatePositions(IGame game) : base(game)
    {
    }

    public override void OnNewFrame(float dt)
    {
        InterpolatePositions();
    }

    internal void InterpolatePositions()
    {
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity e = Game.Entities[i];
            if (e?.networkPosition == null) continue;
            if (i == Game.LocalPlayerId) continue;
            if (!e.networkPosition.PositionLoaded) continue;

            e.playerDrawInfo ??= new PlayerDrawInfo();
            EnsureInterpolation(e);

            e.playerDrawInfo.interpolation.DelayMilliseconds =
                Math.Max(MinDelayMs, Game.ServerInfo.ServerPing.RoundtripMilliseconds);

            UpdateInterpolation(e);
        }
    }

    /// <summary>
    /// Initialises the network interpolation state for an entity if not already set up.
    /// </summary>
    private static void EnsureInterpolation(Entity e)
    {
        if (e.playerDrawInfo.interpolation != null) return;

        e.playerDrawInfo.interpolation = new NetworkInterpolation
        {
            Interpolator = new PlayerInterpolate(),
            DelayMilliseconds = 500,
            Extrapolate = false,
            ExtrapolationTimeMilliseconds = ExtrapolationTimeMs
        };
    }

    private void UpdateInterpolation(Entity e)
    {
        PlayerDrawInfo info = e.playerDrawInfo;
        EntityPosition_ net = e.networkPosition;

        float netX = net.x, netY = net.y, netZ = net.z;

        // Feed a new state packet when network position or rotation has changed.
        bool posChanged = !(netX == info.lastnetworkposX
                         && netY == info.lastnetworkposY
                         && netZ == info.lastnetworkposZ);
        bool rotChanged = net.rotx != info.lastnetworkrotx
                       || net.roty != info.lastnetworkroty
                       || net.rotz != info.lastnetworkrotz;

        if (posChanged || rotChanged)
        {
            // Store rotations in degrees to avoid per-frame radian conversion in Interpolate().
            info.interpolation.AddNetworkPacket(new PlayerInterpolationState
            {
                positionX = netX,
                positionY = netY,
                positionZ = netZ,
                rotx = float.RadiansToDegrees(net.rotx),
                roty = float.RadiansToDegrees(net.roty),
                rotz = float.RadiansToDegrees(net.rotz),
            }, Game.TotalTimeMilliseconds);
        }

        PlayerInterpolationState cur =
          (PlayerInterpolationState)info.interpolation.InterpolatedState(Game.TotalTimeMilliseconds)
            ?? new PlayerInterpolationState();

        info.Velocity = new(
            cur.positionX - info.lastcurposX,
            cur.positionY - info.lastcurposY,
            cur.positionZ - info.lastcurposZ);

        info.moves = !(cur.positionX == info.lastcurposX
                    && cur.positionY == info.lastcurposY
                    && cur.positionZ == info.lastcurposZ);

        info.lastcurposX = cur.positionX;
        info.lastcurposY = cur.positionY;
        info.lastcurposZ = cur.positionZ;
        info.lastnetworkposX = netX;
        info.lastnetworkposY = netY;
        info.lastnetworkposZ = netZ;
        info.lastnetworkrotx = net.rotx;
        info.lastnetworkroty = net.roty;
        info.lastnetworkrotz = net.rotz;

        e.position.x = cur.positionX;
        e.position.y = cur.positionY;
        e.position.z = cur.positionZ;
        // Convert degrees back to radians only once, here at the apply site.
        e.position.rotx = float.DegreesToRadians(cur.rotx);
        e.position.roty = float.DegreesToRadians(cur.roty);
        e.position.rotz = float.DegreesToRadians(cur.rotz);
    }
}

/// <summary>
/// Interpolates <see cref="PlayerInterpolationState"/> snapshots.
/// Rotations are stored and interpolated in degrees; conversion to/from
/// radians happens only at the network-receive and position-apply sites,
/// not on every interpolation call.
/// </summary>
public class PlayerInterpolate : IInterpolation
{
    public IInterpolatedObject Interpolate(IInterpolatedObject a, IInterpolatedObject b, float progress)
    {
        PlayerInterpolationState aa = (PlayerInterpolationState)a;
        PlayerInterpolationState bb = (PlayerInterpolationState)b;

        return new PlayerInterpolationState
        {
            positionX = aa.positionX + (bb.positionX - aa.positionX) * progress,
            positionY = aa.positionY + (bb.positionY - aa.positionY) * progress,
            positionZ = aa.positionZ + (bb.positionZ - aa.positionZ) * progress,
            // Rotations are already in degrees — no conversion needed here.
            rotx = AngleInterpolation.InterpolateAngle360(aa.rotx, bb.rotx, progress),
            roty = AngleInterpolation.InterpolateAngle360(aa.roty, bb.roty, progress),
            rotz = AngleInterpolation.InterpolateAngle360(aa.rotz, bb.rotz, progress),
        };
    }
}