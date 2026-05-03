namespace ManicDigger;

public interface ISaveGameService
{
    // Set once when a session starts, never changes mid-session
    void InitialiseSession(SaveTarget target);

}

public readonly record struct SaveTarget
{
    private readonly string? _path;

    // Private so callers must use the factory methods
    private SaveTarget(string? path) => _path = path;

    public static SaveTarget NewGame() => new SaveTarget(null);
    public static SaveTarget FromFile(string path) => new SaveTarget(path);

    public bool IsNewGame => _path is null;

    internal string Resolve(string defaultPath)
        => _path ?? defaultPath;
}

public class SaveGameService() : ISaveGameService
{
    private SaveTarget? _target;

    public void InitialiseSession(SaveTarget target)
    {
        if (_target.HasValue)
            throw new InvalidOperationException("Session already initialised.");
        _target = target;
    }

    private string ResolvedPath => _target.HasValue
        ? _target.Value.Resolve(Path.Combine(GameStorePath.gamepathsaves, "default" + ext))
        : throw new InvalidOperationException("Session not initialised.");

    public byte[] Save(ManicDiggerSave save) { /* write to ResolvedPath */ }
    public ManicDiggerSave Load() { /* read from ResolvedPath */ }
}