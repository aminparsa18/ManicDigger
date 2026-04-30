public class ModUpdateMain : ModBase
{
    private readonly IGame _game;

    public ModUpdateMain(IGame game)
    {
        _game = game;
    }

    // Should use ReadWrite to be correct but that would be too slow
    public override void OnReadOnlyMainThread(float dt)
    {
        _game.Update(dt);
    }
}
