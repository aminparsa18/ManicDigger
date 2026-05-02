/// <summary>
/// Debug mod that toggles a wireframe outline around the chunk the player is currently in.
/// Toggle with the "chunk" client command.
/// </summary>
public class ModDebugChunk : ModBase
{
    private bool draw;
    private readonly DrawWireframeCube lines;

    public ModDebugChunk(IOpenGlService platform, IMeshDrawer meshDrawer, IGame game) : base(game)
    {
        lines = new DrawWireframeCube(platform, meshDrawer);
    }

    public override bool OnClientCommand(ClientCommandArgs args)
    {
        if (args.Command != "chunk")
        {
            return false;
        }

        draw = !draw;
        return true;
    }

    public override void OnNewFrameDraw3d(float deltaTime)
    {
        if (!draw)
        {
            return;
        }

        int cs = GameConstants.CHUNK_SIZE;
        int cx = (int)(Game.Player.position.x / cs) * cs;
        int cy = (int)(Game.Player.position.y / cs) * cs;
        int cz = (int)(Game.Player.position.z / cs) * cs;

        lines.DrawWireframeCube_(
            cx + (cs / 2),
            cy + (cs / 2),
            cz + (cs / 2),
            cs, cs, cs);
    }
}