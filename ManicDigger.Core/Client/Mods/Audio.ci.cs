using ManicDigger;

/// <summary>
/// Mod that manages audio loading, playback, positional updates, looping, and cleanup
/// for all in-game sounds. Runs once per frame after assets are fully loaded.
/// </summary>
public sealed class ModAudio : ModBase
{
    private readonly Dictionary<string, AudioData> _audioCache = new();
    private readonly IAudioService _audioService;
    private bool _preloaded;

    /// <param name="audioService">Audio backend used for all playback operations.</param>
    public ModAudio(IAudioService audioService)
    {
        _audioService = audioService;
    }

    /// <inheritdoc/>
    public override void OnNewFrame(IGame game, float dt)
    {
        if (game.AssetsLoadProgress < 1f) return;

        if (!_preloaded)
        {
            _preloaded = true;
            Preload(game);
        }

        ProcessSounds(game);
    }

    /// <inheritdoc/>
    public override void OnNewFrameFixed(IGame game, float dt)
    {
        if (game.GuiState == GuiState.MapLoading) return;

        float orientationX = MathF.Sin(game.Player.position.roty);
        float orientationZ = -MathF.Cos(game.Player.position.roty);
        _audioService.UpdateListener(
            game.Player.position.x, game.Player.position.y, game.Player.position.z,
            orientationX, 0f, orientationZ);
    }

    // ── Per-frame sound processing ────────────────────────────────────────────

    private void ProcessSounds(IGame game)
    {
        for (int i = 0; i < _audioService.SoundsCount; i++)
        {
            Sound? sound = _audioService.Sounds[i];
            if (sound is null) continue;

            TryLoad(game, i, sound);
            TryUpdatePosition(sound);
            TryStop(i, sound);
            TryFinish(i, sound);
        }
    }

    /// <summary>
    /// Creates and starts playback for a sound whose <see cref="AudioTask"/> has not
    /// yet been allocated. Sets <see cref="AudioTask.Loop"/> before playing so the
    /// backend handles looping internally without requiring task recreation.
    /// </summary>
    private void TryLoad(IGame game, int i, Sound sound)
    {
        if (sound.Task is not null) return;

        AudioData data = GetOrLoadAudioData(game, sound.Name);
        if (!_audioService.IsAudioDataLoaded(data)) return;

        AudioTask task = _audioService.CreateAudio(data);
        task.Loop = sound.Loop;
        sound.Task = task;
        _audioService.Play(task);
    }

    /// <summary>Synchronises the OpenAL source position with the sound's world position.</summary>
    private void TryUpdatePosition(Sound sound)
    {
        if (sound.Task is null) return;
        _audioService.SetPosition(sound.Task, sound.X, sound.Y, sound.Z);
    }

    /// <summary>Destroys and clears any sound flagged for stopping.</summary>
    private void TryStop(int i, Sound sound)
    {
        if (sound.Task is null || !sound.Stop) return;
        _audioService.DestroyAudio(sound.Task);
        _audioService.Sounds[i] = null;
    }

    /// <summary>
    /// Clears finished one-shot sounds. Looping sounds are handled internally
    /// by <see cref="AudioTask.Loop"/> and do not need recreation here.
    /// </summary>
    private void TryFinish(int i, Sound sound)
    {
        if (sound.Task is null) return;
        if (!_audioService.IsFinished(sound.Task)) return;
        _audioService.Sounds[i] = null;
    }

    // ── Asset helpers ─────────────────────────────────────────────────────────

    /// <summary>Preloads all <c>.ogg</c> assets so the first playback request is instant.</summary>
    private void Preload(IGame game)
    {
        foreach (Asset asset in game.Assets)
        {
            if (asset.name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                GetOrLoadAudioData(game, asset.name);
        }
    }

    /// <summary>
    /// Returns cached <see cref="AudioData"/> for <paramref name="name"/>,
    /// decoding the asset on first access.
    /// </summary>
    private AudioData GetOrLoadAudioData(IGame game, string name)
    {
        if (!_audioCache.TryGetValue(name, out AudioData? data))
        {
            data = _audioService.CreateAudioData(
                game.GetAssetFile(name),
                game.GetAssetFileLength(name));
            _audioCache[name] = data;
        }
        return data;
    }
}


// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Represents an active or pending sound instance in the world.
/// </summary>
public sealed class Sound
{
    /// <summary>Asset name of the audio file to play (e.g. <c>"hit.ogg"</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>World-space X coordinate of the sound source.</summary>
    public float X { get; set; }

    /// <summary>World-space Y coordinate of the sound source.</summary>
    public float Y { get; set; }

    /// <summary>World-space Z coordinate of the sound source.</summary>
    public float Z { get; set; }

    /// <summary>When <see langword="true"/>, the sound plays continuously until stopped.</summary>
    public bool Loop { get; init; }

    /// <summary>When <see langword="true"/>, the sound will be destroyed on the next frame.</summary>
    public bool Stop { get; set; }

    /// <summary>The active playback task; <see langword="null"/> until the asset is loaded.</summary>
    internal AudioTask? Task { get; set; }
}