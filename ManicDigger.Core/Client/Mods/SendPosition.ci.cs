/// <summary>
/// Sends the local player's position and orientation to the server at most every 100ms.
/// </summary>
public class ModSendPosition : ModBase
{
    private const int SendIntervalMs = 100;
    private readonly IGameService platform;

    public ModSendPosition(IGameService platform, IGame game) : base(game)
    {
        this.platform = platform;
    }

    public override void OnNewFrame(float args)
    {
        if (!Game.Spawned)
        {
            return;
        }

        if (platform.TimeMillisecondsFromStart - Game.LastPositionSentMilliseconds <= SendIntervalMs)
        {
            return;
        }

        Game.LastPositionSentMilliseconds = platform.TimeMillisecondsFromStart;

        var pos = Game.Player.position;
        Game.SendPacketClient(ClientPackets.PositionAndOrientation(
            Game.LocalPlayerId,
            pos.x, pos.y, pos.z,
            pos.rotx, pos.roty, pos.rotz,
            Game.LocalStance));
    }
}