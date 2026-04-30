using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

/// <summary>
/// Manages the main menu lifecycle: asset loading, screen routing, input handling,
/// background animation, and launching the game or server connection flows.
/// </summary>
public class MainMenu : IMenu
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    /// <summary>The key char code for 'F' (uppercase), used to cycle texture filters.</summary>
    private const char KeyF_Upper = 'F';

    /// <summary>The key char code for 'f' (lowercase), used to cycle texture filters.</summary>
    private const char KeyF_Lower = 'f';

    /// <summary>The key char code for backtick '`', used to trigger back navigation.</summary>
    private const int KeyBacktick = 96;

    /// <summary>Number of texture filter modes available (0, 1, 2).</summary>
    private const int FilterModeCount = 3;

    /// <summary>Fixed background tile size in pixels.</summary>
    private const int BackgroundTileSize = 512;

    /// <summary>Maximum delta-time cap per frame to avoid physics tunnelling on hitches.</summary>
    private const float MaxDeltaTime = 1f;

    // -------------------------------------------------------------------------
    // Public state
    // -------------------------------------------------------------------------

    public string Translate(string key) => _lang.Get(key);

    /// <summary>Raw asset list populated asynchronously during <see cref="Start"/>.</summary>
    public List<Asset> Assets { get; set; }

    /// <summary>Fraction [0, 1] indicating how far async asset loading has progressed.</summary>
    public float AssetsLoadProgress { get; set; }

    /// <summary>Renderer responsible for rasterising coloured/styled text into bitmaps.</summary>
    public TextColorRenderer TextColorRenderer { get; set; }

    /// <summary>Background tile horizontal scroll position (pixels, wraps via <see cref="overlap"/>).</summary>
    public int BackgroundW { get; set; }

    /// <summary>Background tile vertical scroll position.</summary>
    public int BackgroundH { get; set; }

    /// <summary>Current canvas width cached each frame.</summary>
    public float WindowX { get; set; }

    /// <summary>Current canvas height cached each frame.</summary>
    public float WindowY { get; set; }

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    /// <summary>The active platform abstraction (windowing, GL, input, etc.).</summary>
    public IGameService GameService { get; set; }
    private readonly IGameExit _gameExit;
    private readonly IOpenGlService _platformOpenGl;
    private readonly ISinglePlayerService _singlePlayerService;
    private readonly IPreferences _preferences;

    /// <summary>Loaded localisation/translation data.</summary>
    private LanguageService _lang;

    private int viewportWidth;
    private int viewportHeight;

    private Matrix4 mvMatrix;
    private Matrix4 pMatrix;

    private bool[] currentlyPressedKeys;

    private ScreenBase screen;
    private GeometryModel cubeModel;

    // Background animation
    private float xRot;
    private bool xInv;
    private float xSpeed;

    private float yRot;
    private bool yInv;
    private float ySpeed;

    private int overlap;
    private int minspeed;
    private Random rnd;

    private int filter;
    private bool initialized;

    // Touch tracking
    private int touchId;
    private int previousTouchX;
    private int previousTouchY;

    // Texture caches
    /// <summary>GPU texture cache: filename → OpenGL texture ID.</summary>
    public Dictionary<string, int> Textures { get; set; }
    public string[] GameArgs { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    /// <summary>
    /// Rasterised text texture cache: (text, fontSize) → <see cref="TextTexture"/>.
    /// Keyed by both string content and size so different sizes of the same text are cached independently.
    /// </summary>
    private readonly Dictionary<(string Text, float Size), TextTexture> textTextureCache;

    private readonly LoginClientCi loginClient;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public MainMenu(IGameService platform, IOpenGlService platformOpenGl, ISinglePlayerService singlePlayerService, IPreferences preferences, IGameExit gameExit)
    {
        GameService = platform;
        _platformOpenGl = platformOpenGl;
        _singlePlayerService = singlePlayerService;
        _preferences = preferences;
        _gameExit = gameExit;
        Textures = [];
        textTextureCache = [];
        screen = new MainScreen(this, GameService, singlePlayerService);
        loginClient = new LoginClientCi();
        Assets = [];
    }

    // -------------------------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Bootstraps the main menu: loads translations, triggers async asset loading,
    /// initialises background animation state, and registers all platform callbacks.
    /// Must be called once before the platform's main loop starts.
    /// </summary>
    /// <param name="platform">The platform implementation to bind to.</param>
    public void Start()
    {
        _lang = new LanguageService();
        _lang.LoadTranslations();
        GameService.SetTitle(_lang.GameName());

        TextColorRenderer = new TextColorRenderer();
        var assetLoader = new AssetLoader([PathHelper.DataRoot, "data"]);
        Assets = assetLoader.LoadAssetsAsync(out float progress);
        AssetsLoadProgress = progress;

        overlap = 200;
        minspeed = 20;
        rnd = new Random();

        xRot = 0;
        xInv = false;
        xSpeed = minspeed + rnd.Next(5);

        yRot = 0;
        yInv = false;
        ySpeed = minspeed + rnd.Next(5);

        filter = 0;

        mvMatrix = Matrix4.Identity;
        pMatrix = Matrix4.Identity;

        currentlyPressedKeys = new bool[360];

        GameService.AddOnNewFrame(OnNewFrame);
        GameService.AddOnKeyEvent(HandleKeyDown, HandleKeyUp, HandleKeyPress);
        GameService.AddOnMouseEvent(HandleMouseDown, HandleMouseUp, HandleMouseMove, HandleMouseWheel);
        GameService.AddOnTouchEvent(HandleTouchStart, HandleTouchMove, HandleTouchEnd);
    }

    // -------------------------------------------------------------------------
    // Per-frame update
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called once per rendered frame. Lazily initialises GL state on the first
    /// invocation, then updates viewport dimensions, draws the scene, advances
    /// background animation, and pumps the login client.
    /// </summary>
    /// <param name="dt">Elapsed time since the last frame, in seconds.</param>
    public void OnNewFrame(float dt)
    {
        if (!initialized)
        {
            initialized = true;
            _platformOpenGl.InitShaders();
            _platformOpenGl.GlClearColorRgbaf(0, 0, 0, 1);
            _platformOpenGl.GlEnableDepthTest();
        }

        viewportWidth = GameService.CanvasWidth;
        viewportHeight = GameService.CanvasHeight;

        DrawScene(dt);
        Animate(dt);
    }

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    /// <summary>
    /// Clears the framebuffer, sets up an orthographic projection matching the
    /// current canvas size, and delegates to the active screen's <c>Render</c>.
    /// </summary>
    /// <param name="dt">Delta time forwarded to the screen renderer.</param>
    private void DrawScene(float dt)
    {
        _platformOpenGl.GlViewport(0, 0, viewportWidth, viewportHeight);
        _platformOpenGl.GlClearColorBufferAndDepthBuffer();
        _platformOpenGl.GlDisableDepthTest();
        _platformOpenGl.GlDisableCullFace();

        Matrix4.CreateOrthographicOffCenter(
            0, GameService.CanvasWidth,
            GameService.            CanvasHeight, 0,
            0, 10,
            out pMatrix);

        screen.Render(dt);
    }

    /// <summary>
    /// Draws a textured button quad and, when non-empty, centres the label text over it.
    /// </summary>
    /// <param name="text">Label text. Null or empty skips text rendering.</param>
    /// <param name="fontSize">Font size in points.</param>
    /// <param name="dx">Left edge of the button in canvas pixels.</param>
    /// <param name="dy">Top edge of the button in canvas pixels.</param>
    /// <param name="dw">Button width in pixels.</param>
    /// <param name="dh">Button height in pixels.</param>
    /// <param name="pressed">When <c>true</c>, uses the pressed/highlighted button sprite.</param>
    public void DrawButton(string text, float fontSize, float dx, float dy, float dw, float dh, bool pressed)
    {
        string textureName = pressed ? "button_sel.png" : "button.png";
        Draw2dQuad(GetTexture(textureName), dx, dy, dw, dh);

        if (!string.IsNullOrEmpty(text))
        {
            DrawText(text, fontSize, dx + dw / 2, dy + dh / 2, TextAlign.Center, TextBaseline.Middle);
        }
    }

    /// <summary>
    /// Renders a string at the given canvas position using a cached rasterised texture.
    /// </summary>
    /// <param name="text">The string to render.</param>
    /// <param name="fontSize">Font size in points.</param>
    /// <param name="x">Horizontal anchor in canvas pixels.</param>
    /// <param name="y">Vertical anchor in canvas pixels.</param>
    /// <param name="align">Horizontal alignment relative to <paramref name="x"/>.</param>
    /// <param name="baseline">Vertical alignment relative to <paramref name="y"/>.</param>
    public void DrawText(string text, float fontSize, float x, float y, TextAlign align, TextBaseline baseline)
    {
        TextTexture t = GetTextTexture(text, fontSize);

        int dx = align switch
        {
            TextAlign.Center => -t.TextWidth / 2,
            TextAlign.Right => -t.TextWidth,
            _ => 0
        };

        int dy = baseline switch
        {
            TextBaseline.Middle => -t.TextHeight / 2,
            TextBaseline.Bottom => -t.TextHeight,
            _ => 0
        };

        Draw2dQuad(t.Texture, x + dx, y + dy, t.TextureWidth, t.TextureHeight);
    }

    /// <summary>
    /// Draws a server-list entry consisting of a background panel, a thumbnail icon,
    /// and four text labels (name, motd, gamemode, player count).
    /// </summary>
    public void DrawServerButton(
        string name, string motd, string gamemode, string playercount,
        float x, float y, float width, float height, string image)
    {
        // Background panel (screen-width minus margin × 64 px by convention)
        Draw2dQuad(GetTexture("serverlist_entry_background.png"), x, y, width, height);
        // Square icon on the left; height × height keeps it 1:1
        Draw2dQuad(GetTexture(image), x, y, height, height);

        //         value          size   xPos                    yPos                    align               baseline
        DrawText(name, 14, x + 70, y + 5, TextAlign.Left, TextBaseline.Top);
        DrawText(gamemode, 12, x + width - 10, y + height - 5, TextAlign.Right, TextBaseline.Bottom);
        DrawText(playercount, 12, x + width - 10, y + 5, TextAlign.Right, TextBaseline.Top);
        DrawText(motd, 12, x + 70, y + height - 5, TextAlign.Left, TextBaseline.Bottom);
    }

    /// <summary>
    /// Tiles the scrolling background texture to fill the canvas, accounting for
    /// the animated pan offset and bleed <see cref="overlap"/>.
    /// </summary>
    public void DrawBackground()
    {
        BackgroundW = BackgroundTileSize;
        BackgroundH = BackgroundTileSize;
        WindowX = GameService.CanvasWidth;
        WindowY = GameService.CanvasHeight;

        int countX = (int)((WindowX + 2 * overlap) / BackgroundW) + 1;
        int countY = (int)((WindowY + 2 * overlap) / BackgroundH) + 1;

        int bgTexture = GetTexture("background.png");
        for (int x = 0; x < countX; x++)
        {
            for (int y = 0; y < countY; y++)
            {
                Draw2dQuad(
                    bgTexture,
                    x * BackgroundW + xRot - overlap,
                    y * BackgroundH + yRot - overlap,
                    BackgroundW, BackgroundH);
            }
        }
    }

    /// <summary>
    /// Renders a screen-aligned quad scaled to <paramref name="dw"/> × <paramref name="dh"/>
    /// and positioned at (<paramref name="dx"/>, <paramref name="dy"/>) in canvas space.
    /// The quad geometry is lazily created and reused across all calls.
    /// </summary>
    public void Draw2dQuad(int textureid, float dx, float dy, float dw, float dh)
    {
        // Build model-view: translate → scale to size → halve → shift origin to centre
        Matrix4.CreateTranslation(dx, dy, 0, out Matrix4 translation);
        Matrix4.CreateScale(dw, dh, 0, out Matrix4 scale);
        Matrix4.CreateScale(0.5f, 0.5f, 0, out Matrix4 halfScale);
        Matrix4.CreateTranslation(1, 1, 0, out Matrix4 centreShift);

        mvMatrix = centreShift * halfScale * scale * translation;

        SetMatrixUniforms();
        cubeModel ??= _platformOpenGl.CreateModel(Quad.Create());
        _platformOpenGl.BindTexture2d(textureid);
        _platformOpenGl.DrawModel(cubeModel);
    }

    // -------------------------------------------------------------------------
    // Animation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Advances the background parallax scroll animation. Each axis bounces
    /// between [−overlap, +overlap] at a randomly varied speed.
    /// Delta time is clamped to <see cref="MaxDeltaTime"/> to prevent large jumps
    /// after frame hitches.
    /// </summary>
    private void Animate(float dt)
    {
        dt = Math.Min(dt, MaxDeltaTime);

        // X axis
        if (xInv)
        {
            if (xRot <= -overlap)
            {
                xInv = false;
                xSpeed = minspeed + rnd.Next(5);
            }
            xRot -= xSpeed * dt;
        }
        else
        {
            if (xRot >= overlap)
            {
                xInv = true;
                xSpeed = minspeed + rnd.Next(5);
            }
            xRot += xSpeed * dt;
        }

        // Y axis
        if (yInv)
        {
            if (yRot <= -overlap)
            {
                yInv = false;
                ySpeed = minspeed + rnd.Next(5);
            }
            yRot -= ySpeed * dt;
        }
        else
        {
            if (yRot >= overlap)
            {
                yInv = true;
                ySpeed = minspeed + rnd.Next(5);
            }
            yRot += ySpeed * dt;
        }
    }

    // -------------------------------------------------------------------------
    // Matrix helpers
    // -------------------------------------------------------------------------

    /// <summary>Uploads the current projection and model-view matrices to the active shader.</summary>
    private void SetMatrixUniforms()
    {
        _platformOpenGl.SetMatrixUniformProjection(ref pMatrix);
        _platformOpenGl.SetMatrixUniformModelView(ref mvMatrix);
    }

    // -------------------------------------------------------------------------
    // Texture / asset helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the OpenGL texture ID for <paramref name="name"/>, loading and caching
    /// it from the asset list on first access.
    /// </summary>
    /// <param name="name">Asset filename (case-insensitive).</param>
    public int GetTexture(string name)
    {
        if (!Textures.TryGetValue(name, out int value))
        {
            Bitmap bmp = PixelBuffer.BitmapFromPng(GetFile(name), GetFileLength(name));
            value = _platformOpenGl.LoadTextureFromBitmap(bmp);
            Textures[name] = value;
            bmp.Dispose();
        }
        return value;
    }

    /// <summary>
    /// Registers a pre-loaded GPU texture in the renderer's cache under the given name,
    /// making it retrievable via <see cref="IMenuRenderer.GetTexture"/> without a
    /// redundant disk or asset lookup.
    /// </summary>
    /// <param name="name">
    /// The cache key, conventionally the asset filename (e.g. <c>"serverlist_entry_abc123.png"</c>).
    /// Must be unique; an existing entry under the same name will be overwritten.
    /// </param>
    /// <param name="textureId">The OpenGL texture ID returned by the platform after uploading the bitmap.</param>
    public void RegisterTexture(string name, int textureId) => Textures[name] = textureId;

    /// <summary>
    /// Returns the raw bytes for the named asset, or <c>null</c> if not found.
    /// Comparison is case-insensitive via <see cref="string.ToLowerInvariant"/>.
    /// </summary>
    /// <param name="name">Asset filename.</param>
    public byte[] GetFile(string name)
    {
        string lower = name.ToLowerInvariant();
        for (int i = 0; i < Assets.Count; i++)
        {
            if (Assets[i].name == lower)
                return Assets[i].data;
        }
        return null;
    }

    /// <summary>
    /// Returns the byte length of the named asset's data, or 0 if not found.
    /// </summary>
    /// <param name="name">Asset filename.</param>
    public int GetFileLength(string name)
    {
        string lower = name.ToLowerInvariant();
        for (int i = 0; i < Assets.Count; i++)
        {
            if (Assets[i].name == lower)
                return Assets[i].dataLength;
        }
        return 0;
    }

    /// <summary>
    /// Returns a cached <see cref="TextTexture"/> for the given text and size,
    /// rasterising and uploading a new one to the GPU if necessary.
    /// </summary>
    private TextTexture GetTextTexture(string text, float fontSize)
    {
        if (textTextureCache.TryGetValue((text, fontSize), out TextTexture cached))
            return cached;

        TextStyle style = new()
        {
            Text = text,
            FontSize = fontSize,
            FontFamily = "Arial",
            Color = ColorUtils.ColorFromArgb(255, 255, 255, 255)
        };

        Bitmap textBitmap = TextColorRenderer.CreateTextTexture(style);
        int texture = _platformOpenGl.LoadTextureFromBitmap(textBitmap);
        TextRenderer.TextSize(text, fontSize, out int textWidth, out int textHeight);

        TextTexture entry = new()
        {
            Texture = texture,
            TextureWidth = textBitmap.Width,
            TextureHeight = textBitmap.Height,
            Text = text,
            Size = fontSize,
            TextWidth = textWidth,
            TextHeight = textHeight
        };

        textBitmap.Dispose();
        textTextureCache[(text, fontSize)] = entry;
        return entry;
    }

    // -------------------------------------------------------------------------
    // Scale helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a UI scale factor. On small/mobile screens this scales proportionally
    /// to a 1280-pixel reference width; on desktop it returns exactly 1.
    /// </summary>
    public float GetScale() =>
        GameService.IsSmallScreen()
            ? GameService.CanvasWidth / 1280f
            : 1f;

    // -------------------------------------------------------------------------
    // Screen navigation
    // -------------------------------------------------------------------------

    /// <summary>Navigates to the main (home) screen and releases any mouse pointer lock.</summary>
    public void StartMainMenu()
    {
        screen = new MainScreen(this, GameService, default);
        GameService.ExitMousePointerLock();
    }

    /// <summary>Navigates to the single-player world selection screen.</summary>
    public void StartSingleplayer()
    {
        screen = new SingleplayerScreen(this, GameService, _singlePlayerService);
        screen.LoadTranslations();
    }

    /// <summary>Navigates to the multiplayer server-browser screen.</summary>
    public void StartMultiplayer()
    {
        screen = new MultiplayerScreen(this, GameService, default, default);
        screen.LoadTranslations();
    }

    /// <summary>
    /// Navigates to the login screen pre-populated with the target server details.
    /// </summary>
    /// <param name="serverHash">Server authentication hash.</param>
    /// <param name="ip">Server IP address.</param>
    /// <param name="port">Server port number.</param>
    public void StartLogin(string serverHash, string ip, int port)
    {
        screen = new LoginScreen(this, GameService)
        {
            serverHash = serverHash,
            serverIp = ip,
            serverPort = port,
        };
        screen.LoadTranslations();
    }

    /// <summary>Navigates to the direct-connect / manual IP entry screen.</summary>
    public void StartConnectToIp()
    {
        screen = new ConnectionScreen(this, GameService, _preferences);
        screen.LoadTranslations();
    }

    /// <summary>
    /// Creates a <see cref="ScreenGame"/>, starts it in single- or multi-player mode,
    /// and makes it the active screen.
    /// </summary>
    /// <param name="singleplayer"><c>true</c> to start a local server; <c>false</c> for a remote connection.</param>
    /// <param name="singleplayerSavePath">Path to the save file; ignored when <paramref name="singleplayer"/> is <c>false</c>.</param>
    /// <param name="connectData">Remote connection parameters; ignored when <paramref name="singleplayer"/> is <c>true</c>.</param>
    public void StartGame(bool singleplayer, string singleplayerSavePath, ConnectionData connectData)
    {
        ScreenGame screenGame = new(this, GameService, _platformOpenGl, _singlePlayerService, _preferences, _gameExit);
        screenGame.Start(singleplayer, singleplayerSavePath, connectData);
        screen = screenGame;
    }

    /// <summary>
    /// Builds a <see cref="ConnectionData"/> from a successful login response and
    /// immediately transitions to the game screen.
    /// </summary>
    public void ConnectToGame(LoginData loginResultData, string username)
    {
        ConnectionData connectData = new()
        {
            Ip = loginResultData.ServerAddress,
            Port = loginResultData.Port,
            Auth = loginResultData.AuthCode,
            Username = username
        };
        StartGame(false, null, connectData);
    }

    /// <summary>Starts a single-player session using an existing save file.</summary>
    /// <param name="filename">Absolute path to the <c>.mddbs</c> save file.</param>
    public void ConnectToSingleplayer(string filename) =>
        StartGame(true, filename, null);

    // -------------------------------------------------------------------------
    // Login / account helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initiates a login request. Returns early (setting <paramref name="loginResult"/>
    /// to <see cref="LoginResult.Failed"/>) when credentials are incomplete.
    /// </summary>
    /// <param name="user">Username.</param>
    /// <param name="password">Password; may be empty when <paramref name="token"/> is provided.</param>
    /// <param name="serverHash">Server authentication hash.</param>
    /// <param name="token">Session token; may be empty when <paramref name="password"/> is provided.</param>
    /// <param name="loginResult">Receives the immediate outcome (may change asynchronously via <paramref name="loginResultData"/>).</param>
    /// <param name="loginResultData">Populated with server address and auth code on success.</param>
    public void Login(
        string user, string password,
        string serverHash, string token,
        LoginResult loginResult, LoginData loginResultData)
    {
        if (string.IsNullOrEmpty(user) || (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(token)))
        {
            loginResult = LoginResult.Failed;
            return;
        }
        loginClient.Login(GameService, user, password, serverHash, token, loginResult, loginResultData);
    }

    /// <summary>
    /// Validates account creation inputs. Currently only performs local validation;
    /// actual account creation is not yet implemented.
    /// </summary>
    /// <returns><see cref="LoginResult.Failed"/> on invalid input; <see cref="LoginResult.Ok"/> otherwise.</returns>
    public static LoginResult CreateAccount(string user, string password)
    {
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
            return LoginResult.Failed;

        return LoginResult.Ok;
    }

    /// <summary>Placeholder for new-world creation workflow. Not yet implemented.</summary>
    public static void StartNewWorld() { }

    /// <summary>Placeholder for world modification workflow. Not yet implemented.</summary>
    public static void StartModifyWorld() { }

    // -------------------------------------------------------------------------
    // Input handlers
    // -------------------------------------------------------------------------

    /// <summary>Records the key as pressed and forwards the event to the active screen.</summary>
    public void HandleKeyDown(KeyEventArgs e)
    {
        currentlyPressedKeys[e.KeyChar] = true;
        screen.OnKeyDown(e);
    }

    /// <summary>Records the key as released and forwards the event to the active screen.</summary>
    public void HandleKeyUp(KeyEventArgs e)
    {
        currentlyPressedKeys[e.KeyChar] = false;
        screen.OnKeyUp(e);
    }

    /// <summary>
    /// Handles character-level key events: 'F'/'f' cycles the texture filter,
    /// backtick triggers back-navigation, then the event is forwarded to the active screen.
    /// </summary>
    public void HandleKeyPress(KeyPressEventArgs e)
    {
        if (e.KeyChar is KeyF_Upper or KeyF_Lower)
        {
            filter = (filter + 1) % FilterModeCount;
        }

        if (e.KeyChar == KeyBacktick)
        {
            screen.OnBackPressed();
        }

        screen.OnKeyPress(e);
    }

    /// <summary>Records a mouse-button press, caches the initial position, and forwards to the active screen.</summary>
    public void HandleMouseDown(MouseEventArgs e)
    {
        screen.OnMouseDown(e);
    }

    /// <summary>Records a mouse-button release and forwards to the active screen.</summary>
    public void HandleMouseUp(MouseEventArgs e)
    {
        screen.OnMouseUp(e);
    }

    /// <summary>
    /// Tracks the cursor position and forwards to the active screen.
    /// </summary>
    public void HandleMouseMove(MouseEventArgs e)
    {
        screen.OnMouseMove(e);
    }

    /// <summary>Forwards mouse-wheel events to the active screen.</summary>
    public void HandleMouseWheel(MouseWheelEventArgs e) =>
        screen.OnMouseWheel(e);

    /// <summary>Records the initiating touch contact and forwards to the active screen.</summary>
    public void HandleTouchStart(TouchEventArgs e)
    {
        touchId = e.GetId();
        previousTouchX = e.GetX();
        previousTouchY = e.GetY();
        screen.OnTouchStart(e);
    }

    /// <summary>
    /// Forwards touch-move to the active screen, then — for the tracked contact —
    /// applies the delta to the background scroll speed.
    /// </summary>
    public void HandleTouchMove(TouchEventArgs e)
    {
        screen.OnTouchMove(e);

        if (e.GetId() != touchId)
            return;

        float dx = e.GetX() - previousTouchX;
        float dy = e.GetY() - previousTouchY;

        previousTouchX = e.GetX();
        previousTouchY = e.GetY();

        ySpeed += dx / 10;
        xSpeed += dy / 10;
    }

    /// <summary>Forwards touch-end events to the active screen.</summary>
    public void HandleTouchEnd(TouchEventArgs e) =>
        screen.OnTouchEnd(e);
}

