public interface IScreenManager
{
    void StartMainMenu();
    void StartSingleplayer();
    void StartMultiplayer();
    void Login(string user, string password, string serverHash, string token,
        LoginResult loginResult, LoginData loginResultData);
    void StartLogin(string serverHash, string ip, int port);
    void StartConnectToIp();
    void StartGame(bool singleplayer, string savePath, ConnectionData connectData);
    void ConnectToSingleplayer(string filename);
    void ConnectToGame(LoginData loginData, string username);

    void Start(string[] args);
}