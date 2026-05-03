using OpenTK.Mathematics;
using System.Runtime.InteropServices;

namespace ManicDigger;

public class ServerModManager(IGameExit gameExit, IBlockRegistry blockRegistry, IChunkDbCompressed chunkDb,
    IServerMapStorage serverMapStorage, ILanguageService languageService, IServerConfig config,
    Server server) : IServerModManager
{
    private readonly IGameExit gameExit = gameExit;
    private readonly IBlockRegistry _blockRegistry = blockRegistry;
    private readonly IChunkDbCompressed _chunkDb = chunkDb;
    private readonly IServerMapStorage _serverMapStorage = serverMapStorage;
    private readonly ILanguageService _languageService = languageService;
    private readonly IServerConfig _config = config;

    public int GetMaxBlockTypes() => GameConstants.MAX_BLOCKTYPES;

    public void SetBlockType(int id, string name, BlockType block)
    {
        block.Sounds ??= defaultSounds;
        _server.SetBlockType(id, name, block);
    }

    public void SetBlockType(string name, BlockType block)
    {
        block.Sounds ??= defaultSounds;
        _server.SetBlockType(name, block);
    }

    public int GetBlockId(string name)
    {
        foreach ((int id, BlockType? blockType) in _blockRegistry.BlockTypes)
        {
            if (blockType.Name == name)
            {
                return id;
            }
        }

        throw new Exception(name);
    }

    public void AddToCreativeInventory(string blockType)
    {
        int id = GetBlockId(blockType);
        if (id == -1)
        {
            throw new Exception(blockType);
        }

        _blockRegistry.BlockTypes[id].IsBuildable = true;
        _blockRegistry.RegisterBlockType(id, _blockRegistry.BlockTypes[id]);
    }

    public int GetMapSizeX() => _serverMapStorage.MapSizeX;
    public int GetMapSizeY() => _serverMapStorage.MapSizeY;
    public int GetMapSizeZ() => _serverMapStorage.MapSizeZ;

    public int GetBlock(int x, int y, int z) => _serverMapStorage.GetBlock(x, y, z);

    public string GetBlockName(int blockType) => _blockRegistry.BlockTypes[blockType].Name;

    public string GetBlockNameAt(int x, int y, int z) => GetBlockName(GetBlock(x, y, z));

    public void SetBlock(int x, int y, int z, int tileType) => _server.SetBlockAndNotify(x, y, z, tileType);

    private Server _server => server;

    public void SetSunLevels(int[] sunLevels) => _server.SetSunLevels(sunLevels);

    public void SetLightLevels(float[] lightLevels) => _server.SetLightLevels(lightLevels);

    private const string recipeError = "Recipe error:";

    public void AddCraftingRecipe(string output, int outputAmount, string Input0, int Input0Amount)
    {
        if (GetBlockId(output) == -1)
        {
            Console.WriteLine(recipeError + output);
            return;
        }

        if (GetBlockId(Input0) == -1)
        {
            Console.WriteLine(recipeError + Input0);
            return;
        }

        CraftingRecipe r = new()
        {
            Ingredients =
            [
                    new Ingredient(){Type=GetBlockId(Input0), Amount=Input0Amount},
            ],
            Output = new Ingredient() { Type = GetBlockId(output), Amount = outputAmount }
        };
        _server.CraftingRecipes.Add(r);
    }

    public void AddCraftingRecipe2(string output, int outputAmount, string Input0, int Input0Amount, string Input1, int Input1Amount)
    {
        if (GetBlockId(output) == -1)
        {
            Console.WriteLine(recipeError + output);
            return;
        }

        if (GetBlockId(Input0) == -1)
        {
            Console.WriteLine(recipeError + Input0);
            return;
        }

        if (GetBlockId(Input1) == -1)
        {
            Console.WriteLine(recipeError + Input1);
            return;
        }

        CraftingRecipe r = new()
        {
            Ingredients =
            [
                    new Ingredient(){Type=GetBlockId(Input0), Amount=Input0Amount},
                    new Ingredient(){Type=GetBlockId(Input1), Amount=Input1Amount},
            ],
            Output = new Ingredient() { Type = GetBlockId(output), Amount = outputAmount }
        };
        _server.CraftingRecipes.Add(r);
    }

    public void AddCraftingRecipe3(string output, int outputAmount, string Input0, int Input0Amount, string Input1, int Input1Amount, string Input2, int Input2Amount)
    {
        if (GetBlockId(output) == -1)
        {
            Console.WriteLine(recipeError + output);
            return;
        }

        if (GetBlockId(Input0) == -1)
        {
            Console.WriteLine(recipeError + Input0);
            return;
        }

        if (GetBlockId(Input1) == -1)
        {
            Console.WriteLine(recipeError + Input1);
            return;
        }

        if (GetBlockId(Input2) == -1)
        {
            Console.WriteLine(recipeError + Input2);
            return;
        }

        CraftingRecipe r = new()
        {
            Ingredients =
            [
                    new Ingredient(){Type=GetBlockId(Input0), Amount=Input0Amount},
                    new Ingredient(){Type=GetBlockId(Input1), Amount=Input1Amount},
                    new Ingredient(){Type=GetBlockId(Input2), Amount=Input2Amount},
            ],
            Output = new Ingredient() { Type = GetBlockId(output), Amount = outputAmount }
        };
        _server.CraftingRecipes.Add(r);
    }

    public void SetString(string language, string id, string translation) => _languageService.Override(language, id, translation);

    public string GetString(string id)
        //Returns string depending on server language
        => _languageService.Get(id);

    public bool IsValidPos(int x, int y, int z) => VectorUtils.IsValidPos(_serverMapStorage, x, y, z);

    public void RegisterTimer(Action a, double interval) => _server.Timers[new Timer() { INTERVAL = interval }] = delegate { a(); };

    public void PlaySoundAt(int posx, int posy, int posz, string sound) => _server.PlaySoundAt(posx, posy, posz, sound);

    public void PlaySoundAt(int x, int y, int z, string sound, int range) => _server.PlaySoundAt(x, y, z, sound, range);

    public int NearestPlayer(int x, int y, int z)
    {
        int closeplayer = -1;
        int closedistance = -1;
        foreach (KeyValuePair<int, ClientOnServer> k in _server.Clients)
        {
            int distance = VectorUtils.DistanceSquared(new Vector3i(k.Value.PositionMul32GlX / 32, k.Value.PositionMul32GlZ / 32, k.Value.PositionMul32GlY / 32), new Vector3i(x, y, z));
            if (closedistance == -1 || distance < closedistance)
            {
                closedistance = distance;
                closeplayer = k.Key;
            }
        }

        return closeplayer;
    }

    public void GrabBlock(int player, int block) => GrabBlocks(player, block, 1);

    public void GrabBlocks(int player, int block, int amount)
    {
        Inventory inventory = _server.GetPlayerInventory(_server.GetClient(player).PlayerName);

        InventoryItem item = new()
        {
            InventoryItemType = InventoryItemType.Block,
            BlockCount = amount,
            BlockId = _blockRegistry.WhenPlayerPlacesGetsConvertedTo[block]
        };
        _server.GetInventoryUtil(inventory).GrabItem(item, 0);
    }

    public bool PlayerHasPrivilege(int player, string privilege) => _server.PlayerHasPrivilege(player, privilege);

    public bool IsCreative => _config.IsCreative;

    public bool IsBlockFluid(int block) => _blockRegistry.BlockTypes[block].IsFluid();

    public void NotifyInventory(int player)
    {
        _server.GetClient(player).IsInventoryDirty = true;
        _server.NotifyInventory(player);
    }

    public string ColorError => _server.colorError;

    public void SendMessage(int player, string p) => _server.SendMessage(player, p);

    public void RegisterPrivilege(string p)
    {
        // Add to list of all available privileges on server
        if (!_server.AllPrivileges.Contains(p))
        {
            _server.AllPrivileges.Add(p);
        }
        // Direct modification of console client as mods are loaded after privileges are assigned
        if (!_server.ServerConsoleClient.Privileges.Contains(p))
        {
            _server.ServerConsoleClient.Privileges.Add(p);
        }
    }

    public bool IsTransparentForLight(int p) => Game.IsTransparentForLight(_blockRegistry.BlockTypes[p]);

    public void RegisterOptionBool(string optionname, bool default_) => modoptions[optionname] = default_;

    private readonly Dictionary<string, object> modoptions = [];

    public int GetChunkSize() => GameConstants.ServerChunkSize;

    public object GetOption(string optionname) => modoptions[optionname];

    public int Seed => _server.Seed;

    public int Index3d(int x, int y, int h, int sizex, int sizey) => (((h * sizey) + y) * sizex) + x;

    public void SetDefaultSounds(SoundSet defaultSounds) => this.defaultSounds = defaultSounds;
    private SoundSet defaultSounds;

    public byte[] GetGlobalData(string name)
    {
        if (_server.ModData.TryGetValue(name, out byte[]? value))
        {
            return value;
        }

        return null;
    }

    public void SetGlobalData(string name, byte[] value) => _server.ModData[name] = value;

    public void RegisterOnLoad(Action f) => _server.OnLoad.Add(f);

    public void RegisterOnSave(Action f) => _server.OnSave.Add(f);

    public string GetPlayerIp(int player) => _server.GetClient(player).Socket.RemoteEndPoint().AddressToString();

    public string GetPlayerName(int player) => _server.GetClient(player).PlayerName;

    public List<string> required { get; set; } = [];

    public void RequireMod(string modname) => required.Add(modname);

    public void SetGlobalDataNotSaved(string name, object value) => notsaved[name] = value;

    public object GetGlobalDataNotSaved(string name)
    {
        if (!notsaved.TryGetValue(name, out object? value))
        {
            return null;
        }

        return value;
    }
    private readonly Dictionary<string, object> notsaved = [];

    public void SendMessageToAll(string message) => _server.SendMessageToAll(message);

    public void RegisterCommandHelp(string command, string help) => _server.commandhelps[command] = help;

    public void AddToStartInventory(string blocktype, int amount) => _blockRegistry.StartInventoryAmount[GetBlockId(blocktype)] = amount;

    public long GetCurrentTick() => _server.GetSimulationCurrentFrame();

    public void SetDaysPerYear(int days)
    {
        if (days > 0)
        {
            _server.GetTimer().DaysPerYear = days;
        }
        else
        {
            throw new ArgumentOutOfRangeException("The number of days per year must be greater than 0!");
        }
    }

    public int GetDaysPerYear() => _server.GetTimer().DaysPerYear;

    public int GetHour() => _server.GetTimer().Hour;

    public double GetTotalHours() => _server.GetTimer().HourTotal;

    public int GetDay() => _server.GetTimer().Day;

    public double GetTotalDays() => _server.GetTimer().DaysTotal;

    public int GetYear() => _server.GetTimer().Year;

    public int GetSeason() => _server.GetTimer().Season;

    public void UpdateBlockTypes()
    {
        foreach (KeyValuePair<int, ClientOnServer> k in _server.Clients)
        {
            _server.SendBlockTypes(k.Key);
        }
    }

    public double GetGameDayRealHours()
    {
        double nSecondsPerDay = TimeSpan.FromDays(1).TotalSeconds;

        int nSpeed = _server.GetTimer().SpeedOfTime;
        double nSeconds = nSecondsPerDay / nSpeed;

        double nHours = TimeSpan.FromSeconds(nSeconds).TotalHours;

        return nHours;
    }

    public void SetGameDayRealHours(double hours)
    {
        double nSecondsPerDay = TimeSpan.FromDays(1).TotalSeconds;

        double nSecondsGiven = TimeSpan.FromHours(hours).TotalSeconds;

        _server.GetTimer().SpeedOfTime = (int)(nSecondsPerDay / nSecondsGiven);
    }

    public void EnableShadows(bool value) => _server.EnableShadows = value;

    public float GetPlayerPositionX(int player) => (float)_server.GetClient(player).PositionMul32GlX / 32;

    public float GetPlayerPositionY(int player) => (float)_server.GetClient(player).PositionMul32GlZ / 32;

    public float GetPlayerPositionZ(int player) => (float)_server.GetClient(player).PositionMul32GlY / 32;

    public void SetPlayerPosition(int player, float x, float y, float z)
    {
        ServerEntityPositionAndOrientation pos;
        if (_server.Clients[player].PositionOverride == null)
        {
            //No position override so far. Clone from player position
            pos = _server.Clients[player].Entity.Position.Clone();
        }
        else
        {
            //Position has already been modified. Clone from override to prevent data loss
            pos = _server.Clients[player].PositionOverride.Clone();
        }

        pos.X = x;
        pos.Y = z;
        pos.Z = y;
        _server.Clients[player].PositionOverride = pos;
    }

    public int GetPlayerHeading(int player) => _server.GetClient(player).PositionHeading;

    public int GetPlayerPitch(int player) => _server.GetClient(player).PositionPitch;

    public int GetPlayerStance(int player) => _server.GetClient(player).Stance;

    public void SetPlayerOrientation(int player, int heading, int pitch, int stance)
    {
        ServerEntityPositionAndOrientation pos;
        if (_server.Clients[player].PositionOverride == null)
        {
            //No position override so far. Clone from player position
            pos = _server.Clients[player].Entity.Position.Clone();
        }
        else
        {
            //Position has already been modified. Clone from override to prevent data loss
            pos = _server.Clients[player].PositionOverride.Clone();
        }

        pos.Heading = (byte)heading;
        pos.Pitch = (byte)pitch;
        pos.Stance = (byte)stance;
        _server.Clients[player].PositionOverride = pos;
    }

    public int[] AllPlayers()
    {
        List<int> players = [];
        foreach (KeyValuePair<int, ClientOnServer> k in _server.Clients)
        {
            players.Add(k.Key);
        }

        return [.. players];
    }

    public void SetPlayerAreaSize(int size)
    {
        _server.playerareasize = size;
        _server.centerareasize = size / 2;
        _server.DrawDistance = size / 2;
    }

    public bool IsSinglePlayer() => _server.IsSinglePlayer;

    public void AddPermissionArea(int x1, int y1, int z1, int x2, int y2, int z2, int permissionLevel)
    {
        AreaConfig area = new()
        {
            Level = permissionLevel,
            Coords = string.Format("{0},{1},{2},{3},{4},{5}", x1, y1, z1, x2, y2, z2)
        };
        _config.Areas.Add(area);
        _config.ConfigNeedsSaving = true;
    }

    public void RemovePermissionArea(int x1, int y1, int z1, int x2, int y2, int z2)
    {
        for (int i = _config.Areas.Count - 1; i >= 0; i--)
        {
            string coords = string.Format("{0},{1},{2},{3},{4},{5}", x1, y1, z1, x2, y2, z2);
            if (_config.Areas[i].Coords == coords)
            {
                _config.Areas.RemoveAt(i);
                _config.ConfigNeedsSaving = true;
            }
        }
    }

    public int GetPlayerPermissionLevel(int player) => _server.Clients[player].ClientGroup.Level;

    public void SetCreative(bool value) => _config.IsCreative = value;

    public void SetWorldSize(int x, int y, int z) => _serverMapStorage.Reset(x, y, z);

    public int[] GetScreenResolution(int player) => _server.Clients[player].WindowSize;

    public void SendDialog(int player, string id, Dialog dialog) => _server.SendDialog(player, id, dialog);

    public void SetPlayerModel(int player, string model, string texture)
    {
        _server.Clients[player].Model = model;
        _server.Clients[player].Texture = texture;
        _server.PlayerEntitySetDirty(player);
    }
    public void RenderHint(RenderHint hint) => _server.RenderHint = hint;
    public void EnableFreemove(int player, bool enable) => _server.SendFreemoveState(player, enable);

    public int GetPlayerHealth(int player)
    {
        string name = GetPlayerName(player);
        return _server.GetPlayerStats(name).CurrentHealth;
    }

    public int GetPlayerMaxHealth(int player)
    {
        string name = GetPlayerName(player);
        return _server.GetPlayerStats(name).MaxHealth;
    }

    public void SetPlayerHealth(int player, int health, int maxhealth)
    {
        string name = GetPlayerName(player);
        _server.GetPlayerStats(name).CurrentHealth = health;
        _server.GetPlayerStats(name).MaxHealth = maxhealth;
        _server.Clients[player].IsPlayerStatsDirty = true;
        _server.NotifyPlayerStats(player);
    }

    public int GetPlayerOxygen(int player)
    {
        string name = GetPlayerName(player);
        return _server.GetPlayerStats(name).CurrentOxygen;
    }

    public int GetPlayerMaxOxygen(int player)
    {
        string name = GetPlayerName(player);
        return _server.GetPlayerStats(name).MaxOxygen;
    }

    public void SetPlayerOxygen(int player, int oxygen, int maxoxygen)
    {
        string name = GetPlayerName(player);
        _server.GetPlayerStats(name).CurrentOxygen = oxygen;
        _server.GetPlayerStats(name).MaxOxygen = maxoxygen;
        _server.Clients[player].IsPlayerStatsDirty = true;
        _server.NotifyPlayerStats(player);
    }

    public float[] GetDefaultSpawnPosition(int player)
    {
        Vector3i pos = _server.GetPlayerSpawnPositionMul32(player);
        return [(float)pos.X / 32, (float)pos.Z / 32, (float)pos.Y / 32];
    }

    public int[] GetDefaultSpawnPosition() => [_server.DefaultPlayerSpawn.X, _server.DefaultPlayerSpawn.Y, _server.DefaultPlayerSpawn.Z];

    public void SetDefaultSpawnPosition(int x, int y, int z)
    {
        if (IsValidPos(x, y, z))
        {
            // Will fail for numbers it cannot parse. Should not happen due to previous check.
            _server.ServerClient.DefaultSpawn.Coords = string.Format("{0},{1},{2}", x, y, z);
            _server.DefaultPlayerSpawn = new Vector3i(x, y, z);
            // Mark ServerClient as dirty for saving
            _server.ServerClientNeedsSaving = true;
        }
        else
        {
            Console.WriteLine("[Mod API] Invalid default spawn position given!");
        }
    }

    public string ServerName => _config.Name;

    public string ServerMotd => _config.Motd;

    [DllImport("libc")]
    private static extern int uname(IntPtr buf);

    private static bool checkedIsArm;
    private static bool isArm;

    public static bool IsArm
    {
        get
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix)
            {
                return false;
            }

            if (!checkedIsArm)
            {
                IntPtr buf = Marshal.AllocHGlobal(8192);
                if (uname(buf) == 0)
                {
                    // todo
                    for (int i = 0; i < 8192 - 3; i++)
                    {
                        if (Marshal.ReadByte(new IntPtr(buf.ToInt64() + i + 0)) == 'a'
                            && Marshal.ReadByte(new IntPtr(buf.ToInt64() + i + 1)) == 'r'
                            && Marshal.ReadByte(new IntPtr(buf.ToInt64() + i + 2)) == 'm')
                        {
                            isArm = true;
                        }
                    }
                }

                Marshal.FreeHGlobal(buf);
                checkedIsArm = true;
            }

            return isArm;
        }
    }

    public float[] MeasureTextSize(string text, DialogFont font)
    {
        if (IsArm)
        {
            // fixes crash
            return [text.Length * 1f * font.Size, 1.7f * font.Size];
        }
        else
        {
            using (Bitmap bmp = new(1, 1))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    SizeF size = g.MeasureString(text, new Font(font.FamilyName, font.Size, (FontStyle)font.FontStyle), new PointF(0, 0), new StringFormat(StringFormatFlags.MeasureTrailingSpaces));
                    return [size.Width, size.Height];
                }
            }
        }
    }

    public string ServerIp => "!SERVER_IP!";

    public string ServerPort => "!SERVER_PORT!";

    public float GetPlayerPing(int player) => _server.Clients[player].LastPing;

    public int AddBot(string name)
    {
        int id = _server.GenerateClientId();
        ClientOnServer c = new()
        {
            Id = id,
            IsBot = true,
            PlayerName = name
        };
        _server.Clients[id] = c;
        c.State = ClientStateOnServer.Playing;
        DummyNetwork network = new();
        c.Socket = new DummyNetConnection(network);
        c.Ping.Timeout = TimeSpan.MaxValue;
        c.chunksseen = new bool[_serverMapStorage.MapSizeX / GameConstants.ServerChunkSize
                                * _serverMapStorage.MapSizeY / GameConstants.ServerChunkSize * _serverMapStorage.MapSizeZ / GameConstants.ServerChunkSize];
        c.AssignGroup(_server.DefaultGroupRegistered);
        _server.PlayerEntitySetDirty(id);
        return id;
    }

    public bool IsBot(int player) => _server.Clients[player].IsBot;

    public void SetPlayerHeight(int player, float eyeheight, float modelheight)
    {
        _server.Clients[player].EyeHeight = eyeheight;
        _server.Clients[player].ModelHeight = modelheight;
        _server.PlayerEntitySetDirty(player);
    }

    public void DisablePrivilege(string privilege) => _server.Disabledprivileges[privilege] = true;

    public Inventory GetInventory(int player) => _server.GetPlayerInventory(_server.Clients[player].PlayerName);

    public int GetActiveMaterialSlot(int player) => _server.Clients[player].ActiveMaterialSlot;

    public void FollowPlayer(int player, int target, bool tpp) => _server.SendPacketFollow(player, target, tpp);

    public void SetPlayerSpectator(int player, bool isSpectator) => _server.Clients[player].IsSpectator = isSpectator;

    public bool IsPlayerSpectator(int player) => _server.Clients[player].IsSpectator;

    public BlockType GetBlockType(int block) => _blockRegistry.BlockTypes[block];

    public void NotifyAmmo(int player, Dictionary<int, int> totalAmmo) => _server.SendAmmo(player, totalAmmo);

    public void LogChat(string s) => _server.ChatLog(s);

    public void EnableExtraPrivilegeToAll(string privilege, bool enable)
    {
        if (enable)
        {
            _server.ExtraPrivileges[privilege] = true;
        }
        else
        {
            _server.ExtraPrivileges.Remove(privilege);
        }
    }

    public void LogServerEvent(string serverEvent) => _server.ServerEventLog(serverEvent);

    public void SetWorldDatabaseReadOnly(bool readOnly) => _chunkDb.ReadOnly = readOnly;

    public string[] ModPaths => [.. _server.ModPaths];

    public void SendExplosion(int player, float x, float y, float z, bool relativeposition, float range, float time) => _server.SendExplosion(player, x, y, z, relativeposition, range, time);

    public void DisconnectPlayer(int player) => _server.KillPlayer(player);

    public void DisconnectPlayer(int player, string message)
    {
        _server.SendPacket(player, ServerPackets.DisconnectPlayer(message));
        _server.KillPlayer(player);
    }

    public string GetGroupColor(int player) => _server.GetGroupColor(player);

    public string GetGroupName(int player) => _server.GetGroupName(player);

    public void InstallHttpModule(string name, Func<string> description, IHttpModule module) => _server.InstallHttpModule(name, description, module);

    public int MaxPlayers => _config.MaxClients;

    public ServerClient GetServerClient() => _server.ServerClient;

    public long TotalReceivedBytes => _server.TotalReceivedBytes;

    public long TotalSentBytes => _server.TotalSentBytes;

    public void SetPlayerNameColor(int player, string color)
    {
        if (color.Equals("&0") || color.Equals("&1") || color.Equals("&2") || color.Equals("&3") ||
            color.Equals("&4") || color.Equals("&5") || color.Equals("&6") || color.Equals("&7") ||
            color.Equals("&8") || color.Equals("&9") || color.Equals("&a") || color.Equals("&b") ||
            color.Equals("&c") || color.Equals("&d") || color.Equals("&e") || color.Equals("&f"))
        {
            _server.Clients[player].DisplayColor = color;
            _server.PlayerEntitySetDirty(player);
        }
    }

    public int AutoRestartInterval => _config.AutoRestartCycle;

    public int ServerUptimeSeconds => (int)_server.Uptime.TotalSeconds;

    public void SendPlayerRedirect(int player, string ip, int port) => _server.SendServerRedirect(player, ip, port);

    public bool IsShuttingDown => gameExit.Exit;

    #region Deprecated methods
    public double GetCurrentYearTotal() => _server.GetTimer().Year;
    public double GetCurrentHourTotal() => _server.GetTimer().Hour;
    public double GetGameYearRealHours() => GetGameDayRealHours() * GetDaysPerYear();
    public void SetGameYearRealHours(double hours) => throw new NotImplementedException("SetGameYearRealHours is no longer supported!");
    #endregion
}
