/// <summary>
/// The top-level main menu screen. Displays the game logo and the three primary
/// navigation buttons: Singleplayer, Multiplayer, and Quit.
/// </summary>
/// <remarks>
/// On first render (after assets finish loading) the screen checks for an
/// <c>?ip=&amp;port=</c> query string and auto-connects if one is present.
/// </remarks>
public class MainScreen : ScreenBase
{
    public MainScreen(IMenuRenderer renderer, IMenuNavigator navigator, IGameService platform, ISinglePlayerService singlePlayerService, IPreferences preferences)
        : base(renderer, navigator, platform, default, singlePlayerService)
    {
        buttonSingleplayer = new MenuWidget { text = "Singleplayer" };
        buttonMultiplayer = new MenuWidget { text = "Multiplayer" };
        buttonExit = new MenuWidget { text = "Quit" };

        Widgets.Add(buttonSingleplayer);
        Widgets.Add(buttonMultiplayer);
        Widgets.Add(buttonExit);
    }

    private readonly MenuWidget buttonSingleplayer;
    private readonly MenuWidget buttonMultiplayer;
    private readonly MenuWidget buttonExit;

    /// <summary>Current canvas width, updated every frame. Exposed for external layout queries.</summary>
    internal float windowX;

    /// <summary>Current canvas height, updated every frame. Exposed for external layout queries.</summary>
    internal float windowY;

    private bool queryStringChecked;
    private bool cursorLoaded;

    private const int ButtonHeight = 64;
    private const int ButtonWidth = 256;
    private const int SpaceBetween = 5;
    private const int OffsetFromBorder = 50;

    /// <inheritdoc/>
    public override void LoadTranslations()
    {
        buttonSingleplayer.text = Renderer.Translate("MainMenu_Singleplayer");
        buttonMultiplayer.text = Renderer.Translate("MainMenu_Multiplayer");
        buttonExit.text = Renderer.Translate("MainMenu_Quit");
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        windowX = Platform.CanvasWidth;
        windowY = Platform.CanvasHeight;

        float scale = Renderer.GetScale();

        if (Renderer.AssetsLoadProgress != 1)
        {
            string s = string.Format(Renderer.Translate("MainMenu_AssetsLoadProgress"),
                ((int)(Renderer.AssetsLoadProgress * 100)).ToString());
            Renderer.DrawText(s, 20 * scale, windowX / 2, windowY / 2, TextAlign.Center, TextBaseline.Middle);
            return;
        }

        if (!cursorLoaded)
        {
            Platform.SetWindowCursor(0, 0, 32, 32,
                Renderer.GetFile("mousecursor.png"),
                Renderer.GetFileLength("mousecursor.png"));
            cursorLoaded = true;
        }

        UseQueryStringIpAndPort();

        Renderer.DrawBackground();
        Renderer.Draw2dQuad(Renderer.GetTexture("logo.png"),
            windowX / 2 - 1024 * scale / 2, 0, 1024 * scale, 512 * scale);

        float centerX = windowX / 2 - ButtonWidth / 2 * scale;

        buttonSingleplayer.x = centerX;
        buttonSingleplayer.y = ButtonY(3, scale);
        buttonSingleplayer.sizex = ButtonWidth * scale;
        buttonSingleplayer.sizey = ButtonHeight * scale;

        buttonMultiplayer.x = centerX;
        buttonMultiplayer.y = ButtonY(2, scale);
        buttonMultiplayer.sizex = ButtonWidth * scale;
        buttonMultiplayer.sizey = ButtonHeight * scale;

        buttonExit.visible = true;
        buttonExit.x = centerX;
        buttonExit.y = ButtonY(1, scale);
        buttonExit.sizex = ButtonWidth * scale;
        buttonExit.sizey = ButtonHeight * scale;

        DrawWidgets();
    }

    /// <summary>
    /// Returns the Y position for a button at the given slot (1 = bottom, 2 = above that, etc.)
    /// measured from the bottom of the canvas.
    /// </summary>
    private float ButtonY(int slot, float scale)
        => windowY - slot * (ButtonHeight * scale + SpaceBetween) - OffsetFromBorder * scale;

    /// <summary>
    /// On the first call, checks the page query string for <c>ip</c> and <c>port</c>
    /// parameters and auto-starts a login if an IP is found. Subsequent calls are no-ops.
    /// </summary>
    private void UseQueryStringIpAndPort()
    {
        if (queryStringChecked) { return; }
        queryStringChecked = true;

        string ip = Platform.QueryStringValue("ip");
        string port = Platform.QueryStringValue("port");

        int portInt = int.TryParse(port, out int parsedPort) ? parsedPort : 25565;

        if (ip != null)
        {
            Navigator.StartLogin(null, ip, portInt);
        }
    }

    /// <inheritdoc/>
    public override void OnButton(MenuWidget w)
    {
        if (w == buttonSingleplayer) { Navigator.StartSingleplayer(); return; }
        if (w == buttonMultiplayer) { Navigator.StartMultiplayer(); return; }
        if (w == buttonExit) { Environment.Exit(0); return; }
    }

    /// <inheritdoc/>
    public override void OnBackPressed() => Environment.Exit(0);

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
#if DEBUG
        // F5 — launch default singleplayer save (legacy .mdss format).
        if (e.KeyChar == (int)Keys.F5)
        {
            SinglePlayerService.SinglePlayerServerDisable();
            Navigator.StartGame(true, Path.Combine(Platform.GameSavePath, "Default.mdss"), null);
        }
        // F6 — launch default singleplayer save (database .mddbs format).
        if (e.KeyChar == (int)Keys.F6)
        {
            Navigator.StartGame(true, Path.Combine(Platform.GameSavePath, "Default.mddbs"), null);
        }
#endif
    }
}