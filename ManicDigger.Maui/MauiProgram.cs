using ManicDigger.Extensions;

namespace ManicDigger.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        builder.Services.AddSharedServices();
        builder.Services.AddClientServices();
        builder.Services.AddServerServices();

        builder.Services.AddClientMods();
        builder.Services.AddServerMods();

        builder.Services.AddScreens();

        builder.Services.AddWorkerInfrastructure();

        return builder.Build();
    }
}
