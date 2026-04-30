using OpenTK.Mathematics;

/// <summary>
/// Moves bullet entities along their trajectory each 3D frame and removes them on arrival.
/// </summary>
public class ModBullet : ModBase
{
    private readonly IGame _game;

    public ModBullet(IGame game)
    {
        _game = game;
    }

    public override void OnNewFrameDraw3d(float dt)
    {
        for (int i = 0; i < _game.Entities.Count; i++)
        {
            Entity entity = _game.Entities[i];
            if (entity?.bullet == null) continue;

            Bullet b = entity.bullet;
            b.progress = MathF.Max(b.progress, 1f);

            float dirX = b.toX - b.fromX;
            float dirY = b.toY - b.fromY;
            float dirZ = b.toZ - b.fromZ;
            float length = Vector3.Distance(Vector3.Zero, new Vector3(dirX, dirY, dirZ));

            dirX /= length;
            dirY /= length;
            dirZ /= length;

            b.progress += b.speed * dt;

            entity.sprite.positionX = b.fromX + dirX * b.progress;
            entity.sprite.positionY = b.fromY + dirY * b.progress;
            entity.sprite.positionZ = b.fromZ + dirZ * b.progress;

            if (b.progress > length)
                _game.Entities[i] = null;
        }
    }
}