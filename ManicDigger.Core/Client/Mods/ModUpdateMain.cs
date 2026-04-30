public class ModUpdateMain : ModBase
{
    public ModUpdateMain()
    {
    }

    // Should use ReadWrite to be correct but that would be too slow
    public override void OnReadOnlyMainThread(IGame game, float dt)
    {
        game.Update(dt);
    }
}
