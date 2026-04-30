/// <summary>
/// Renders billboard sprites for entities (e.g. particles, projectile effects) in the 3D world.
/// </summary>
public class ModDrawSprites : ModBase
{
    private const float SpriteScale = 0.02f;

    public ModDrawSprites()
    {
    }

    public override void OnNewFrameDraw3d(IGame game, float deltaTime)
    {
        for (int i = 0; i < game.Entities.Count; i++)
        {
            Entity entity = game.Entities[i];
            if (entity?.sprite == null) continue;

            Sprite b = entity.sprite;
            int? frame = null;
            if (b.animationcount > 0)
            {
                float progress = 1f - (entity.expires.timeLeft / entity.expires.totalTime);
                frame = (int)(progress * (b.animationcount * b.animationcount - 1));
            }

            game.GLMatrixModeModelView();
            game.GLPushMatrix();
            game.GLTranslate(b.positionX, b.positionY, b.positionZ);
            VectorUtils.Billboard(game);
            game.GLScale(SpriteScale, SpriteScale, SpriteScale);
            game.GLTranslate(-b.size / 2, -b.size / 2, 0);
            game.Draw2dTexture(game.GetTexture(b.image), 0, 0, b.size, b.size, frame, b.animationcount, ColorUtils.ColorFromArgb(255, 255, 255, 255), true);
            game.GLPopMatrix();
        }
    }


}