/// <summary>
/// Plays footstep sounds based on the block under the player and movement state.
/// </summary>
public class ModWalkSound : ModBase
{
    private const float StepSoundDuration = 0.4f;
    private const float RandomSoundChance = 40;

    private float walkSoundTimer;
    private int lastWalkSound;
    private Random random;
    private readonly IGameClient game;

    public ModWalkSound(IGameClient game)
    {
        this.game = game;
        random = new Random();
    }

    public override void OnNewFrameFixed(float args)
    {
        if (game.FollowId() != null) return;

        if (game.soundnow)
            UpdateWalkSound(StepSoundDuration / 2);

        if (game.IsPlayerOnGround && (game.Controls.movedx != 0 || game.Controls.movedy != 0))
            UpdateWalkSound(args);
    }

    internal void UpdateWalkSound(float dt)
    {
        walkSoundTimer += dt;

        string[] sounds = CurrentWalkSounds();
        int soundCount = GetSoundCount(sounds);
        if (soundCount == 0) return;

        if (walkSoundTimer < StepSoundDuration) return;

        walkSoundTimer = 0;
        lastWalkSound = (lastWalkSound + 1) % soundCount;

        if (random.Next() % 100 < RandomSoundChance)
            lastWalkSound = random.Next() % soundCount;

        game.PlayAudio(sounds[lastWalkSound]);
    }

    internal string[] CurrentWalkSounds()
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