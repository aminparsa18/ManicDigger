using OpenTK.Mathematics;

/// <summary>
/// Renders player name tags and health bars above entity models in the 3D world.
/// </summary>
public class ModDrawPlayerNames : ModBase
{
    private const float NameTagHeightOffset = 0.7f;
    private const float NameTagScale = 0.02f;
    private const float NameTagDrawDistance = 20f;

    private readonly IMeshDrawer meshDrawer;
    public ModDrawPlayerNames(IMeshDrawer meshDrawer, IGame game) : base(game)
    {
        this.meshDrawer = meshDrawer;
    }

    public override void OnNewFrameDraw3d( float deltaTime)
    {
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity e = Game.Entities[i];
            if (e?.drawName == null) continue;
            if (i == Game.LocalPlayerId) continue;
            if (e.networkPosition != null && !e.networkPosition.PositionLoaded) continue;

            DrawName p = e.drawName;
            if (p.OnlyWhenSelected) continue;

            float posX = p.TextX + e.position.x;
            float posY = p.TextY + e.position.y + e.drawModel.ModelHeight + NameTagHeightOffset;
            float posZ = p.TextZ + e.position.z;
            bool nearEnough = Vector3.Distance(new(Game.Player.position.x, Game.Player.position.y, Game.Player.position.z), new(posX, posY, posZ)) < NameTagDrawDistance;
            bool altHeld = Game.KeyboardState[KeyConstants.KeyAltLeft] || Game.KeyboardState[KeyConstants.KeyAltRight];
            if (!nearEnough && !altHeld) continue;

            DrawNameTag(p, posX, posY, posZ);
        }
    }

    private void DrawNameTag( DrawName p, float posX, float posY, float posZ)
    {
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(posX, posY, posZ);
        VectorUtils.Billboard(meshDrawer);
        meshDrawer.GLScale(NameTagScale, NameTagScale, NameTagScale);

        if (p.DrawHealth)
        {
            Game.Draw2dTexture(Game.GetOrCreateWhiteTexture(), -26, -11, 52, 12, null, 0, ColorUtils.ColorFromArgb(255, 0, 0, 0), false);
            Game.Draw2dTexture(Game.GetOrCreateWhiteTexture(), -25, -10, 50 * p.Health, 10, null, 0, ColorUtils.ColorFromArgb(255, 255, 0, 0), false);
        }

        Font font = new("Arial", 14);
        Game.Draw2dText(p.Name, font, -Game.TextSizeWidth(p.Name, 14) / 2, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), true);

        meshDrawer.GLPopMatrix();
    }
}