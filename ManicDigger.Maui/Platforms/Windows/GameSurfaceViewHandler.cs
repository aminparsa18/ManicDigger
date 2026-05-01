using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
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
        base.DisconnectHandler(platformView);
    }

    // -----------------------------------------------------------------------
    // Game bootstrap — runs once the native panel has an HWND
    // -----------------------------------------------------------------------

    private void OnPanelLoaded(object sender, RoutedEventArgs e)
    {
        Grid panel = (Grid)sender;
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

    // -----------------------------------------------------------------------
    // Single-player server thread (unchanged from Program.cs)
    // -----------------------------------------------------------------------

    private void ServerThreadStart()
    {
        Log.Debug("Single-player server thread started");

        Log.Debug("Single-player server thread stopped cleanly");
    }
}