using OpenTK.Mathematics;
using System.Runtime.InteropServices;

namespace ManicDigger;

public class ServerModManager(IGameExit gameExit, IBlockRegistry blockRegistry, IChunkDbCompressed chunkDb) : IServerModManager
{
    private readonly IGameExit gameExit = gameExit;
    private readonly IBlockRegistry _blockRegistry = blockRegistry;
    private readonly IChunkDbCompressed _chunkDb = chunkDb;

    public int GetMaxBlockTypes() => GameConstants.MAX_BLOCKTYPES;

    public void SetBlockType(int id, string name, BlockType block)
    {
        block.Sounds ??= defaultSounds;
        server.SetBlockType(id, name, block);
    }

    public void SetBlockType(string name, BlockType block)
    {
        block.Sounds ??= defaultSounds;
        server.SetBlockType(name, block);
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

    public int GetMapSizeX() => server.Map.MapSizeX;
    public int GetMapSizeY() => server.Map.MapSizeY;
    public int GetMapSizeZ() => server.Map.MapSizeZ;

    public int GetBlock(int x, int y, int z) => server.Map.GetBlock(x, y, z);

    public string GetBlockName(int blockType) => _blockRegistry.BlockTypes[blockType].Name;

    public string GetBlockNameAt(int x, int y, int z) => GetBlockName(GetBlock(x, y, z));

    public void SetBlock(int x, int y, int z, int tileType) => server.SetBlockAndNotify(x, y, z, tileType);

    private Server server;
    public void Start(Server server) => this.server = server;

    public void SetSunLevels(int[] sunLevels) => server.SetSunLevels(sunLevels);

    public void SetLightLevels(float[] lightLevels) => server.SetLightLevels(lightLevels);

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
        server.CraftingRecipes.Add(r);
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
        server.CraftingRecipes.Add(r);
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
        server.CraftingRecipes.Add(r);
    }

    public void SetString(string language, string id, string translation) => server.Language.Override(language, id, translation);

    public string GetString(string id)
        //Returns string depending on server language
        => server.Language.Get(id);

    public bool IsValidPos(int x, int y, int z) => VectorUtils.IsValidPos(server.Map, x, y, z);

    public void RegisterTimer(Action a, double interval) => server.Timers[new Timer() { INTERVAL = interval }] = delegate { a(); };

    public void PlaySoundAt(int posx, int posy, int posz, string sound) => server.PlaySoundAt(posx, posy, posz, sound);

    public void PlaySoundAt(int x, int y, int z, string sound, int range) => server.PlaySoundAt(x, y, z, sound, range);

    public int NearestPlayer(int x, int y, int z)
    {
        int closeplayer = -1;
        int closedistance = -1;
        foreach (KeyValuePair<int, ClientOnServer> k in server.Clients)
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
        Inventory inventory = server.GetPlayerInventory(server.GetClient(player).PlayerName);

        InventoryItem item = new()
        {
            InventoryItemType = InventoryItemType.Block,
            BlockCount = amount,
            BlockId = _blockRegistry.WhenPlayerPlacesGetsConvertedTo[block]
        };
        server.GetInventoryUtil(inventory).GrabItem(item, 0);
    }

    public bool PlayerHasPrivilege(int player, string privilege) => server.PlayerHasPrivilege(player, privilege);

    public bool IsCreative => server.Config.IsCreative;

    public bool IsBlockFluid(int block) => _blockRegistry.BlockTypes[block].IsFluid();

    public void NotifyInventory(int player)
    {
        server.GetClient(player).IsInventoryDirty = true;
        server.NotifyInventory(player);
    }

    public string ColorError => server.colorError;

    public void SendMessage(int player, string p) => server.SendMessage(player, p);

    public void RegisterPrivilege(string p)
    {
        // Add to list of all available privileges on server
        if (!server.AllPrivileges.Contains(p))
        {
            server.AllPrivileges.Add(p);
        }
        // Direct modification of console client as mods are loaded after privileges are assigned
        if (!server.ServerConsoleClient.Privileges.Contains(p))
        {
            server.ServerConsoleClient.Privileges.Add(p);
        }
    }

    public bool IsTransparentForLight(int p) => Game.IsTransparentForLight(_blockRegistry.BlockTypes[p]);

    public void RegisterOptionBool(string optionname, bool default_) => modoptions[optionname] = default_;

    private readonly Dictionary<string, object> modoptions = [];

    public int GetChunkSize() => server.ChunkSize;

    public object GetOption(string optionname) => modoptions[optionname];

    public int Seed => server.Seed;

    public int Index3d(int x, int y, int h, int sizex, int sizey) => (((h * sizey) + y) * sizex) + x;

