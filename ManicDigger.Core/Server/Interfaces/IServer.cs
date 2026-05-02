using ManicDigger;
using OpenTK.Mathematics;

public interface IServer
{
    static abstract int ChunkSize { get; set; }
    static abstract double InvertedChunkSize { get; set; }
    List<string> AllPrivileges { get; set; }
    ServerBanlist BanList { get; set; }
    BlockRegistry BlockTypeRegistry { get; set; }
    Dictionary<int, BlockType> BlockTypes { get; set; }
    IChunkDb ChunkDb { get; set; }
    int ChunkDrawDistance { get; }
    Dictionary<int, ClientOnServer> Clients { get; set; }
    ServerConfig Config { get; set; }
    bool ConfigNeedsSaving { get; set; }
    List<CraftingRecipe> CraftingRecipes { get; set; }
    CraftingTableTool CraftingTableTool { get; set; }
    Group? DefaultGroupGuest { get; set; }
    Group DefaultGroupRegistered { get; set; }
    Vector3i DefaultPlayerSpawn { get; set; }
    Dictionary<string, bool> Disabledprivileges { get; set; }
    int DrawDistance { get; set; }
    bool EnableShadows { get; set; }
    Dictionary<string, bool> ExtraPrivileges { get; set; }
    string GameMode { get; set; }
    List<ActiveHttpModule> HttpModules { get; set; }
    Dictionary<string, Inventory> Inventory { get; set; }
    bool IsSinglePlayer { get; }
    LanguageService Language { get; set; }
    int LastMonsterId { get; set; }
    NetServer[] MainSockets { get; set; }
    ServerMapStorage Map { get; set; }
    Dictionary<string, byte[]> ModData { get; set; }
    ModManager ModManager { get; set; }
    List<string> ModPaths { get; set; }
    ICompression NetworkCompression { get; set; }
    List<Action> OnLoad { get; set; }
    List<Action> OnSave { get; set; }
    int Port { get; set; }
    string ReceivedKey { get; set; }
    RenderHint RenderHint { get; set; }
    string SaveFilenameOverride { get; set; }
    int Seed { get; set; }
    ServerClient ServerClient { get; set; }
    bool ServerClientNeedsSaving { get; set; }
    ClientOnServer ServerConsoleClient { get; set; }
    int ServerConsoleId { get; }
    float SIMULATION_STEP_LENGTH { get; set; }
    int SimulationCurrentFrame { get; set; }
    List<ServerSystem> Systems { get; set; }
    Dictionary<Timer, Timer.Tick> Timers { get; set; }
    long TotalReceivedBytes { get; set; }
    long TotalSentBytes { get; set; }
    TimeSpan Uptime { get; }

