/// <summary>
/// Implements <see cref="IModManager"/> by delegating to <see cref="IGameClient"/>
/// and <see cref="IGamePlatform"/>. Mods never hold a reference to <see cref="Game"/>
/// directly — all access is mediated through these two interfaces.
/// </summary>
public class ClientModManager : IModManager
{
    private readonly IGameClient _game;
    private readonly IGamePlatform _platform;

    /// <param name="game">The game client implementation (typically <see cref="Game"/>).</param>
    public ClientModManager(IGameClient game)
    {
        _game = game;
        _platform = game.Platform;
    }

    // -------------------------------------------------------------------------
    // Platform
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public IGamePlatform GetPlatform() => _platform;

    /// <inheritdoc/>
    public int GetWindowWidth() => _platform.GetCanvasWidth();

    /// <inheritdoc/>
    public int GetWindowHeight() => _platform.GetCanvasHeight();

    /// <inheritdoc/>
    public Bitmap GrabScreenshot() => _platform.GrabScreenshot();

    /// <inheritdoc/>
    public void MakeScreenshot() => _platform.SaveScreenshot();

    // -------------------------------------------------------------------------
    // Player position
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public void SetLocalPosition(float x, float y, float z)
    {
        _game.LocalPositionX = x;
        _game.LocalPositionY = y;
        _game.LocalPositionZ = z;
    }

    /// <inheritdoc/>
    public float GetLocalPositionX() => _game.LocalPositionX;

    /// <inheritdoc/>
    public float GetLocalPositionY() => _game.LocalPositionY;

    /// <inheritdoc/>
    public float GetLocalPositionZ() => _game.LocalPositionZ;

    // -------------------------------------------------------------------------
    // Player orientation
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public void SetLocalOrientation(float x, float y, float z)
    {
        _game.LocalOrientationX = x;
        _game.LocalOrientationY = y;
        _game.LocalOrientationZ = z;
    }

    /// <inheritdoc/>
    public float GetLocalOrientationX() => _game.LocalOrientationX;

    /// <inheritdoc/>
    public float GetLocalOrientationY() => _game.LocalOrientationY;

    /// <inheritdoc/>
    public float GetLocalOrientationZ() => _game.LocalOrientationZ;

    // -------------------------------------------------------------------------
    // Chat
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public void DisplayNotification(string message) => _game.AddChatLine(message);

    /// <inheritdoc/>
    public void SendChatMessage(string message) => _game.SendChat(message);

    // -------------------------------------------------------------------------
    // GUI / camera
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public void ShowGui(int level) => _game.EnableDraw2d = level != 0;

    /// <inheritdoc/>
    public void EnableCameraControl(bool enable) => _game.EnableCameraControl = enable;

    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public bool IsFreemoveAllowed() => _game.IsFreemoveAllowed;

    /// <inheritdoc/>
    public void SetFreemove(int level) => _game.FreemoveLevel = level;

    /// <inheritdoc/>
    public int GetFreemove() => _game.FreemoveLevel;

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public int WhiteTexture() => _game.WhiteTexture();

    /// <inheritdoc/>
    public void Draw2dTexture(int textureid, float x1, float y1, float width, float height, int inAtlasId, int color)
    {
        _game.Draw2dTexture(
            textureid,
            (int)x1, (int)y1,
            (int)width, (int)height,
            inAtlasId, 0,
            ColorUtils.ColorFromArgb(
                ColorUtils.ColorA(color),
                ColorUtils.ColorR(color),
                ColorUtils.ColorG(color),
                ColorUtils.ColorB(color)),
            false);
    }

    /// <inheritdoc/>
    public void Draw2dTextures(Draw2dData[] todraw, int todrawLength, int textureId) =>
        _game.Draw2dTextures(todraw, todrawLength, textureId);

    /// <inheritdoc/>
    public void Draw2dText(string text, float x, float y, float fontsize) =>
        _game.Draw2dText(text, new Font("Arial", fontsize), x, y, null, false);

    /// <inheritdoc/>
    public void OrthoMode() =>
        _game.OrthoMode(_platform.GetCanvasWidth(), _platform.GetCanvasHeight());

    /// <inheritdoc/>
    public void PerspectiveMode() => _game.PerspectiveMode();

    // -------------------------------------------------------------------------
    // Diagnostics
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public Dictionary<string, string> GetPerformanceInfo() => _game.PerformanceInfo;
}