    public void SetDefaultSounds(SoundSet defaultSounds) => this.defaultSounds = defaultSounds;
    private SoundSet defaultSounds;

    public byte[] GetGlobalData(string name)
    {
        if (server.ModData.TryGetValue(name, out byte[]? value))
        {
            return value;
        }

        return null;
    }

    public void SetGlobalData(string name, byte[] value) => server.ModData[name] = value;

    public void RegisterOnLoad(Action f) => server.OnLoad.Add(f);

    public void RegisterOnSave(Action f) => server.OnSave.Add(f);

    public string GetPlayerIp(int player) => server.GetClient(player).Socket.RemoteEndPoint().AddressToString();

    public string GetPlayerName(int player) => server.GetClient(player).PlayerName;

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

    public void SendMessageToAll(string message) => server.SendMessageToAll(message);

    public void RegisterCommandHelp(string command, string help) => server.commandhelps[command] = help;

    public void AddToStartInventory(string blocktype, int amount) => _blockRegistry.StartInventoryAmount[GetBlockId(blocktype)] = amount;

    public long GetCurrentTick() => server.GetSimulationCurrentFrame();

    public void SetDaysPerYear(int days)
    {
        if (days > 0)
        {
            server.GetTimer().DaysPerYear = days;
        }
        else
        {
            throw new ArgumentOutOfRangeException("The number of days per year must be greater than 0!");
        }
    }

    public int GetDaysPerYear() => server.GetTimer().DaysPerYear;

    public int GetHour() => server.GetTimer().Hour;

    public double GetTotalHours() => server.GetTimer().HourTotal;

    public int GetDay() => server.GetTimer().Day;

    public double GetTotalDays() => server.GetTimer().DaysTotal;

    public int GetYear() => server.GetTimer().Year;

    public int GetSeason() => server.GetTimer().Season;

    public void UpdateBlockTypes()
    {
        foreach (KeyValuePair<int, ClientOnServer> k in server.Clients)
        {
            server.SendBlockTypes(k.Key);
        }
    }

    public double GetGameDayRealHours()
    {
        double nSecondsPerDay = TimeSpan.FromDays(1).TotalSeconds;

        int nSpeed = server.GetTimer().SpeedOfTime;
        double nSeconds = nSecondsPerDay / nSpeed;

        double nHours = TimeSpan.FromSeconds(nSeconds).TotalHours;

        return nHours;
    }

    public void SetGameDayRealHours(double hours)
    {
        double nSecondsPerDay = TimeSpan.FromDays(1).TotalSeconds;

        double nSecondsGiven = TimeSpan.FromHours(hours).TotalSeconds;

        server.GetTimer().SpeedOfTime = (int)(nSecondsPerDay / nSecondsGiven);
    }

    public void EnableShadows(bool value) => server.EnableShadows = value;

    public float GetPlayerPositionX(int player) => (float)server.GetClient(player).PositionMul32GlX / 32;

    public float GetPlayerPositionY(int player) => (float)server.GetClient(player).PositionMul32GlZ / 32;

    public float GetPlayerPositionZ(int player) => (float)server.GetClient(player).PositionMul32GlY / 32;

    public void SetPlayerPosition(int player, float x, float y, float z)
    {
        ServerEntityPositionAndOrientation pos;
        if (server.Clients[player].PositionOverride == null)
        {
            //No position override so far. Clone from player position
            pos = server.Clients[player].Entity.Position.Clone();
        }
        else
        {
            //Position has already been modified. Clone from override to prevent data loss
            pos = server.Clients[player].PositionOverride.Clone();
        }

        pos.X = x;
        pos.Y = z;
        pos.Z = y;
        server.Clients[player].PositionOverride = pos;
    }

    public int GetPlayerHeading(int player) => server.GetClient(player).PositionHeading;

    public int GetPlayerPitch(int player) => server.GetClient(player).PositionPitch;

    public int GetPlayerStance(int player) => server.GetClient(player).Stance;

    public void SetPlayerOrientation(int player, int heading, int pitch, int stance)
    {
        ServerEntityPositionAndOrientation pos;
        if (server.Clients[player].PositionOverride == null)
        {
            //No position override so far. Clone from player position
            pos = server.Clients[player].Entity.Position.Clone();
        }
        else
        {
            //Position has already been modified. Clone from override to prevent data loss
            pos = server.Clients[player].PositionOverride.Clone();
        }

        pos.Heading = (byte)heading;
        pos.Pitch = (byte)pitch;
        pos.Stance = (byte)stance;
        server.Clients[player].PositionOverride = pos;
    }

    public int[] AllPlayers()
    {
        List<int> players = [];
        foreach (KeyValuePair<int, ClientOnServer> k in server.Clients)
        {
            players.Add(k.Key);
        }

        return [.. players];
    }

