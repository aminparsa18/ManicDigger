using System.Numerics;

public class ModDrawSprites : ClientMod
{
    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        float one = 1;
        for (int i = 0; i < game.entitiesCount; i++)
        {
            Entity entity = game.entities[i];
            if (entity == null) { continue; }
            if (entity.sprite == null) { continue; }
            Sprite b = entity.sprite;
            game.GLMatrixModeModelView();
            game.GLPushMatrix();
            game.GLTranslate(b.positionX, b.positionY, b.positionZ);
            Billboard(game);
            game.GLScale((one * 2 / 100), (one * 2 / 100), (one * 2 / 100));
            game.GLTranslate(0 - b.size / 2, 0 - b.size / 2, 0);
            //d_Draw2d.Draw2dTexture(night ? moontexture : suntexture, 0, 0, ImageSize, ImageSize, null, Color.White);
            IntRef n = null;
            if (b.animationcount > 0)
            {
                float progress = one - (entity.expires.timeLeft / entity.expires.totalTime);
                n = IntRef.Create(game.platform.FloatToInt(progress * (b.animationcount * b.animationcount - 1)));
            }
            game.Draw2dTexture(game.GetTexture(b.image), 0, 0, b.size, b.size, n, b.animationcount, Game.ColorFromArgb(255, 255, 255, 255), true);
            game.GLPopMatrix();
        }
    }

    public static void Billboard(Game game)
    {
        Matrix4x4 m = game.mvMatrix.Peek();
        float d = MathF.Sqrt(m.M11 * m.M11 + m.M12 * m.M12 + m.M13 * m.M13);

        m.M11 = d; m.M12 = 0; m.M13 = 0; m.M14 = 0;
        m.M21 = 0; m.M22 = d; m.M23 = 0; m.M24 = 0;
        m.M31 = 0; m.M32 = 0; m.M33 = d; m.M34 = 0;
        // M41, M42, M43 stay as they are (translation)
        m.M44 = 1;

        m *= Matrix4x4.CreateRotationX(MathF.PI);

        game.GLLoadMatrix(m);
    }
}