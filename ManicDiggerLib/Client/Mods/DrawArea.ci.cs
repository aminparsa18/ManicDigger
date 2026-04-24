/// <summary>
/// Renders wireframe outlines around entity draw areas. Toggle visibility by hitting an entity.
/// </summary>
public class ModDrawArea : ModBase
{
    private readonly DrawWireframeCube lines = new();

    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        if (!game.ENABLE_DRAW2D) return;

        for (int i = 0; i < game.Entities.Count; i++)
        {
            Entity e = game.Entities[i];
            if (e?.drawArea == null || !e.drawArea.visible) continue;

            float cx = e.drawArea.x + e.drawArea.sizex / 2f;
            float cy = e.drawArea.y + e.drawArea.sizey / 2f;
            float cz = e.drawArea.z + e.drawArea.sizez / 2f;

            lines.DrawWireframeCube_(game, cx, cy, cz, e.drawArea.sizex, e.drawArea.sizey, e.drawArea.sizez);
        }
    }

    public override void OnHitEntity(Game game, OnUseEntityArgs e)
    {
        var area = game.Entities[e.entityId]?.drawArea;
        if (area == null) return;
        area.visible = !area.visible;
    }
}