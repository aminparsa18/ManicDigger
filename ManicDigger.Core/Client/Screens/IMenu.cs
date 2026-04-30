public interface IMenu
{ 
    // Render
    string Translate(string key);
    float AssetsLoadProgress { get; }
    List<Asset> Assets { get; }
    float GetScale();
    void DrawBackground();
    void DrawText(string text, float fontSize, float x, float y, TextAlign align, TextBaseline baseline);
    void DrawButton(string text, float fontSize, float dx, float dy, float dw, float dh, bool pressed);
    void DrawServerButton(string name, string motd, string gamemode, string playercount,
                          float x, float y, float width, float height, string image);
    void Draw2dQuad(int textureid, float dx, float dy, float dw, float dh);
    int GetTexture(string name);
    void RegisterTexture(string name, int textureId);
    byte[] GetFile(string name);
    int GetFileLength(string name);

    //Navigate
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