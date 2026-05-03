public interface IServerConfig
{
    bool AllowGuests { get; set; }
    bool AllowScripting { get; set; }
    bool AllowSpectatorBuild { get; set; }
    bool AllowSpectatorUse { get; set; }
    List<AreaConfig> Areas { get; set; }
    int AutoRestartCycle { get; set; }
    bool BuildLogging { get; set; }
    bool ChatLogging { get; set; }
    int ClientConnectionTimeout { get; set; }
    int ClientPlayingTimeout { get; set; }
    bool EnableHTTPServer { get; set; }
    bool EnablePlayerPushing { get; set; }
    int Format { get; set; }
    bool IsCreative { get; set; }
    string Key { get; set; }
    int MapSizeX { get; set; }
    int MapSizeY { get; set; }
    int MapSizeZ { get; set; }
    int MaxClients { get; set; }
    bool Monsters { get; set; }
    string Motd { get; set; }
    string Name { get; set; }
    string Password { get; set; }
    int PlayerDrawDistance { get; set; }
    int Port { get; set; }
    bool Public { get; set; }
    bool RandomSeed { get; set; }
    int Seed { get; set; }
    bool ServerEventLogging { get; set; }
    string ServerLanguage { get; set; }
    bool ServerMonitor { get; set; }
    string WelcomeMessage { get; set; }

    bool CanUserBuild(ClientOnServer client, int x, int y, int z);
    bool IsPasswordProtected();
    void CopyFrom(ServerConfig source);
    bool ConfigNeedsSaving { get; set; }
}