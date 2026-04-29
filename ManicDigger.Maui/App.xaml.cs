using Serilog;

namespace ManicDigger.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new ContentPage(); // empty, never seen
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);

        // Hide the MAUI window — OpenTK will create its own
        window.Width = 0;
        window.Height = 0;
        window.X = -9999;
        window.Y = -9999;

        // Start the game once the window is created
        window.Created += OnWindowCreated;

        return window;
    }

    private void OnWindowCreated(object? sender, EventArgs e)
    {
        // Run on a background thread so we don't block the MAUI UI thread
        // OpenTK's Run() is blocking so it must not run on the main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var gameRunner = new GameRunner();
                gameRunner.Start([.. Environment.GetCommandLineArgs().Skip(1)]);
            }
            catch (Exception ex)
            {
                File.WriteAllText(
               Path.Combine(AppContext.BaseDirectory, "crash.txt"),
               FlattenException(ex));
            }
        });
    }

    private static string FlattenException(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        while (ex != null)
        {
            sb.AppendLine(ex.GetType().FullName);
            sb.AppendLine(ex.Message);
            sb.AppendLine(ex.StackTrace);
            sb.AppendLine("--- Inner Exception ---");
            ex = ex.InnerException;
        }
        return sb.ToString();
    }
}

public class GameRunner
{
    private readonly DummyNetwork _dummyNetwork = new();
    private GamePlatformNative _platform;
    private GameExit _exit = new();
    private string? _savefilename;

    public void Start(string[] args)
    {
        Environment.CurrentDirectory = AppContext.BaseDirectory;
        // Temporary: log what the loader will actually scan
        Log.Information("Working dir: {Dir}", Environment.CurrentDirectory);
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

        using GameWindowNative window = new();
        _platform.window = window;

        MainMenu mainmenu = new(_platform);
        mainmenu.Start();

        if (args.Length > 0)
            mainmenu.StartGame(false, null, ConnectionData.FromUri(new Uri(args[0])));

        _platform.Start();
        window.Run(); // blocks here until game exits
    }

    private void ServerThreadStart()
    {
        var netServer = new DummyNetServer(_dummyNetwork);
        var server = new Server
        {
            SaveFilenameOverride = _savefilename,
            GameExit = _exit,
            MainSockets = new NetServer[3]
        };
        server.MainSockets[0] = netServer;

        while (true)
        {
            server.Process();
            Thread.Sleep(1);
            _platform.singlePlayerServerLoaded = true;

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
    }
}