    public void SetPlayerAreaSize(int size)
    {
        server.playerareasize = size;
        server.centerareasize = size / 2;
        server.DrawDistance = size / 2;
    }

    public bool IsSinglePlayer() => server.IsSinglePlayer;

    public void AddPermissionArea(int x1, int y1, int z1, int x2, int y2, int z2, int permissionLevel)
    {
        AreaConfig area = new()
        {
            Level = permissionLevel,
            Coords = string.Format("{0},{1},{2},{3},{4},{5}", x1, y1, z1, x2, y2, z2)
        };
        server.Config.Areas.Add(area);
        server.ConfigNeedsSaving = true;
    }

    public void RemovePermissionArea(int x1, int y1, int z1, int x2, int y2, int z2)
    {
        for (int i = server.Config.Areas.Count - 1; i >= 0; i--)
        {
            string coords = string.Format("{0},{1},{2},{3},{4},{5}", x1, y1, z1, x2, y2, z2);
            if (server.Config.Areas[i].Coords == coords)
            {
                server.Config.Areas.RemoveAt(i);
                server.ConfigNeedsSaving = true;
            }
        }
    }

    public int GetPlayerPermissionLevel(int player) => server.Clients[player].ClientGroup.Level;

    public void SetCreative(bool value) => server.Config.IsCreative = value;

    public void SetWorldSize(int x, int y, int z) => server.Map.Reset(x, y, z);

    public int[] GetScreenResolution(int player) => server.Clients[player].WindowSize;

    public void SendDialog(int player, string id, Dialog dialog) => server.SendDialog(player, id, dialog);

    public void SetPlayerModel(int player, string model, string texture)
    {
        server.Clients[player].Model = model;
        server.Clients[player].Texture = texture;
        server.PlayerEntitySetDirty(player);
    }
    public void RenderHint(RenderHint hint) => server.RenderHint = hint;
    public void EnableFreemove(int player, bool enable) => server.SendFreemoveState(player, enable);

    public int GetPlayerHealth(int player)
    {
        string name = GetPlayerName(player);
        return server.GetPlayerStats(name).CurrentHealth;
    }

    public int GetPlayerMaxHealth(int player)
    {
        string name = GetPlayerName(player);
        return server.GetPlayerStats(name).MaxHealth;
    }

    public void SetPlayerHealth(int player, int health, int maxhealth)
    {
        string name = GetPlayerName(player);
        server.GetPlayerStats(name).CurrentHealth = health;
        server.GetPlayerStats(name).MaxHealth = maxhealth;
        server.Clients[player].IsPlayerStatsDirty = true;
        server.NotifyPlayerStats(player);
    }

    public int GetPlayerOxygen(int player)
    {
        string name = GetPlayerName(player);
        return server.GetPlayerStats(name).CurrentOxygen;
    }

    public int GetPlayerMaxOxygen(int player)
    {
        string name = GetPlayerName(player);
        return server.GetPlayerStats(name).MaxOxygen;
    }

    public void SetPlayerOxygen(int player, int oxygen, int maxoxygen)
    {
        string name = GetPlayerName(player);
        server.GetPlayerStats(name).CurrentOxygen = oxygen;
        server.GetPlayerStats(name).MaxOxygen = maxoxygen;
        server.Clients[player].IsPlayerStatsDirty = true;
        server.NotifyPlayerStats(player);
    }

    public float[] GetDefaultSpawnPosition(int player)
    {
        Vector3i pos = server.GetPlayerSpawnPositionMul32(player);
        return [(float)pos.X / 32, (float)pos.Z / 32, (float)pos.Y / 32];
    }

    public int[] GetDefaultSpawnPosition() => [server.DefaultPlayerSpawn.X, server.DefaultPlayerSpawn.Y, server.DefaultPlayerSpawn.Z];

    public void SetDefaultSpawnPosition(int x, int y, int z)
    {
        if (IsValidPos(x, y, z))
        {
            // Will fail for numbers it cannot parse. Should not happen due to previous check.
            server.ServerClient.DefaultSpawn.Coords = string.Format("{0},{1},{2}", x, y, z);
            server.DefaultPlayerSpawn = new Vector3i(x, y, z);
            // Mark ServerClient as dirty for saving
            server.ServerClientNeedsSaving = true;
        }
        else
        {
            Console.WriteLine("[Mod API] Invalid default spawn position given!");
        }
    }

    public string ServerName => server.Config.Name;

    public string ServerMotd => server.Config.Motd;

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

    public float GetPlayerPing(int player) => server.Clients[player].LastPing;

