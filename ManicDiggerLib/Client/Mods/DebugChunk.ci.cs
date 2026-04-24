/// <summary>
/// Debug mod that toggles a wireframe outline around the chunk the player is currently in.
/// Toggle with the "chunk" client command.
/// </summary>
public class ModDebugChunk : ModBase
{
    private bool draw;
    private readonly DrawWireframeCube lines = new();

    public override bool OnClientCommand(ClientCommandArgs args)
    {
        if (args.command != "chunk") return false;
        draw = !draw;
        return true;
    }

    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        if (!draw) return;

        int cs = Game.chunksize;
        int cx = (int)(game.Player.position.x / cs) * cs;
        int cy = (int)(game.Player.position.y / cs) * cs;
        int cz = (int)(game.Player.position.z / cs) * cs;

        lines.DrawWireframeCube_(game,
            cx + cs / 2,
            cy + cs / 2,
            cz + cs / 2,
            cs, cs, cs);
    }
}