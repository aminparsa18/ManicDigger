/// <summary>
/// Plays footstep sounds based on the block under the player and movement state.
/// </summary>
public class ModWalkSound : ModBase
{
    private const float StepSoundDuration = 0.4f;
    private const float RandomSoundChance = 40;

    private float walkSoundTimer;
    private int lastWalkSound;
    private readonly Random random;

    public ModWalkSound()
    {
        random = new Random();
    }

    public override void OnNewFrameFixed(IGame game, float args)
    {
        if (game.FollowId() != null) return;

        if (game.soundnow)
            UpdateWalkSound(game, StepSoundDuration / 2);

        if (game.IsPlayerOnGround && (game.Controls.MovedX != 0 || game.Controls.MovedY != 0))
            UpdateWalkSound(game, args);
    }

    internal void UpdateWalkSound(IGame game, float dt)
    {
        walkSoundTimer += dt;

        string[] sounds = CurrentWalkSounds(game);
        int soundCount = GetSoundCount(sounds);
        if (soundCount == 0) return;

        if (walkSoundTimer < StepSoundDuration) return;

        walkSoundTimer = 0;
        lastWalkSound = (lastWalkSound + 1) % soundCount;

        if (random.Next() % 100 < RandomSoundChance)
            lastWalkSound = random.Next() % soundCount;

        game.PlayAudio(sounds[lastWalkSound]);
    }

    internal string[] CurrentWalkSounds(IGame game)
    {
        int b = game.BlockUnderPlayer();
        return game.BlockRegistry.WalkSound[b != -1 ? b : 0];
    }

    internal static int GetSoundCount(string[] sounds)
    {
        int count = 0;
        for (int i = 0; i < BlockTypeRegistry.SoundCount; i++)
            if (sounds[i] != null) count++;
        return count;
    }
}