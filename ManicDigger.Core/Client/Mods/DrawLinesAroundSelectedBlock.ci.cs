/// <summary>
/// Draws a wireframe outline around the currently selected block or entity.
/// </summary>
public class ModDrawLinesAroundSelectedBlock : ModBase
{
    private const float SelectionScale = 1.02f;

    private readonly DrawWireframeCube lines;
    private readonly IGame game;

    public ModDrawLinesAroundSelectedBlock(IGame game, IOpenGlService platform)
    {
        this.game = game;
        lines = new DrawWireframeCube(platform);
    }

    public override void OnNewFrameDraw3d(float deltaTime)
    {
        if (!game.ENABLE_DRAW2D) return;

        if (game.SelectedEntityId != -1)
            DrawEntityOutline();
        else if (game.SelectedBlockPositionX != -1)
            DrawBlockOutline();
    }

    private void DrawEntityOutline()
    {
        Entity e = game.Entities[game.SelectedEntityId];
        if (e == null) return;

        float height = e.drawModel.ModelHeight;
        lines.DrawWireframeCube_(game,
            e.position.x, e.position.y + height / 2, e.position.z,
            SelectionScale, SelectionScale * height, SelectionScale);
    }

    private void DrawBlockOutline()
    {
        int x = game.SelectedBlockPositionX;
        int y = game.SelectedBlockPositionY;
        int z = game.SelectedBlockPositionZ;
        float blockHeight = game.Getblockheight(x, z, y);

        lines.DrawWireframeCube_(game,
            x + 0.5f, y + blockHeight * 0.5f, z + 0.5f,
            SelectionScale, SelectionScale * blockHeight, SelectionScale);
    }
}

/// <summary>
/// Renders a wireframe cube at a given position and scale using GL line rendering.
/// </summary>
public class DrawWireframeCube
{
    private GeometryModel wireframeCube;
    private readonly IOpenGlService platform;

    public DrawWireframeCube(IOpenGlService game)
    {
        this.platform = game;
    }

    public void DrawWireframeCube_(IGame game, float posX, float posY, float posZ, float scaleX, float scaleY, float scaleZ)
    {
        platform.GLLineWidth(2);
        platform.BindTexture2d(0);

        wireframeCube ??= platform.CreateModel(WireframeCube.Create());

        game.GLPushMatrix();
        game.GLTranslate(posX, posY, posZ);
        game.GLScale(scaleX * 0.5f, scaleY * 0.5f, scaleZ * 0.5f);
        game.DrawModel(wireframeCube);
        game.GLPopMatrix();
    }
}