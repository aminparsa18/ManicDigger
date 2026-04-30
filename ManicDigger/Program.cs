using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Diagnostics;

public class Program
{
    public static IServiceProvider ServiceProvider { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        CrashReporter.DefaultFileName = "ManicDiggerClientCrash.txt";
        CrashReporter.EnableGlobalExceptionHandling(isConsole: false);
        _ = new Program(args);
    }

    public Program(string[] args)
    {
        dummyNetwork = new DummyNetwork();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        ServiceProvider = serviceCollection.BuildServiceProvider();

        Start(args);
    }

    private void ConfigureServices(ServiceCollection services)
    {
        // Register your services here
        services.AddTransient<IMenu, MainMenu>();

        services.AddTransient<IGameExit, GameExit>();
        services.AddTransient<IGameService, GameService>();
        services.AddTransient<IPreferences, Preferences>();
        services.AddTransient<IOpenGlService, OpenGlService>();
        services.AddTransient<ISinglePlayerService, SinglePlayerService>(factory =>
        {
            return new SinglePlayerService()
            {
                SinglePlayerServerNetwork = dummyNetwork,
                StartSinglePlayerServer = filename =>
                {
                    savefilename = filename;
                    new Thread(ServerThreadStart) { IsBackground = true }.Start();
                }
            };
        });

    }

    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------

    private readonly DummyNetwork dummyNetwork;
    private string savefilename;
    public IGameExit exit;
    private ISinglePlayerService singlePlayerService;

    // -------------------------------------------------------------------------
    // Startup
    // -------------------------------------------------------------------------

    private static void Start(string[] args)
    {
        if (!Debugger.IsAttached)
            Environment.CurrentDirectory = Path.GetDirectoryName(Application.ExecutablePath)!;

        Log.Debug("Initialising GamePlatformNative");

        var mainmenu = ServiceProvider.GetRequiredService<IMenu>();

        Log.Debug("Creating GameWindowNative");

        using GameWindowNative window = new();
        mainmenu.GameService.Window = window;

        mainmenu.Start();

        ReadArgs(mainmenu, args);

        mainmenu.GameService.Start();
        window.Run();
    }

    private static void ReadArgs(IMenu mainmenu, string[] args)
    {
        if (args.Length > 0)
            mainmenu.StartGame(false, null, ConnectionData.FromUri(new Uri(args[0])));
    }

    // -------------------------------------------------------------------------
    // Single-player server thread
    // -------------------------------------------------------------------------

    public void ServerThreadStart()
    {
        Log.Debug("Single-player server thread started");
        DummyNetServer netServer = new(dummyNetwork);

        singlePlayerService = ServiceProvider.GetRequiredService<ISinglePlayerService>();
        var gameService = ServiceProvider.GetRequiredService<IGameService>();

        exit ??= ServiceProvider.GetRequiredService<IGameExit>();
        Server server = new(exit, gameService)
        {
            SaveFilenameOverride = savefilename,
            MainSockets = new NetServer[3]
        };
        server.MainSockets[0] = netServer;

        while (true)
        {
            server.Process();
            Thread.Sleep(1);
            singlePlayerService.SinglePlayerServerLoaded = true;

            if (exit?.Exit == true)
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

        exit.Exit = false;
        Log.Debug("Single-player server thread stopped cleanly");
    }
}
