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

    private static void ConfigureServices(ServiceCollection services)
    {
        // Register your services here
        services.AddTransient<IMenu, MainMenu>();

        services.AddTransient<IGameExit, GameExit>();
        services.AddTransient<IGameService, GameService>();
        services.AddTransient<IPreferences, Preferences>();
        services.AddTransient<IOpenGlService, OpenGlService>();
        services.AddTransient<ISinglePlayerService, SinglePlayerService>();
          
        services.AddSingleton<IDummyNetwork, DummyNetwork>();
    }

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
}