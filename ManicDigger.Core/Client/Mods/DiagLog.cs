using static ManicDigger.Mods.ModNetworkProcess;

public class ModDiagLog : ModBase
{
    public ModDiagLog(IGame game) : base(game)
    {
        DiagLog.Write("ModDiagLog started");
    }
}