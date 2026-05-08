using System.Diagnostics;

namespace ManicDigger.Maui;

public partial class App : Application
{
    private readonly IModRegistry _modRegistry;
    private readonly IEnumerable<IModBase> _mods;
    private readonly IScreenManager _screenManager;

    public App(IModRegistry modRegistry, IEnumerable<IModBase> mods, IScreenManager screenManager)
    {
        InitializeComponent();
        _mods = mods;
        _modRegistry = modRegistry;
        _screenManager = screenManager;
        MainPage = new ContentPage(); // empty, never seen
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = base.CreateWindow(activationState);

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
            if (!Debugger.IsAttached)
            {
                Environment.CurrentDirectory = Path.GetDirectoryName(AppContext.BaseDirectory)!;
            }

            _modRegistry.Initialise(_mods);

            // 3. Loop starts — ClientMods is fully populated
            _screenManager.Start([.. Environment.GetCommandLineArgs().Skip(1)]);
        });
    }
}