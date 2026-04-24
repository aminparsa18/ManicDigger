/// <summary>
/// Clears draw info for players who have not sent a network update in over 2 seconds.
/// </summary>
public class ModClearInactivePlayersDrawInfo : ModBase
{
    private const float InactiveThresholdSeconds = 2f;

    private readonly IGameClient _game;
    private readonly IGamePlatform _platform;

    public ModClearInactivePlayersDrawInfo(IGameClient game, IGamePlatform platform)
    {
        _game = game;
        _platform = platform;
    }

    public override void OnNewFrameFixed(Game game, float args)
    {
        int now = _platform.TimeMillisecondsFromStart;

        for (int i = 0; i < _game.Entities.Count; i++)
        {
            Entity p = _game.Entities[i];
            if (p?.playerDrawInfo == null || p.networkPosition == null) continue;

            float secondsSinceUpdate = (now - p.networkPosition.LastUpdateMilliseconds) / 1000f;
            if (secondsSinceUpdate > InactiveThresholdSeconds)
            {
                p.playerDrawInfo = null;
                p.networkPosition.PositionLoaded = false;
            }
        }
    }
}