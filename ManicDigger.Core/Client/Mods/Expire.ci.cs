using ManicDigger;
using OpenTK.Mathematics;

/// <summary>
/// Removes entities when their lifetime expires, triggering grenade explosions where applicable.
/// </summary>
public class ModExpire : ModBase
{
    private readonly IBlockRegistry _blockTypeRegistry;
    public ModExpire(IGame game, IBlockRegistry blockTypeRegistry) : base(game)
    {
        _blockTypeRegistry = blockTypeRegistry;
    }

    public override void OnNewFrameFixed(float args)
    {
        float dt = args;
        for (int i = 0; i < Game.Entities.Count; i++)
        {
            Entity entity = Game.Entities[i];
            if (entity?.expires == null)
            {
                continue;
            }

            entity.expires.timeLeft -= dt;
            if (entity.expires.timeLeft > 0)
            {
                continue;
            }

            if (entity.grenade != null)
            {
                GrenadeExplosion(i);
            }

            Game.Entities[i] = null;
        }
    }

    private void GrenadeExplosion(int grenadeEntityId)
    {
        Entity grenadeEntity = Game.Entities[grenadeEntityId];
        Sprite sprite = grenadeEntity.sprite;
        Grenade grenade = grenadeEntity.grenade;
        var blockType = _blockTypeRegistry.BlockTypes[grenade.block];

        float posX = sprite.positionX;
        float posY = sprite.positionY;
        float posZ = sprite.positionZ;

        Game.PlayAudioAt("grenadeexplosion.ogg", posX, posY, posZ);

        // Spawn explosion animation sprite
        Game.EntityAddLocal(new Entity
        {
            sprite = new Sprite
            {
                image = "ani5.png",
                positionX = posX,
                positionY = posY + 1,
                positionZ = posZ,
                size = 200,
                animationcount = 4
            },
            expires = Expires.Create(1)
        });

        // Spawn explosion push entity
        float explosionTime = blockType.ExplosionTime;
        float explosionRange = blockType.ExplosionRange;

        Game.EntityAddLocal(new Entity
        {
            push = new Packet_ServerExplosion
            {
                XFloat = EncodingHelper.EncodeFixedPoint(posX),
                YFloat = EncodingHelper.EncodeFixedPoint(posZ),
                ZFloat = EncodingHelper.EncodeFixedPoint(posY),
                RangeFloat = (int)blockType.ExplosionRange,
                IsRelativeToPlayerPosition = 0,
                TimeFloat = (int)blockType.ExplosionTime
            },
            expires = new Expires { timeLeft = explosionTime }
        });

        // Apply damage to local player based on distance
        float dist = Vector3.Distance(new Vector3(Game.Player.position.x, Game.Player.position.y, Game.Player.position.z), new Vector3(posX, posY, posZ));
        float dmg = (1f - (dist / explosionRange)) * blockType.DamageBody;
        if (dmg > 0)
        {
            Game.ApplyDamageToPlayer((int)dmg, DeathReason.Explosion, grenade.sourcePlayer);
        }
    }
}