    public int AddBot(string name)
    {
        int id = server.GenerateClientId();
        ClientOnServer c = new()
        {
            Id = id,
            IsBot = true,
            PlayerName = name
        };
        server.Clients[id] = c;
        c.State = ClientStateOnServer.Playing;
        DummyNetwork network = new();
        c.Socket = new DummyNetConnection(network);
        c.Ping.Timeout = TimeSpan.MaxValue;
        c.chunksseen = new bool[server.Map.MapSizeX / server.ChunkSize
                                * server.Map.MapSizeY / server.ChunkSize * server.Map.MapSizeZ / server.ChunkSize];
        c.AssignGroup(server.DefaultGroupRegistered);
        server.PlayerEntitySetDirty(id);
        return id;
    }

    public bool IsBot(int player) => server.Clients[player].IsBot;

    public void SetPlayerHeight(int player, float eyeheight, float modelheight)
    {
        server.Clients[player].EyeHeight = eyeheight;
        server.Clients[player].ModelHeight = modelheight;
        server.PlayerEntitySetDirty(player);
    }

    public void DisablePrivilege(string privilege) => server.Disabledprivileges[privilege] = true;

    public Inventory GetInventory(int player) => server.GetPlayerInventory(server.Clients[player].PlayerName);

    public int GetActiveMaterialSlot(int player) => server.Clients[player].ActiveMaterialSlot;

    public void FollowPlayer(int player, int target, bool tpp) => server.SendPacketFollow(player, target, tpp);

    public void SetPlayerSpectator(int player, bool isSpectator) => server.Clients[player].IsSpectator = isSpectator;

    public bool IsPlayerSpectator(int player) => server.Clients[player].IsSpectator;

    public BlockType GetBlockType(int block) => _blockRegistry.BlockTypes[block];

    public void NotifyAmmo(int player, Dictionary<int, int> totalAmmo) => server.SendAmmo(player, totalAmmo);

    public void LogChat(string s) => server.ChatLog(s);

    public void EnableExtraPrivilegeToAll(string privilege, bool enable)
    {
        if (enable)
        {
            server.ExtraPrivileges[privilege] = true;
        }
        else
        {
            server.ExtraPrivileges.Remove(privilege);
        }
    }

    public void LogServerEvent(string serverEvent) => server.ServerEventLog(serverEvent);

    public void SetWorldDatabaseReadOnly(bool readOnly) => _chunkDb.ReadOnly = readOnly;

    public string CurrentWorld => server.GetSaveFilename();

    public void LoadWorld(string filename) => server.LoadDatabase(filename);

    public string[] ModPaths => [.. server.ModPaths];

    public void SendExplosion(int player, float x, float y, float z, bool relativeposition, float range, float time) => server.SendExplosion(player, x, y, z, relativeposition, range, time);

    public void DisconnectPlayer(int player) => server.KillPlayer(player);

    public void DisconnectPlayer(int player, string message)
    {
        server.SendPacket(player, ServerPackets.DisconnectPlayer(message));
        server.KillPlayer(player);
    }

    public string GetGroupColor(int player) => server.GetGroupColor(player);

    public string GetGroupName(int player) => server.GetGroupName(player);

    public void InstallHttpModule(string name, Func<string> description, IHttpModule module) => server.InstallHttpModule(name, description, module);

    public int MaxPlayers => server.Config.MaxClients;

    public ServerClient GetServerClient() => server.ServerClient;

    public long TotalReceivedBytes => server.TotalReceivedBytes;

    public long TotalSentBytes => server.TotalSentBytes;

    public void SetPlayerNameColor(int player, string color)
    {
        if (color.Equals("&0") || color.Equals("&1") || color.Equals("&2") || color.Equals("&3") ||
            color.Equals("&4") || color.Equals("&5") || color.Equals("&6") || color.Equals("&7") ||
            color.Equals("&8") || color.Equals("&9") || color.Equals("&a") || color.Equals("&b") ||
            color.Equals("&c") || color.Equals("&d") || color.Equals("&e") || color.Equals("&f"))
        {
            server.Clients[player].DisplayColor = color;
            server.PlayerEntitySetDirty(player);
        }
    }

    public int AutoRestartInterval => server.Config.AutoRestartCycle;

    public int ServerUptimeSeconds => (int)server.Uptime.TotalSeconds;

    public void SendPlayerRedirect(int player, string ip, int port) => server.SendServerRedirect(player, ip, port);

    public bool IsShuttingDown => gameExit.Exit;

    #region Deprecated methods
    public double GetCurrentYearTotal() => server.GetTimer().Year;
    public double GetCurrentHourTotal() => server.GetTimer().Hour;
    public double GetGameYearRealHours() => GetGameDayRealHours() * GetDaysPerYear();
    public void SetGameYearRealHours(double hours) => throw new NotImplementedException("SetGameYearRealHours is no longer supported!");
    #endregion
}
