// The only class that knows about IServiceProvider — kept in your composition root.
using Microsoft.Extensions.DependencyInjection;

public sealed class ScreenFactory : IScreenFactory
{
    private readonly IServiceProvider _sp;

    public ScreenFactory(IServiceProvider sp) => _sp = sp;

    public IMainScreen CreateMainScreen() => _sp.GetRequiredService<IMainScreen>();
    public ISingleplayerScreen CreateSingleplayerScreen() => _sp.GetRequiredService<ISingleplayerScreen>();
    public IScreenMultiplayer CreateMultiplayerScreen() => _sp.GetRequiredService<IScreenMultiplayer>();
    public IConnectionScreen CreateConnectionScreen() => _sp.GetRequiredService<IConnectionScreen>();

    public ILoginScreen CreateLoginScreen(string serverHash, string ip, int port)
    {
        var screen = _sp.GetRequiredService<ILoginScreen>();
        screen.Configure(serverHash, ip, port);   // runtime params separated from ctor
        return screen;
    }

    public IScreenGame CreateScreenGame() => _sp.GetRequiredService<IScreenGame>();
}