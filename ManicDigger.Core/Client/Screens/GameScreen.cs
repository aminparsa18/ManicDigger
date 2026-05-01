using OpenTK.Windowing.Common;
using Serilog;

/// <summary>
/// Menu screen that owns and drives the active <see cref="Game"/> session.
/// Bridges platform input events to the game, manages the singleplayer
/// embedded server lifecycle, and handles reconnect / exit-to-menu transitions.
/// </summary>
public class ScreenGame(IMenu navigator, IGameService platform,
    ISinglePlayerService singlePlayerService, IPreferences preferences, IGameExit gameExit,
    IDummyNetwork dummyNetwork,  IGame game) : ScreenBase(navigator, platform)
{
    /// <summary>The game instance owned by this screen.</summary>
    private readonly IGame game = game;
    private ConnectionData connectData;
    private bool singleplayer;
    private string singleplayerSavePath;
    private readonly IGameExit gameExit = gameExit;
    private readonly IDummyNetwork dummyNetwork = dummyNetwork;

    /// <summary>
    /// Initialises the game with the given connection parameters and starts the
    /// network session. Must be called before the screen becomes active.
    /// </summary>
    /// <param name="singleplayer_">
    /// <see langword="true"/> to start an embedded singleplayer session;
    /// <see langword="false"/> to connect to a remote server via <paramref name="connectData_"/>.
    /// </param>
    /// <param name="singleplayerSavePath_">Path to the singleplayer save directory.</param>
    /// <param name="connectData_">Remote server address and credentials (multiplayer only).</param>
    public void Start(bool singleplayer_, string singleplayerSavePath_, ConnectionData connectData_)
    {
        singleplayer = singleplayer_;
        singleplayerSavePath = singleplayerSavePath_;
        connectData = connectData_;

        game.IsSinglePlayer = singleplayer;
        game.Assets = Menu.Assets;
        game.AssetsLoadProgress = Menu.AssetsLoadProgress;

        game.Start();
        Connect();
    }

    /// <summary>
    /// Sets up the network transport and, for singleplayer, the embedded server.
    /// </summary>
    private void Connect()
    {
        if (singleplayer)
        {
            IDummyNetwork network = singlePlayerService.SinglePlayerServerNetwork;

            // Platform provides its own singleplayer server (e.g. mobile).
            Task.Run(() =>
            {
                    ServerThreadStart(singleplayerSavePath);
            });

            // Prime the server inbox so the handshake starts immediately.
            network.ServerInbox.Enqueue([]);
            game.NetClient = new DummyNetClient(network);
            game.ConnectData = connectData = new ConnectionData { Username = "Local" };
        }
        else
        {
            game.ConnectData = connectData;
            game.NetClient = CreateNetClient()
                ?? throw new InvalidOperationException("No network transport available.");
        }
    }

    private void ServerThreadStart(string singleplayerSavePath)
    {
        Log.Debug("Single-player server thread started");
        DummyNetServer netServer = new(dummyNetwork);

        Server server = new(gameExit, GameService)
        {
            SaveFilenameOverride = singleplayerSavePath,
            MainSockets = new NetServer[3]
        };
        server.MainSockets[0] = netServer;

        while (true)
        {
            server.Process();
            Thread.Sleep(1);
            singlePlayerService.SinglePlayerServerLoaded = true;

            if (gameExit?.Exit == true)
            {
                server.Stop();
                break;
            }

            if (singlePlayerService.SinglePlayerServerExit)
            {
                server.Exit();
                singlePlayerService.SinglePlayerServerExit = false;
            }
        }

        gameExit.Exit = false;
        Log.Debug("Single-player server thread stopped cleanly");
    }

    /// <summary>
    /// Returns the first available <see cref="NetClient"/> transport supported by
    /// the platform, in priority order: TCP → ENet → WebSocket.
    /// Returns <see langword="null"/> if no transport is available.
    /// </summary>
    private NetClient? CreateNetClient()
    {
        if (GameService.NetworkService.TcpAvailable()) return new TcpNetClient();
        if (GameService.NetworkService.EnetAvailable()) return new EnetNetClient(GameService.NetworkService);
        if (GameService.NetworkService.WebSocketAvailable()) return new WebSocketNetClient();
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Checks the game's reconnect and exit-to-menu flags before delegating to
    /// <see cref="Game.OnRenderFrame"/>. Reconnects restart the game in place.
    /// Exits trigger the redirect handshake when the server requests a redirect,
    /// or navigate back to the main menu otherwise.
    /// </remarks>
    public override void Render(float dt)
    {
        if (game.IsReconnecting)
        {
            game.Dispose();
            Menu.StartGame(singleplayer, singleplayerSavePath, connectData);
            return;
        }

        if (game.IsExitingToMainMenu)
        {
            game.Dispose();
            HandleExit();
            return;
        }

        game.OnRenderFrame(dt);
    }

    /// <summary>
    /// Handles the exit-to-menu transition. If the server issued a redirect,
    /// queries the target server and re-authenticates before connecting.
    /// Otherwise returns directly to the main menu.
    /// </summary>
    private void HandleExit()
    {
        var redirect = game.Redirect;

        if (redirect == null)
        {
            Menu.StartMainMenu();
            return;
        }

        // Synchronously query the redirect target.
        // NOTE: This is a deliberate sync-over-async call on the render thread;
        // the game loop is already stopped at this point so blocking is acceptable.
        var (qresult, message) = Task.Run(() =>
            new QueryClient(GameService.NetworkService).QueryAsync(redirect.IP, redirect.Port)
        ).GetAwaiter().GetResult();

        if (qresult == null)
        {
            platform.MessageBoxShowError(message, "Redirection error");
            Menu.StartMainMenu();
            return;
        }

        LoginClientCi lic = new();
        LoginData lidata = new();
        string token = qresult.PublicHash.Split('=')[1];

        lic.Login(platform, connectData.Username, "",
            token, preferences.GetString("Password", ""),
            new LoginResult(), lidata);

        if (!lidata.ServerCorrect)
        {
            platform.MessageBoxShowError("Invalid server address!", "Redirection error!");
            Menu.StartMainMenu();
        }
        else if (!lidata.PasswordCorrect)
        {
            Menu.StartLogin(token, null, 0);
        }
        else if (!string.IsNullOrEmpty(lidata.ServerAddress))
        {
            Menu.ConnectToGame(lidata, connectData.Username);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the platform window has focus.
    /// Mouse events are suppressed while focus is lost to avoid unintended actions.
    /// </summary>
    private bool IsFocused => GameService.Focused();

    public override void OnKeyDown(KeyEventArgs e) => game.KeyDown(e);
    public override void OnKeyUp(KeyEventArgs e) => game.KeyUp(e);
    public override void OnKeyPress(KeyPressEventArgs e) => game.KeyPress(e);
    public override void OnMouseWheel(MouseWheelEventArgs e) => game.MouseWheelChanged(e);
    public override void OnTouchStart(TouchEventArgs e) => game.OnTouchStart(e);
    public override void OnTouchMove(TouchEventArgs e) => game.OnTouchMove(e);
    public override void OnTouchEnd(TouchEventArgs e) => game.OnTouchEnd(e);
    public override void OnBackPressed() => Game.OnBackPressed();

    public override void OnMouseDown(MouseEventArgs e)
    {
        if (IsFocused) { game.MouseDown(e); }
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        if (IsFocused) { game.MouseMove(e); }
    }

    public override void OnMouseUp(MouseEventArgs e)
    {
        if (IsFocused) { game.MouseUp(e); }
    }
}