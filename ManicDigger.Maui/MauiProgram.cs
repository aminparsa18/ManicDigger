using ManicDigger.Maui.Platforms.Windows;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ManicDigger.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Configure Serilog early
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("manicdigger.log")
                .CreateLogger();

            // Global crash handler
            CrashReporter.DefaultFileName = "ManicDiggerClientCrash.txt";
            CrashReporter.EnableGlobalExceptionHandling(isConsole: false);

            MauiAppBuilder builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                }).ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
                    // Register our custom handler for the game surface view
                    handlers.AddHandler<GameSurfaceView, GameSurfaceViewHandler>();
#endif
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
