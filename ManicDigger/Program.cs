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

    private static void ConfigureServices(ServiceCollection services)
    {
        // Register your services here
        services.AddTransient<IPreferences, Preferences>();
    }

    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------

    private readonly DummyNetwork dummyNetwork;
    private string savefilename;
    public GameExit exit = new();
    private GameService platform;
    private ISinglePlayerService singlePlayerService;

    // -------------------------------------------------------------------------
    // Startup
    // -------------------------------------------------------------------------

    private void Start(string[] args)
    {
        if (!Debugger.IsAttached)
            Environment.CurrentDirectory = Path.GetDirectoryName(Application.ExecutablePath)!;

        Log.Debug("Initialising GamePlatformNative");

        platform = new GameService
        {
            crashreporter = new CrashReporter(),
            GameExit = exit,
        };

        Log.Debug("Creating GameWindowNative");
        using GameWindowNative window = new();
        platform.Window = window;

        singlePlayerService = new SinglePlayerService()
        {
            singlePlayerServerDummyNetwork = dummyNetwork,
            StartSinglePlayerServer = filename =>
            {
                savefilename = filename;
                new Thread(ServerThreadStart) { IsBackground = true }.Start();
            }
        };
        //temporary until DI is done;
        var preference = ServiceProvider.GetRequiredService<IPreferences>();
        MainMenu mainmenu = new(platform, new OpenGlService(), singlePlayerService, preference);

        mainmenu.Start();
        ReadArgs(mainmenu, args);

        platform.Start();
        window.Run();
    }

    private static void ReadArgs(MainMenu mainmenu, string[] args)
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

        Server server = new()
        {
            SaveFilenameOverride = savefilename,
            GameExit = exit,
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
