using System.Numerics;


/// <summary>
/// Accumulates push forces from nearby explosion/push entities onto the local player each fixed frame.
/// </summary>
public class ModPush : ModBase
{
    private readonly IGame game;

    public ModPush(IGame game)
    {
        this.game = game;
    }

    public override void OnNewFrameFixed(float args)
    {
        game.PushX = 0;
        game.PushY = 0;
        game.PushZ = 0;

        float pX = game.Player.position.x;
        float pY = game.Player.position.y;
        float pZ = game.Player.position.z;

        for (int i = 0; i < game.Entities.Count; i++)
        {
            Entity entity = game.Entities[i];
            if (entity?.push == null) continue;
            if (entity.networkPosition != null && !entity.networkPosition.PositionLoaded) continue;

            float kX = game.DecodeFixedPoint(entity.push.XFloat);
            float kY = game.DecodeFixedPoint(entity.push.ZFloat);
            float kZ = game.DecodeFixedPoint(entity.push.YFloat);

            if (entity.push.IsRelativeToPlayerPosition != 0)
            {
                kX += pX;
                kY += pY;
                kZ += pZ;
            }

            if (Vector3.Distance(new Vector3(kX, kY, kZ), new Vector3(pX, pY, pZ)) < game.DecodeFixedPoint(entity.push.RangeFloat))
            {
                game.PushX += pX - kX;
                game.PushY += pY - kY;
                game.PushZ += pZ - kZ;
            }
        }
    }
}