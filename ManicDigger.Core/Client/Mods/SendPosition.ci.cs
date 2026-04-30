/// <summary>
/// Sends the local player's position and orientation to the server at most every 100ms.
/// </summary>
public class ModSendPosition : ModBase
{
    private const int SendIntervalMs = 100;
    private readonly IGameService platform;

    public ModSendPosition(IGameService platform)
    {
        this.platform = platform;
    }

    public override void OnNewFrame(IGame game, float args)
    {
        if (!game.Spawned) return;
        if (platform.TimeMillisecondsFromStart - game.LastPositionSentMilliseconds <= SendIntervalMs) return;

        game.LastPositionSentMilliseconds = platform.TimeMillisecondsFromStart;

        var pos = game.Player.position;
        game.SendPacketClient(ClientPackets.PositionAndOrientation(
            game.LocalPlayerId,
            pos.x, pos.y, pos.z,
            pos.rotx, pos.roty, pos.rotz,
            game.LocalStance));
    }
}