using ManicDigger;
using ManicDigger.Mods;
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
        services.AddSingleton<GameWindowNative>();
        services.AddSingleton<IVoxelMap, VoxelMap>();

        // ── Player logic ──────────────────────────────────────────────────────
        services.AddScoped<IModBase, ModDrawMain>();
        services.AddScoped<IModBase, ModUpdateMain>();
        services.AddScoped<IModBase, ModNetworkProcess>();
        services.AddScoped<IModBase, ModNetworkEntity>();
        services.AddScoped<IModBase, ModAutoCamera>();
        services.AddScoped<IModBase, ModFallDamageToPlayer>();
        services.AddScoped<IModBase, ModBlockDamageToPlayer>();
        services.AddScoped<IModBase, ModLoadPlayerTextures>();
        services.AddScoped<IModBase, ModSendPosition>();
        services.AddScoped<IModBase, ModInterpolatePositions>();
        services.AddScoped<IModBase, ModPush>();
        services.AddScoped<IModBase, ModFly>();

        // ── Camera ────────────────────────────────────────────────────────────
        services.AddScoped<IModBase, ModAutoCamera>();
        services.AddScoped<IModBase, ModCameraKeys>();
        services.AddScoped<IModBase, ModCamera>();

        // ── Gameplay mechanics ────────────────────────────────────────────────
        services.AddScoped<IModBase, ModRail>();
        services.AddScoped<IModBase, ModCompass>();
        services.AddScoped<IModBase, ModGrenade>();
        services.AddScoped<IModBase, ModBullet>();
        services.AddScoped<IModBase, ModExpire>();
        services.AddScoped<IModBase, ModPicking>();

        // ── Inventory / ammo ──────────────────────────────────────────────────
        services.AddScoped<IModBase, ModReloadAmmo>();
        services.AddScoped<IModBase, ModSendActiveMaterial>();
        services.AddScoped<IModBase, ModGuiCrafting>();
        services.AddScoped<IModBase, ModGuiInventory>();

        // ── Audio ─────────────────────────────────────────────────────────────
        services.AddScoped<IModBase, ModWalkSound>();
        services.AddScoped<IModBase, ModAudio>();

        // ── World rendering ───────────────────────────────────────────────────
        services.AddScoped<IModBase, ModSkySphereAnimated>();
        services.AddScoped<IModBase, SunMoonRenderer>();
        services.AddScoped<IModBase, ModDrawTerrain>();
        services.AddScoped<IModBase, ModDrawArea>();
        services.AddScoped<IModBase, ModDrawSprites>();
        services.AddScoped<IModBase, ModDrawMinecarts>();
        services.AddScoped<IModBase, ModDrawLinesAroundSelectedBlock>();
        services.AddScoped<IModBase, ModDebugChunk>();
        services.AddScoped<IModBase, ModDrawParticleEffectBlockBreak>();

        // ── Entity / player rendering ─────────────────────────────────────────
        services.AddScoped<IModBase, ModDrawPlayers>();
        services.AddScoped<IModBase, ModDrawPlayerNames>();
        services.AddScoped<IModBase, ModDrawTestModel>();
        services.AddScoped<IModBase, ModClearInactivePlayersDrawInfo>();

        // ── HUD / 2D overlay ──────────────────────────────────────────────────
        services.AddScoped<IModBase, ModDrawHand2d>();
        services.AddScoped<IModBase, ModDrawHand3d>();
        services.AddScoped<IModBase, ModDrawText>();
        services.AddScoped<IModBase, ModDraw2dMisc>();
        services.AddScoped<IModBase, ModFpsHistoryGraph>();

        // ── GUI (topmost — rendered last) ─────────────────────────────────────
        services.AddScoped<IModBase, ModDialog>();
        services.AddScoped<IModBase, ModGuiTouchButtons>();
        services.AddScoped<IModBase, ModGuiEscapeMenu>();
        services.AddScoped<IModBase, ModGuiMapLoading>();
        services.AddScoped<IModBase, ModGuiPlayerStats>();
        services.AddScoped<IModBase, ModGuiChat>();
        services.AddScoped<IModBase, ModScreenshot>();

        services.AddTransient<IMenu, MainMenu>();

        services.AddTransient<IGameExit, GameExit>();
        services.AddTransient<IGameService, GameService>();
        services.AddSingleton<IAudioService, AudioService>();
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

        mainmenu.Start(args);
    }
}