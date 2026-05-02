/// <summary>
/// Draws a wireframe outline around the currently selected block or entity.
/// </summary>
public class ModDrawLinesAroundSelectedBlock : ModBase
{
    private const float SelectionScale = 1.02f;

    private readonly DrawWireframeCube lines;

    public ModDrawLinesAroundSelectedBlock(IOpenGlService platform, IMeshDrawer meshDrawer, IGame game) : base(game)
    {
        lines = new DrawWireframeCube(platform, meshDrawer);
    }

    public override void OnNewFrameDraw3d(float deltaTime)
    {
        if (!Game.ENABLE_DRAW2D)
        {
            return;
        }

        if (Game.SelectedEntityId != -1)
        {
            DrawEntityOutline(Game);
        }
        else if (Game.SelectedBlockPositionX != -1)
        {
            DrawBlockOutline(Game);
        }
    }

    private void DrawEntityOutline(IGame game)
    {
        Entity e = game.Entities[game.SelectedEntityId];
        if (e == null)
        {
            return;
        }

        float height = e.drawModel.ModelHeight;
        lines.DrawWireframeCube_(e.position.x, e.position.y + (height / 2), e.position.z,
            SelectionScale, SelectionScale * height, SelectionScale);
    }

    private void DrawBlockOutline(IGame game)
    {
        int x = game.SelectedBlockPositionX;
        int y = game.SelectedBlockPositionY;
        int z = game.SelectedBlockPositionZ;
        float blockHeight = game.Getblockheight(x, z, y);

        lines.DrawWireframeCube_(x + 0.5f, y + (blockHeight * 0.5f), z + 0.5f,
            SelectionScale, SelectionScale * blockHeight, SelectionScale);
    }
}

/// <summary>
/// Renders a wireframe cube at a given position and scale using GL line rendering.
/// </summary>
public class DrawWireframeCube
{
    private GeometryModel wireframeCube;
    private readonly IMeshDrawer meshDrawer;
    private readonly IOpenGlService platform;

    public DrawWireframeCube(IOpenGlService game, IMeshDrawer meshDrawer)
    {
        this.platform = game;
        this.meshDrawer = meshDrawer;
    }

    public void DrawWireframeCube_(float posX, float posY, float posZ, float scaleX, float scaleY, float scaleZ)
    {
        platform.GLLineWidth(2);
        platform.BindTexture2d(0);

        wireframeCube ??= platform.CreateModel(WireframeCube.Create());

        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(posX, posY, posZ);
        meshDrawer.GLScale(scaleX * 0.5f, scaleY * 0.5f, scaleZ * 0.5f);
        meshDrawer.DrawModel(wireframeCube);
        meshDrawer.GLPopMatrix();
    }
}