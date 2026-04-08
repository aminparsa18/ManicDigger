/// <summary>
/// Clears draw info for players who have not sent a network update in over 2 seconds.
/// </summary>
public class ModClearInactivePlayersDrawInfo : ModBase
{
    private const int MaxPlayers = 64;
    private const float InactiveThresholdSeconds = 2f;

    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        int now = game.platform.TimeMillisecondsFromStart();

        for (int i = 0; i < MaxPlayers; i++)
        {
            Entity p = game.entities[i];
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