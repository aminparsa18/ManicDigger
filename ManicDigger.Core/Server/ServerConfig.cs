public class ServerConfig : IServerConfig
{
    public int Format { get; set; }             //XML Format Version Number
    public string Name { get; set; }
    public string Motd { get; set; }            //Message of the day
    public string WelcomeMessage { get; set; }  //Displays when the user logs in.
    public int Port { get; set; }               //Port the server runs on
    public int MaxClients { get; set; }
    public int AutoRestartCycle { get; set; }
    public bool ServerMonitor { get; set; }
    public int ClientConnectionTimeout { get; set; }
    public int ClientPlayingTimeout { get; set; }
    public bool BuildLogging { get; set; }
    public bool ServerEventLogging { get; set; }
    public bool ChatLogging { get; set; }
    public bool AllowScripting { get; set; }
    public string Key { get; set; }             //GUID to uniquely identify the server
    public bool IsCreative { get; set; }        //Is this a free build server?
    public bool Public { get; set; }            //Advertise this server?
    public string Password { get; set; }
    public bool AllowGuests { get; set; }
    public bool Monsters { get; set; }
    public int MapSizeX { get; set; }
    public int MapSizeY { get; set; }
    public int MapSizeZ { get; set; }
    public List<AreaConfig> Areas { get; set; }
    public int Seed { get; set; }
    public bool RandomSeed { get; set; }
    public bool EnableHTTPServer { get; set; }
    public bool AllowSpectatorUse { get; set; }
    public bool AllowSpectatorBuild { get; set; }
    public string ServerLanguage { get; set; }
    public int PlayerDrawDistance { get; set; }
    public bool EnablePlayerPushing { get; set; }
    public bool ConfigNeedsSaving { get; set; }

    public bool IsPasswordProtected() => !string.IsNullOrEmpty(Password);

    public bool CanUserBuild(ClientOnServer client, int x, int y, int z)
    {
        bool canBuild = false;
        // TODO: fast tree datastructure
        foreach (AreaConfig area in Areas)
        {
            if (area.IsInCoords(x, y, z))
            {
                if (area.CanUserBuild(client))
                {
                    return true;
                }
            }
        }

        return canBuild;
    }

    public ServerConfig()
    {
        //Set Defaults
        Format = 1;
        Name = "Manic Digger server";
        Motd = "MOTD";
        WelcomeMessage = "Welcome to my Manic Digger server!";
        Port = 25565;
        MaxClients = 16;
        ServerMonitor = true;
        ClientConnectionTimeout = 600;
        ClientPlayingTimeout = 60;
        BuildLogging = false;
        ServerEventLogging = false;
        ChatLogging = false;
        AllowScripting = false;
        Key = Guid.NewGuid().ToString();
        IsCreative = true;
        Public = true;
        AllowGuests = true;
        Monsters = false;
        MapSizeX = 9984;
        MapSizeY = 9984;
        MapSizeZ = 128;
        Areas = [];
        AutoRestartCycle = 6;
        Seed = 0;
        RandomSeed = true;
        EnableHTTPServer = false;
        AllowSpectatorUse = false;
        AllowSpectatorBuild = false;
        ServerLanguage = "en";
        PlayerDrawDistance = 128;
        EnablePlayerPushing = true;
    }

    public void CopyFrom(ServerConfig source)
    {
        Format = source.Format;
        Name = source.Name;
        Motd = source.Motd;
        WelcomeMessage = source.WelcomeMessage;
        Port = source.Port;
        MaxClients = source.MaxClients;
        AutoRestartCycle = source.AutoRestartCycle;
        ServerMonitor = source.ServerMonitor;
        ClientConnectionTimeout = source.ClientConnectionTimeout;
        ClientPlayingTimeout = source.ClientPlayingTimeout;
        BuildLogging = source.BuildLogging;
        ServerEventLogging = source.ServerEventLogging;
        ChatLogging = source.ChatLogging;
        AllowScripting = source.AllowScripting;
        Key = source.Key;
        IsCreative = source.IsCreative;
        Public = source.Public;
        Password = source.Password;
        AllowGuests = source.AllowGuests;
        Monsters = source.Monsters;
        MapSizeX = source.MapSizeX;
        MapSizeY = source.MapSizeY;
        MapSizeZ = source.MapSizeZ;
        Areas = source.Areas;
        Seed = source.Seed;
        RandomSeed = source.RandomSeed;
        EnableHTTPServer = source.EnableHTTPServer;
        AllowSpectatorUse = source.AllowSpectatorUse;
        AllowSpectatorBuild = source.AllowSpectatorBuild;
        ServerLanguage = source.ServerLanguage;
        PlayerDrawDistance = source.PlayerDrawDistance;
        EnablePlayerPushing = source.EnablePlayerPushing;
    }
}

public class AreaConfig
{
    public int Id { get; set; }
    private int x1;
    private int x2;
    private int y1;
    private int y2;
    private int z1;
    private int z2;
    private string coords;
    public List<string> PermittedGroups { get; set; }
    public List<string> PermittedUsers { get; set; }
    public int? Level { get; set; }

    public AreaConfig()
    {
        Id = -1;
        Coords = "0,0,0,0,0,0";
        PermittedGroups = [];
        PermittedUsers = [];
    }

    public string Coords
    {
        get { return coords; }
        set
        {
            coords = value;
            string[] myCoords = Coords.Split([',']);
            x1 = Convert.ToInt32(myCoords[0]);
            x2 = Convert.ToInt32(myCoords[3]);
            y1 = Convert.ToInt32(myCoords[1]);
            y2 = Convert.ToInt32(myCoords[4]);
            z1 = Convert.ToInt32(myCoords[2]);
            z2 = Convert.ToInt32(myCoords[5]);
        }
    }

    public bool IsInCoords(int x, int y, int z)
        => x >= x1 && x <= x2 && y >= y1 && y <= y2 && z >= z1 && z <= z2;

    public bool ContainsBox(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        => minX >= x1 && maxX <= x2 && minY >= y1 && maxY <= y2 && minZ >= z1 && maxZ <= z2;

    public bool CanUserBuild(ClientOnServer client)
    {
        if (Level != null && client.ClientGroup.Level >= Level)
        {
            return true;
        }

        if (PermittedGroups.Contains(client.ClientGroup.Name))
        {
            return true;
        }

        if (PermittedUsers.Any(u => u.Equals(client.PlayerName, StringComparison.InvariantCultureIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    public override string ToString()
    {
        string groups = string.Join(",", PermittedGroups);
        string users = string.Join(",", PermittedUsers);
        string level = Level?.ToString() ?? "";
        return $"{Id}:{Coords}:{groups}:{users}:{level}";
    }
}

public static class ServerConfigMisc
{
    public static List<AreaConfig> getDefaultAreas()
    {
        List<AreaConfig> defaultAreas = [];

        AreaConfig publicArea = new()
        {
            Id = 1,
            Coords = "0,0,1,9984,5000,128"
        };
        publicArea.PermittedGroups.Add("Guest");
        defaultAreas.Add(publicArea);
        AreaConfig protectedArea = new()
        {
            Id = 2,
            Coords = "0,0,1,9984,9984,128"
        };
        protectedArea.PermittedGroups.Add("Builder");
        protectedArea.PermittedGroups.Add("Moderator");
        protectedArea.PermittedGroups.Add("Admin");
        defaultAreas.Add(protectedArea);

        return defaultAreas;
    }
}
