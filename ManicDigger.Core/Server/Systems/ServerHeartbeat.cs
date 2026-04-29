using System.Net.Http.Headers;

/// <summary>
/// Sends a heartbeat to the public server list every 60 seconds when the server
/// is configured as public. The first heartbeat fires immediately on startup by
/// initialising the elapsed timer to the interval.
/// The received server hash is printed to the console once on first successful contact.
/// </summary>
public class ServerSystemHeartbeat : ServerSystem
{
    private const float HeartbeatInterval = 60f;
    private const string HashPrefix = "server=";

    private float elapsed;
    private bool hashPrinted;
    private readonly ServerHeartbeat heartbeat = new();

    public ServerSystemHeartbeat()
    {
        // Pre-fill the timer so the first heartbeat fires on the first tick
        elapsed = HeartbeatInterval;
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnUpdate(Server server, float dt)
    {
        elapsed += dt;
        while (elapsed >= HeartbeatInterval)
        {
            elapsed -= HeartbeatInterval;
            if (server.Config.Public)
            {
                heartbeat.GameMode = server.GameMode;
                ThreadPool.QueueUserWorkItem(async (_) => await SendHeartbeat(server));
            }
        }
    }

    // -------------------------------------------------------------------------
    // Heartbeat
    // -------------------------------------------------------------------------

    /// <summary>
    /// Populates the heartbeat payload from the current server state and dispatches
    /// it asynchronously. Bot players are excluded from the player list.
    /// Errors are logged briefly in release builds and in full in debug builds.
    /// </summary>
    public async Task SendHeartbeat(Server server)
    {
        if (server.Config?.Key == null) return;

        heartbeat.Name = server.Config.Name;
        heartbeat.MaxClients = server.Config.MaxClients;
        heartbeat.PasswordProtected = server.Config.IsPasswordProtected();
        heartbeat.AllowGuests = server.Config.AllowGuests;
        heartbeat.Port = server.Config.Port;
        heartbeat.Version = GameVersion.Version;
        heartbeat.Key = server.Config.Key;
        heartbeat.Motd = server.Config.Motd;

        var playerNames = new List<string>();
        lock (server.Clients)
        {
            foreach (var (_, client) in server.Clients)
            {
                if (!client.IsBot)
                    playerNames.Add(client.PlayerName);
            }
        }
        heartbeat.Players = playerNames;
        heartbeat.UsersCount = playerNames.Count;

        try
        {
            //TODO: its not hosted yet
           // await heartbeat.SendHeartbeatAsync();
            server.ReceivedKey = heartbeat.ReceivedKey;

            if (!hashPrinted)
            {
                Console.WriteLine($"hash: {StripHashPrefix(heartbeat.ReceivedKey)}");
                hashPrinted = true;
            }

            Console.WriteLine(server.Language.ServerHeartbeatSent());
        }
        catch (Exception e)
        {
#if DEBUG
            Console.WriteLine(e.ToString());
#endif
            Console.WriteLine("{0} ({1})", server.Language.ServerHeartbeatError(), e.Message);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Strips the <c>"server="</c> prefix from the received key string, returning
    /// only the hash value. Returns an empty string if parsing fails.
    /// </summary>
    private static string StripHashPrefix(string hash)
    {
        try
        {
            int idx = hash?.IndexOf(HashPrefix) ?? -1;
            return idx >= 0 ? hash[(idx + HashPrefix.Length)..] : hash ?? "";
        }
        catch
        {
            return "";
        }
    }
}

// =============================================================================

/// <summary>
/// Encapsulates the heartbeat payload and the HTTP logic for posting it to the
/// public server list. The list URL is fetched once and cached for the lifetime
/// of the instance.
/// </summary>
public class ServerHeartbeat
{
    public string Name { get; set; } = "";
    public string Key { get; set; } = Guid.NewGuid().ToString();
    public int MaxClients { get; set; } = 16;
    public bool Public { get; set; } = true;
    public bool PasswordProtected { get; set; }
    public bool AllowGuests { get; set; } = true;
    public int Port { get; set; } = 25565;
    public string Version { get; set; } = "Unknown";
    public List<string> Players { get; set; } = [];
    public int UsersCount { get; set; }
    public string Motd { get; set; } = "";
    public string GameMode { get; set; }
    public string ReceivedKey { get; set; }

    private string listUrl;

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true }
        }
    };

    /// <summary>
    /// Posts the current heartbeat payload to the server list. The list endpoint
    /// URL is resolved once on first call and cached. <see cref="ReceivedKey"/>
    /// is populated with the server's response after a successful post.
    /// </summary>
    public async Task SendHeartbeatAsync()
    {
        listUrl ??= await HttpClient.GetStringAsync("http://manicdigger.sourceforge.net/heartbeat.txt");

        var formData = new Dictionary<string, string>
        {
            ["name"] = Name,
            ["max"] = MaxClients.ToString(),
            ["public"] = Public.ToString(),
            ["passwordProtected"] = PasswordProtected.ToString(),
            ["allowGuests"] = AllowGuests.ToString(),
            ["port"] = Port.ToString(),
            ["version"] = Version,
            ["fingerprint"] = Key.Replace("-", ""),
            ["users"] = UsersCount.ToString(),
            ["motd"] = Motd,
            ["gamemode"] = GameMode,
            ["players"] = string.Join(",", Players),
        };

        using var content = new FormUrlEncodedContent(formData);
        using var response = await HttpClient.PostAsync(listUrl, content);
        response.EnsureSuccessStatusCode();
        ReceivedKey = await response.Content.ReadAsStringAsync();
    }
}