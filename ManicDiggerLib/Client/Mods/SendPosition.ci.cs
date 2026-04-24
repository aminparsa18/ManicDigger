/// <summary>
/// Sends the local player's position and orientation to the server at most every 100ms.
/// </summary>
public class ModSendPosition : ModBase
{
    private const int SendIntervalMs = 100;
    private readonly IGameClient game;
    private readonly IGamePlatform platform;

    public ModSendPosition(IGameClient game, IGamePlatform platform)
    {
        this.game = game;
        this.platform = platform;
    }

    public override void OnNewFrame(float args)
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