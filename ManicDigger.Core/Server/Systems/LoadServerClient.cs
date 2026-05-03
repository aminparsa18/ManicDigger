using ManicDigger;
using OpenTK.Mathematics;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Server system responsible for loading and saving <c>ServerClient.txt</c>, which
/// stores player groups, registered client entries, and the default world spawn point.
/// <para>
/// On first run, if no file exists, defaults are generated and written to disk.
/// Like <c>ServerConfig.txt</c>, the file supports a legacy XML format that is
/// automatically upgraded to the current serializer format on load.
/// </para>
/// </summary>
public class ServerSystemLoadServerClient : ServerSystem
{
    private const string ClientFilename = "ServerClient.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IServerMapStorage _serverMapStorage;
    private readonly ILanguageService _languageService;

    public ServerSystemLoadServerClient(IModEvents modEvents, IServerMapStorage serverMapStorage, ILanguageService languageService) : base(modEvents)
    {
        _serverMapStorage = serverMapStorage;
        _languageService = languageService;
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void Initialize(Server server) => LoadServerClient(server);

    /// <inheritdoc/>
    protected override void OnUpdate(Server server, float dt)
    {
        if (server.ServerClientNeedsSaving)
        {
            server.ServerClientNeedsSaving = false;
            SaveServerClient(server);
        }
    }

    // -------------------------------------------------------------------------
    // Load
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads <c>ServerClient.txt</c> from the game config directory, then resolves
    /// the default player spawn point and the default guest/registered groups.
    /// <list type="bullet">
    ///   <item>If the file does not exist, defaults are written and the method continues.</item>
    ///   <item>If the file uses the current XML format it is deserialized directly.</item>
    ///   <item>If the file uses the legacy format, known fields are read via XPath and
    ///         the file is immediately re-saved in the current format.</item>
    /// </list>
    /// </summary>
    /// <exception cref="Exception">
    /// Thrown if the guest or registered default group name in the config does not
    /// match any group defined in <c>ServerClient.txt</c>.
    /// </exception>
    public void LoadServerClient(Server server)
    {
        string path = Path.Combine(GameStorePath.gamepathconfig, ClientFilename);

        if (!File.Exists(path))
        {
            Console.WriteLine(_languageService.ServerClientConfigNotFound());
            SaveServerClient(server);
        }
        else
        {
            TryLoadCurrentFormat(server, path);
        }

        ResolveSpawn(server);
        ResolveDefaultGroups(server);
        Console.WriteLine(_languageService.ServerClientConfigLoaded());
    }

    /// <summary>
    /// Attempts to deserialize <c>ServerClient.txt</c> using the current
    /// <see cref="XmlSerializer"/> format. Groups are sorted after a successful load.
    /// </summary>
    /// <returns><c>true</c> on success; <c>false</c> if deserialization fails.</returns>
    private static bool TryLoadCurrentFormat(Server server, string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            server.ServerClient = JsonSerializer.Deserialize<ServerClient>(json, JsonOptions)
                                  ?? new ServerClient();
            server.ServerClient.Groups.Sort();
            SaveServerClient(server);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Spawn resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves <see cref="Server.DefaultPlayerSpawn"/> from the loaded config.
    /// <list type="bullet">
    ///   <item>If no spawn is configured, the spawn is placed at the surface above
    ///         the centre of the map.</item>
    ///   <item>If a spawn is configured but has no Z component, the surface height
    ///         at the (X, Y) position is used.</item>
    ///   <item>If all three coordinates are present, they are used verbatim.</item>
    /// </list>
    /// When no configured spawn exists, <see cref="Server.DontSpawnPlayerInWater"/>
    /// is applied to push the position above any water surface.
    /// </summary>
    private void ResolveSpawn(Server server)
    {
        if (server.ServerClient.DefaultSpawn == null)
        {
            int x = _serverMapStorage.MapSizeX / 2;
            int y = _serverMapStorage.MapSizeY / 2;
            int z = VectorUtils.BlockHeight(_serverMapStorage, 0, x, y);
            server.DefaultPlayerSpawn = server.DontSpawnPlayerInWater(new Vector3i(x, y, z));
            return;
        }

        var spawn = server.ServerClient.DefaultSpawn;
        int spawnZ = spawn.z ?? VectorUtils.BlockHeight(_serverMapStorage, 0, spawn.x, spawn.y);
        server.DefaultPlayerSpawn = new Vector3i(spawn.x, spawn.y, spawnZ);
    }

    // -------------------------------------------------------------------------
    // Group resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves <see cref="Server.DefaultGroupGuest"/> and
    /// <see cref="Server.DefaultGroupRegistered"/> by matching the group names
    /// stored in the config against the loaded group list.
    /// </summary>
    /// <exception cref="Exception">
    /// Thrown if either group name cannot be found, indicating a misconfigured
    /// <c>ServerClient.txt</c>.
    /// </exception>
    private void ResolveDefaultGroups(Server server)
    {
        server.DefaultGroupGuest = server.ServerClient.Groups
            .Find(g => g.Name.Equals(server.ServerClient.DefaultGroupGuests))
            ?? throw new Exception(_languageService.ServerClientConfigGuestGroupNotFound());

        server.DefaultGroupRegistered = server.ServerClient.Groups
            .Find(g => g.Name.Equals(server.ServerClient.DefaultGroupRegistered))
            ?? throw new Exception(_languageService.ServerClientConfigRegisteredGroupNotFound());
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serializes <see cref="Server.ServerClient"/> to <c>ServerClient.txt</c>.
    /// If the object has not yet been initialized, or if its groups or client
    /// lists are empty, defaults are populated before saving.
    /// </summary>
    public static void SaveServerClient(Server server)
    {
        Directory.CreateDirectory(GameStorePath.gamepathconfig);

        server.ServerClient ??= new ServerClient();

        if (server.ServerClient.Groups.Count == 0)
        {
            server.ServerClient.Groups = ServerClientMisc.getDefaultGroups();
        }

        if (server.ServerClient.Clients.Count == 0)
        {
            server.ServerClient.Clients = ServerClientMisc.getDefaultClients();
        }

        server.ServerClient.Clients.Sort();

        File.WriteAllText(
            Path.Combine(GameStorePath.gamepathconfig, ClientFilename),
            JsonSerializer.Serialize(server.ServerClient, JsonOptions));
    }
}