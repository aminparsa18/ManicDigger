public interface INavigator
{
    void StartMainMenu();
    void StartSingleplayer();
    void StartMultiplayer();
    void StartLogin(string serverHash, string ip, int port);
    void StartConnectToIp();
    void Login(string user, string password, string serverHash, string token,
        LoginResult loginResult, LoginData loginResultData);
    void StartGame(bool singleplayer, string savePath, ConnectionData connectData);
    void ConnectToSingleplayer(string filename);
    void ConnectToGame(LoginData loginData, string username);
}

public interface IScreenManager : INavigator
{
    void Start(string[] args);
}