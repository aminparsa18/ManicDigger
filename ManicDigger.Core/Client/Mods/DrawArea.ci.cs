/// <summary>
/// Renders wireframe outlines around entity draw areas. Toggle visibility by hitting an entity.
/// </summary>
public class ModDrawArea : ModBase
{
    private readonly DrawWireframeCube lines;

    public ModDrawArea(IOpenGlService platform, IMeshDrawer meshDrawer)
    {
        lines = new DrawWireframeCube(platform, meshDrawer);
    }

    public override void OnNewFrameDraw3d(IGame game, float deltaTime)
    {
        if (!game.ENABLE_DRAW2D) return;

        for (int i = 0; i < game.Entities.Count; i++)
        {
            Entity e = game.Entities[i];
            if (e?.drawArea == null || !e.drawArea.visible) continue;

            float cx = e.drawArea.x + e.drawArea.sizex / 2f;
            float cy = e.drawArea.y + e.drawArea.sizey / 2f;
            float cz = e.drawArea.z + e.drawArea.sizez / 2f;

            lines.DrawWireframeCube_(cx, cy, cz, e.drawArea.sizex, e.drawArea.sizey, e.drawArea.sizez);
        }
    }

    public override void OnHitEntity(IGame game, OnUseEntityArgs e)
    {
        var area = game.Entities[e.Id]?.drawArea;
        if (area == null) return;
        area.visible = !area.visible;
    }
}