// =============================================================================
// Supporting types
// =============================================================================

/// <summary>
/// Holds a rasterised text string as an uploaded GPU texture together with the
/// layout metrics needed to position and align it during rendering.
/// </summary>
public class TextTexture
{
    /// <summary>Font size (in points) used when this texture was rasterised.</summary>
    public float Size { get; set; }

    /// <summary>The string that was rasterised into this texture.</summary>
    public string Text { get; set; }

    /// <summary>OpenGL texture ID.</summary>
    public int Texture { get; set; }

    /// <summary>Width of the uploaded bitmap in texels (may include padding).</summary>
    public int TextureWidth { get; set; }

    /// <summary>Height of the uploaded bitmap in texels (may include padding).</summary>
    public int TextureHeight { get; set; }

    /// <summary>Typographic width of the text (no padding), used for alignment calculations.</summary>
    public int TextWidth { get; set; }

    /// <summary>Typographic height of the text (no padding), used for alignment calculations.</summary>
    public int TextHeight { get; set; }
}

/// <summary>Represents the outcome of a login or account-creation attempt.</summary>
public enum LoginResult
{
    /// <summary>No attempt has been made yet.</summary>
    None,

    /// <summary>A request is in-flight.</summary>
    Connecting,

    /// <summary>The attempt was rejected or input was invalid.</summary>
    Failed,

    /// <summary>The attempt succeeded.</summary>
    Ok
}

/// <summary>
/// Carries the raw HTTP response from a server-thumbnail request,
/// including the image bytes and any error state.
/// </summary>
public class ThumbnailResponseCi
{
    /// <summary><c>true</c> once the request has completed (successfully or not).</summary>
    public bool Done { get; set; }

    /// <summary><c>true</c> if the request failed; check <see cref="ServerMessage"/> for details.</summary>
    public bool Error { get; set; }

    /// <summary>Optional message returned by the server alongside the result.</summary>
    public string ServerMessage { get; set; }

    /// <summary>Raw PNG or JPEG bytes of the thumbnail, or <c>null</c> on error.</summary>
    public byte[] Data { get; set; }
}