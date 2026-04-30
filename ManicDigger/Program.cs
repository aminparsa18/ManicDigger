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
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        ServiceProvider = serviceCollection.BuildServiceProvider();

        Start(args);
    }

    private void ConfigureServices(ServiceCollection services)
    {
        // Register your services here
        services.AddTransient<IGameExit, GameExit>();
        services.AddTransient<IGameService, GameService>();
        services.AddTransient<IPreferences, Preferences>();
        services.AddTransient<IOpenGlService, OpenGlService>();
        services.AddTransient<ISinglePlayerService, SinglePlayerService>();
          
                //StartSinglePlayerServer = filename =>
                //{
                //    savefilename = filename;
                //    new Thread(ServerThreadStart) { IsBackground = true }.Start();
                //}

        services.AddSingleton<IDummyNetwork, DummyNetwork>();

    }

    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------

    private string savefilename;
    public IGameExit exit;
    private IGameService gameService;
    private ISinglePlayerService singlePlayerService;

    // -------------------------------------------------------------------------
    // Startup
    // -------------------------------------------------------------------------

    private void Start(string[] args)
    {
        if (!Debugger.IsAttached)
            Environment.CurrentDirectory = Path.GetDirectoryName(Application.ExecutablePath)!;

        Log.Debug("Initialising GamePlatformNative");

        gameService = ServiceProvider.GetRequiredService<IGameService>();

        Log.Debug("Creating GameWindowNative");
        using GameWindowNative window = new();
        gameService.Window = window;

        //temporary until DI is done;
        exit ??= ServiceProvider.GetRequiredService<IGameExit>();
        singlePlayerService = ServiceProvider.GetRequiredService<ISinglePlayerService>();
        var openGlService = ServiceProvider.GetRequiredService<IOpenGlService>();
        var preference = ServiceProvider.GetRequiredService<IPreferences>();
        var network = ServiceProvider.GetRequiredService<IDummyNetwork>();

        MainMenu mainmenu = new(gameService, openGlService, singlePlayerService, preference, exit, network);

        mainmenu.Start();
        ReadArgs(mainmenu, args);

        gameService.Start();
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
}