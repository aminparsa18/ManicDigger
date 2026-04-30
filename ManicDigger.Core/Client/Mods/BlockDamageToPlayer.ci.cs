//This handles two types of environmental damage to the player:
//Block damage — every second, checks the block at eye level and the block at foot level.
//If either deals damage (like lava), it adds them together and calls ApplyDamageToPlayer.
//A timer ensures it only ticks once per second rather than every frame.
//Drowning — every second, if the player's eyes are underwater it decrements their oxygen.
//When oxygen hits zero it deals damage equal to 10% of max health. When they surface, oxygen instantly refills.
//The current oxygen level is also sent to the server if it's new enough to support that packet.

using ManicDigger;

/// <summary>
/// Handles environmental damage to the player from blocks (e.g. lava, fire) and drowning mechanics.
/// </summary>
public class ModBlockDamageToPlayer : ModBase
{
    public const int BlockDamageToPlayerEvery = 1;
    private int lastOxygenTickMilliseconds;
    private readonly DamageTimer blockDamageTimer;

    private readonly IGameService _platform;

    public ModBlockDamageToPlayer( IGameService platform)
    {
        _platform = platform;
        blockDamageTimer = new DamageTimer(BlockDamageToPlayerEvery, BlockDamageToPlayerEvery * 2);
    }

    public override void OnNewFrameFixed(IGame game, float args)
    {
        if (game.GuiState == GuiState.MapLoading || game.FollowId() != null)
            return;

        UpdateBlockDamageToPlayer(game, args);
    }

    private void UpdateBlockDamageToPlayer(IGame game, float dt)
    {
        UpdateBlockDamage(game, dt);
        UpdateDrowning(game);
    }

    /// <summary>Applies damage from damaging blocks (e.g. lava) the player is standing in or near.</summary>
    private void UpdateBlockDamage(IGame game, float dt)
    {
        float pX = game.LocalPositionX;
        float pY = game.LocalPositionY + game.LocalEyeHeight;
        float pZ = game.LocalPositionZ;

        int block1 = GetBlockAt(game, pX, pY, pZ);
        int block2 = GetBlockAt(game, pX, pY - 1, pZ);

        int damage = game.BlockRegistry.DamageToPlayer[block1] + game.BlockRegistry.DamageToPlayer[block2];
        if (damage <= 0) return;

        // Prefer eye-level block as damage source; fall back to feet block
        int hurtingBlock = (block1 != 0 && game.BlockRegistry.DamageToPlayer[block1] > 0) ? block1 : block2;
        int times = blockDamageTimer.Update(dt);
        for (int i = 0; i < times; i++)
            game.ApplyDamageToPlayer(damage, DeathReason.BlockDamage, hurtingBlock);
    }

    /// <summary>Drains oxygen while underwater and applies drowning damage when oxygen is depleted.</summary>
    private void UpdateDrowning(IGame game)
    {
        int deltaMs = _platform.TimeMillisecondsFromStart - lastOxygenTickMilliseconds;
        if (deltaMs < 1000) return;

        if (game.WaterSwimmingEyes())
        {
            game.PlayerStats.CurrentOxygen -= 1;
            if (game.PlayerStats.CurrentOxygen <= 0)
            {
                game.PlayerStats.CurrentOxygen = 0;
                int dmg = Math.Max(1, game.PlayerStats.MaxHealth / 10);
                game.ApplyDamageToPlayer(dmg, DeathReason.Drowning, 0);
            }
        }
        else
        {
            game.PlayerStats.CurrentOxygen = game.PlayerStats.MaxOxygen;
        }

        if (GameVersionHelper.ServerVersionAtLeast(game.ServerGameVersion, 2014, 3, 31))
            game.SendPacketClient(ClientPackets.Oxygen(game.PlayerStats.CurrentOxygen));
        lastOxygenTickMilliseconds = _platform.TimeMillisecondsFromStart;
    }

    private int GetBlockAt(IGame game, float x, float y, float z)
    {
        int bx = (int)MathF.Floor(x);
        int by = (int)MathF.Floor(y);
        int bz = (int)MathF.Floor(z);
        return game.VoxelMap.IsValidPos(bx, bz, by) ? game.VoxelMap.GetBlock((int)x, (int)z, (int)y) : 0;
    }
}

public class DialogScreen : GameScreen
{
    public DialogScreen(IGameService gameService) : base(gameService)
    {
    }

    public override void OnButton(IGame game, MenuWidget w)
    {
        if (w.isbutton)
        {
            string[] textValues = new string[WidgetCount];
            for (int i = 0; i < WidgetCount; i++)
            {
                string s = widgets[i].text;
                s ??= "";
                textValues[i] = s;
            }
            game.SendPacketClient(ClientPackets.DialogClick(w.id, textValues, WidgetCount));
        }
    }
}

public class DamageTimer
{
    public float Interval { get; }
    public float? MaxDeltaTime { get; }

    private float _accumulator;

    public DamageTimer(float interval, float? maxDeltaTime = null)
    {
        if (interval <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        Interval = interval;
        MaxDeltaTime = maxDeltaTime;
    }

    public void Reset() => _accumulator = 0;

    public int Update(float dt)
    {
        if (dt < 0)
            return 0; // or throw, depending on your engine philosophy

        _accumulator += dt;

        if (MaxDeltaTime.HasValue && _accumulator > MaxDeltaTime.Value)
            _accumulator = MaxDeltaTime.Value;

        int updates = (int)(_accumulator / Interval);
        _accumulator -= updates * Interval;

        return updates;
    }
}