    static abstract int InvertChunk(int num);
    static abstract IEnumerable<byte[]> Parts(byte[] blob, int partsize);
    static abstract Vector3i PlayerBlockPosition(ClientOnServer c);
    static abstract byte[] Serialize(Packet_Server p);
    static abstract int SerializeFloat(float p);
    void AddEntity(int x, int y, int z, ServerEntity e);
    bool Announcement(int sourceClientId, string message);
    bool AnswerMessage(int sourceClientId, string message);
    bool AreaAdd(int sourceClientId, int id, string coords, string[] permittedGroups, string[] permittedUsers, int? level);
    bool AreaDelete(int sourceClientId, int id);
    bool BackupDatabase(string backupFilename);
    bool ChangeGroup(int sourceClientId, string target, string newGroupName);
    bool ChangeGroupOffline(int sourceClientId, string target, string newGroupName);
    void ChatLog(string p);
    bool CheckBuildPrivileges(int player, int x, int y, int z, PacketBlockSetMode mode);
    bool ClearInterpreter(int sourceClientId);
    bool ClientSeenChunk(int clientid, int vx, int vy, int vz);
    void ClientSeenChunkRemove(int clientid, int vx, int vy, int vz);
    void ClientSeenChunkSet(int clientid, int vx, int vy, int vz, int time);
    void CommandInterpreter(int sourceClientId, string command, string argument);
    byte[] CompressChunkNetwork(ushort[] chunk);
    void DeleteChunk(int x, int y, int z);
    void DeleteChunks(List<Vector3i> chunkPositions);
    void DespawnEntity(ServerEntityId id);
    void Dispose();
    Vector3i DontSpawnPlayerInWater(Vector3i initialSpawn);
    void DropItem(ref InventoryItem item, Vector3i pos);
    void Exit();
    int GenerateClientId();
    int GetBlock(int x, int y, int z);
    ushort[] GetChunk(int x, int y, int z);
    ushort[] GetChunkFromDatabase(int x, int y, int z, string filename);
    Dictionary<Vector3i, ushort[]> GetChunksFromDatabase(List<Vector3i> chunks, string filename);
    ClientOnServer GetClient(int id);
    ClientOnServer GetClient(string name);
    ServerEntity GetEntity(int chunkx, int chunky, int chunkz, int id);
    string GetGroupColor(int playerid);
    string GetGroupName(int playerid);
    int GetHeight(int x, int y);
    InventoryUtil GetInventoryUtil(Inventory inventory);
    int[] GetMapSize();
    Inventory GetPlayerInventory(string playername);
    Vector3i GetPlayerSpawnPositionMul32(int clientid);
    PacketServerPlayerStats GetPlayerStats(string playername);
    string GetSaveFilename();
    int GetSimulationCurrentFrame();
    GameTimer GetTimer();
    bool Give(int sourceClientId, string target, string blockname, int amount);
    bool GiveAll(int sourceClientId, string target);
    void Help(int sourceClientId);
    void InstallHttpModule(string name, Func<string> description, IHttpModule module);
    bool Kick(int sourceClientId, int targetClientId);
    bool Kick(int sourceClientId, int targetClientId, string reason);
    bool Kick(int sourceClientId, string target);
    bool Kick(int sourceClientId, string target, string reason);
    void KillPlayer(int clientid);
    bool List(int sourceClientId, string type);
    void LoadChunk(int cx, int cy, int cz);
    bool LoadDatabase(string filename);
    bool Login(int sourceClientId, string targetGroupString, string password);
    bool Monsters(int sourceClientId, string option);
    void NotifyInventory(int clientid);
    void NotifyPlayerStats(int clientid);
    void OnConfigLoaded();
    void PlayerEntitySetDirty(int player);
    bool PlayerHasPrivilege(int player, string privilege);
    void PlaySoundAt(int posx, int posy, int posz, string sound);
    void PlaySoundAt(int posx, int posy, int posz, string sound, int range);
    bool PrivateMessage(int sourceClientId, string recipient, string message);
    bool PrivilegeAdd(int sourceClientId, string target, string privilege);
    bool PrivilegeRemove(int sourceClientId, string target, string privilege);
    void Process();
    void ProcessMain();
    void ReceiveServerConsole(string message);
    bool RemoveClientFromConfig(int sourceClientId, string target);
    bool ResetInventory(int sourceClientId, string target);
    void ResetPlayerInventory(ClientOnServer client);
    void Restart();
    bool RestartServer(int sourceClientId);
    void SaveChunksToDatabase(List<Vector3i> chunkPositions, string filename);
    void SendAmmo(int playerid, Dictionary<int, int> totalAmmo);
    void SendBlockTypes(int clientid);
    void SendDialog(int player, string id, Dialog dialog);
    void SendExplosion(int player, float x, float y, float z, bool relativeposition, float range, float time);
    void SendFreemoveState(int clientid, bool isEnabled);
    void SendMessage(int clientid, string message);
    void SendMessage(int clientid, string message, Server.MessageType color);
    void SendMessageToAll(string message);
    void SendPacket(int clientid, byte[] packet);
    void SendPacket(int clientid, Packet_Server packet);
    void SendPacketFollow(int player, int target, bool tpp);
    void SendServerRedirect(int clientid, string ip_, int port_);
    void SendSound(int clientid, string name, int x, int y, int z);
    void ServerEventLog(string p);
    void ServerMessageToAll(string message, Server.MessageType color);
    void SetBlock(int x, int y, int z, int blocktype);
    void SetBlockAndNotify(int x, int y, int z, int blocktype);
    void SetBlockType(int id, string name, BlockType block);
    void SetBlockType(string name, BlockType block);
    void SetChunk(int x, int y, int z, ushort[] data);
    void SetChunks(Dictionary<Vector3i, ushort[]> chunks);
    void SetChunks(int offsetX, int offsetY, int offsetZ, Dictionary<Vector3i, ushort[]> chunks);
    void SetEntityDirty(ServerEntityId id);
    bool SetFillAreaLimit(int sourceClientId, string targetType, string target, int maxFill);
    void SetLightLevels(float[] lightLevels);
    bool SetLogging(int sourceClientId, string type, string option);
    bool SetSpawnPosition(int sourceClientId, int x, int y, int? z);
    bool SetSpawnPosition(int sourceClientId, string targetType, string target, int x, int y, int? z);
    void SetSunLevels(int[] sunLevels);
    bool ShutdownServer(int sourceClientId);
    void Stop();
    bool TeleportPlayer(int sourceClientId, string target, int x, int y, int? z);
    bool TeleportToPlayer(int sourceClientId, int clientTo);
    bool TeleportToPosition(int sourceClientId, int x, int y, int? z);
    bool TimeCommand(int sourceClientId, string argument);
    bool WelcomeMessage(int sourceClientId, string welcomeMessage);
}