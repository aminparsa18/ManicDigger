using System.Numerics;


/// <summary>
/// Accumulates push forces from nearby explosion/push entities onto the local player each fixed frame.
/// </summary>
public class ModPush : ModBase
{

    public ModPush(IGame game) : base(game)
    {
    }

    public override void OnNewFrameFixed(float args)
    {
        Game.PushX = 0;
        Game.PushY = 0;
        Game.PushZ = 0;

        float pX = Game.Player.position.x;
        float pY = Game.Player.position.y;
        float pZ = Game.Player.position.z;
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity entity = Game.Entities[i];
            if (entity?.push == null) continue;
            if (entity.networkPosition != null && !entity.networkPosition.PositionLoaded) continue;

            float kX = EncodingHelper.DecodeFixedPoint(entity.push.XFloat);
            float kY = EncodingHelper.DecodeFixedPoint(entity.push.ZFloat);
            float kZ = EncodingHelper.DecodeFixedPoint(entity.push.YFloat);

            if (entity.push.IsRelativeToPlayerPosition != 0)
            {
                kX += pX;
                kY += pY;
                kZ += pZ;
            }

            if (Vector3.Distance(new Vector3(kX, kY, kZ), new Vector3(pX, pY, pZ)) < EncodingHelper.DecodeFixedPoint(entity.push.RangeFloat))
            {
                Game.PushX += pX - kX;
                Game.PushY += pY - kY;
                Game.PushZ += pZ - kZ;
            }
        }
    }
}