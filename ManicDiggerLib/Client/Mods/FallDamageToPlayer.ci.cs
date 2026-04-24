using ManicDigger;

/// <summary>
/// Applies fall damage and manages the falling wind sound effect for the local player.
/// </summary>
public class ModFallDamageToPlayer : ModBase
{
    private const float FallDamageCooldownSeconds = 1f;

    private bool fallSoundPlaying;
    private int lastFallDamageTimeMilliseconds;
    private readonly IGameClient game;
    private readonly IGamePlatform platform;

    public ModFallDamageToPlayer(IGameClient game, IGamePlatform platform)
    {
        this.game = game;
        this.platform = platform;
    }

    public override void OnNewFrameFixed(float args)
    {
        if (game.GuiState == GuiState.MapLoading) return;

        if (game.Controls.freemove)
        {
            if (fallSoundPlaying) SetFallSoundActive(false);
            return;
        }

        if (game.FollowId() == null)
            UpdateFallDamageToPlayer(args);
    }

    internal void UpdateFallDamageToPlayer(float dt)
    {
        float fallSpeed = game.MovedZ / -game.Basemovespeed;

        int posX = game.PlayerEyesBlockX;
        int posY = game.PlayerEyesBlockY;
        int posZ = game.PlayerEyesBlockZ;

        // Play falling wind sound when high up or falling fast
        bool highUp = game.Blockheight(posX, posY, posZ) < posZ - 8;
        SetFallSoundActive((highUp || fallSpeed > 3) && fallSpeed > 2);

        ApplyFallDamage(posX, posY, posZ, fallSpeed);
    }

    private void ApplyFallDamage(int posX, int posY, int posZ, float fallSpeed)
    {
        if (fallSpeed < 4f) return;
        if (!game.VoxelMap.IsValidPos(posX, posY, posZ - 3)) return;

        int blockBelow = game.VoxelMap.GetBlock(posX, posY, posZ - 3);
        if (blockBelow == 0 || game.IsWater(blockBelow)) return;

        // fallspeed 4 ≈ 10 blocks high, 5.5 ≈ 20 blocks high
        float severity = fallSpeed switch
        {
            < 4.5f => 0.3f,
            < 5.0f => 0.5f,
            < 5.5f => 0.6f,
            < 6.0f => 0.8f,
            _ => 1.0f
        };

        int now = platform.TimeMillisecondsFromStart;
        if ((now - lastFallDamageTimeMilliseconds) / 1000f < FallDamageCooldownSeconds) return;

        lastFallDamageTimeMilliseconds = now;
        game.ApplyDamageToPlayer((int)(severity * game.PlayerStats.MaxHealth), DeathReason.FallDamage, 0);
    }

    internal void SetFallSoundActive(bool active)
    {
        game.AudioPlayLoop("fallloop.wav", active, true);
        fallSoundPlaying = active;
    }
}