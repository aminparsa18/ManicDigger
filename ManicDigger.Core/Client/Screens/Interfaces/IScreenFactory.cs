public interface IScreenFactory
{
    IMainScreen CreateMainScreen();
    ISingleplayerScreen CreateSingleplayerScreen();
    IScreenMultiplayer CreateMultiplayerScreen();
    ILoginScreen CreateLoginScreen(string serverHash, string ip, int port);
    IConnectionScreen CreateConnectionScreen();
    IScreenGame CreateScreenGame();
}