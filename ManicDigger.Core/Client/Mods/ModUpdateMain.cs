public class ModUpdateMain : ModBase
{
    public ModUpdateMain(IGame game) : base(game)
    {
    }

    // Should use ReadWrite to be correct but that would be too slow
    public override void OnReadOnlyMainThread(float dt) => Game.Update(dt);
}
