using static ManicDigger.Mods.ModNetworkProcess;

public class ModDiagLog : ModBase
{
    private readonly IGame game;
    private readonly float logTimer = 0f;
    private const float LogInterval = 5f;

    private readonly long lastGen0 = 0, lastGen1 = 0, lastGen2 = 0;
    private readonly long lastTotalMemory = 0;

    public ModDiagLog(IGame game)
    {
        this.game = game;
        DiagLog.Write("ModDiagLog started");
    }

   
}