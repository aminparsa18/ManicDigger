using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Application = Microsoft.Maui.Controls.Application;
using Grid = Microsoft.UI.Xaml.Controls.Grid;

namespace ManicDigger.Maui.Platforms.Windows;

/// <summary>
/// Windows handler for GameSurfaceView.
/// Creates a native Panel in WinUI3 that hosts the game platform,
/// replacing the old GameWindowNative (WinForms Form).
/// </summary>
public class GameSurfaceViewHandler : ViewHandler<GameSurfaceView, Grid>
{
    // -----------------------------------------------------------------------
    // Static mapper — maps virtual view properties → handler calls
    // -----------------------------------------------------------------------
    public static PropertyMapper<GameSurfaceView, GameSurfaceViewHandler> GameMapper =
        new(ViewMapper);

    public GameSurfaceViewHandler() : base(GameMapper) { }

    // -----------------------------------------------------------------------
    // Game objects (mirrors what Program.cs used to own)
    // -----------------------------------------------------------------------
    private DummyNetwork? _dummyNetwork;
    private GamePlatformNative? _platform;
    private GameExit _exit = new();
    private string? _savefilename;

    // -----------------------------------------------------------------------
    // Handler lifecycle
    // -----------------------------------------------------------------------

    protected override Grid CreatePlatformView()
    {
        return new Grid
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.Colors.Black)
        };
    }

    protected override void ConnectHandler(Grid platformView)
    {
        base.ConnectHandler(platformView);

        // Wait until the panel is in the visual tree so we have an HWND
        platformView.Loaded += OnPanelLoaded;
    }

    protected override void DisconnectHandler(Grid platformView)
    {
        platformView.Loaded -= OnPanelLoaded;
        _exit.SetExit(true);
        base.DisconnectHandler(platformView);
    }

    // -----------------------------------------------------------------------
    // Game bootstrap — runs once the native panel has an HWND
    // -----------------------------------------------------------------------

    private void OnPanelLoaded(object sender, RoutedEventArgs e)
    {
        var panel = (Grid)sender;
        panel.Loaded -= OnPanelLoaded;

        Log.Debug("GameSurfaceViewHandler: panel loaded, bootstrapping game");

        // Notify the virtual view that the surface is ready
        VirtualView?.NotifySurfaceReady();

        // Bootstrap on a background thread so we don't block the UI thread.
        // The game's own message/render loop will run here.
        Task.Run(() => StartGame(panel));
    }

    private void StartGame(Grid panel)
    {
        try
        {
            _dummyNetwork = new DummyNetwork();

            _platform = new GamePlatformNative
            {
                crashreporter = new CrashReporter(),
                singlePlayerServerDummyNetwork = _dummyNetwork
            };
            _platform.SetExit(_exit);
            _platform.StartSinglePlayerServer = filename =>
            {
                _savefilename = filename;
                new Thread(ServerThreadStart) { IsBackground = true }.Start();
            };

            // Just new up GameWindowNative exactly like the old Program.cs did
            // OpenTK creates its own Win32 window — MAUI's window is irrelevant
            using GameWindowNative window = new();
            _platform.window = window;
            window.platform = _platform;

            var mainmenu = new MainMenu();
            mainmenu.Start(_platform);

            _platform.Start();
            window.Run(); // blocks here, OpenTK runs its own message pump
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Game bootstrap failed");
            // Surface the error on the UI thread
            MainThread.BeginInvokeOnMainThread(() =>
                Application.Current?.Windows[0]
                    .Page?.DisplayAlert("Fatal Error", ex.ToString(), "OK"));
        }
    }

    private static void ReadArgs(MainMenu mainmenu, string[] args)
    {
        if (args.Length > 0)
            mainmenu.StartGame(false, null, ConnectionData.FromUri(new Uri(args[0])));
    }

    // -----------------------------------------------------------------------
    // Single-player server thread (unchanged from Program.cs)
    // -----------------------------------------------------------------------

    private void ServerThreadStart()
    {
        Log.Debug("Single-player server thread started");

        var netServer = new DummyNetServer(_dummyNetwork!);
        var server = new Server
        {
            SaveFilenameOverride = _savefilename,
            exit = _exit,
            mainSockets = new NetServer[3]
        };
        server.mainSockets[0] = netServer;

        while (true)
        {
            server.Process();
            Thread.Sleep(1);
            _platform!.singlePlayerServerLoaded = true;

            if (_exit.GetExit())
            {
                server.Stop();
                break;
            }

            if (_platform.singlepLayerServerExit)
            {
                server.Exit();
                _platform.singlepLayerServerExit = false;
            }
        }

        _exit.SetExit(false);
        Log.Debug("Single-player server thread stopped cleanly");
    }
}