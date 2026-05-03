using ManicDigger;

public class ModBootstrapper
{
    public ModBootstrapper(IEnumerable<IMod> mods, IServerModManager modManager, IModEvents modEvents)
    {
        foreach (IMod mod in mods)
            mod.Start(modManager, modEvents);
    }
}