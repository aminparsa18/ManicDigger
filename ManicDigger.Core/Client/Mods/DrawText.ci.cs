using System.Numerics;

/// <summary>
/// Renders floating 3D text labels attached to entities, facing the player when nearby.
/// </summary>
public class ModDrawText : ModBase
{
    private const float TextScale = 0.005f;
    private const float TextDrawDistance = 20f;

    private static readonly Font Font = new("Arial", 14, FontStyle.Regular);
    private readonly IMeshDrawer meshDrawer;

    public ModDrawText(IMeshDrawer meshDrawer, IGame game) : base(game)
    {
        this.meshDrawer = meshDrawer;
    }

    public override void OnNewFrameDraw3d(float deltaTime)
    {
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity e = Game.Entities[i];
            if (e?.drawText == null) continue;
            if (e.networkPosition != null && !e.networkPosition.PositionLoaded) continue;

            EntityDrawText p = e.drawText;
            float posX = -MathF.Sin(e.position.roty) * p.dx + e.position.x;
            float posY = p.dy + e.position.y;
            float posZ = MathF.Cos(e.position.roty) * p.dz + e.position.z;

            bool nearEnough = Vector3.Distance(new Vector3(Game.Player.position.x, Game.Player.position.y, Game.Player.position.z), new Vector3(posX, posY, posZ)) < TextDrawDistance;
            bool altHeld = Game.KeyboardState[KeyConstants.KeyAltLeft] || Game.KeyboardState[KeyConstants.KeyAltRight];
            if (!nearEnough && !altHeld) continue;

            DrawText(e, p, posX, posY, posZ);
        }
    }

    private void DrawText(Entity e, EntityDrawText p, float posX, float posY, float posZ)
    {
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(posX, posY, posZ);
        meshDrawer.GLRotate(180, 1, 0, 0);
        meshDrawer.GLRotate(float.RadiansToDegrees(e.position.roty), 0, 1, 0);
        meshDrawer.GLScale(TextScale, TextScale, TextScale);
        Game.Draw2dText(p.text, Font, -Game.TextSizeWidth(p.text, 14) / 2, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), true);
        meshDrawer.GLPopMatrix();
    }
}