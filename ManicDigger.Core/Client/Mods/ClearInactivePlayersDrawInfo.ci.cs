/// <summary>
/// Clears draw info for players who have not sent a network update in over 2 seconds.
/// </summary>
public class ModClearInactivePlayersDrawInfo : ModBase
{
    private const float InactiveThresholdSeconds = 2f;

    private readonly IGame _game;
    private readonly IGameService _platform;

    public ModClearInactivePlayersDrawInfo(IGameService platform, IGame game) : base(game)
    {
        _platform = platform;
        _game = game;
    }

    public override void OnNewFrameFixed(float args)
    {
        int now = _platform.TimeMillisecondsFromStart;

        for (int i = 0; i < _game.Entities.Count; i++)
        {
            Entity p = _game.Entities[i];
            if (p?.playerDrawInfo == null || p.networkPosition == null)
            {
                continue;
            }

            float secondsSinceUpdate = (now - p.networkPosition.LastUpdateMilliseconds) / 1000f;
            if (secondsSinceUpdate > InactiveThresholdSeconds)
            {
                p.playerDrawInfo = null;
                p.networkPosition.PositionLoaded = false;
            }
        }
    }
}