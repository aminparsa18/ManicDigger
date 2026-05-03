namespace ManicDigger;

public interface IPlayerStatusService
{
    Dictionary<string, PacketServerPlayerStats> PlayerStats { get; }

    PacketServerPlayerStats GetPlayerStats(string playername);

    void NotifyPlayerStats(int clientid);
}

public class PlayerStatusService : IPlayerStatusService
{
    private readonly IServerClientService _serverClientService;
    private readonly IServerPacketService _serverPacketService;

    public PlayerStatusService(IServerClientService serverClientService, IServerPacketService serverPacketService)
    {
        _serverClientService = serverClientService;
        _serverPacketService = serverPacketService;
    }

    public Dictionary<string, PacketServerPlayerStats> PlayerStats { get; } = [];

    public PacketServerPlayerStats GetPlayerStats(string playername)
    {
        if (!PlayerStats.TryGetValue(playername, out PacketServerPlayerStats? value))
        {
            value = StartPlayerStats();
            PlayerStats[playername] = value;
        }

        return value;
    }

    private static PacketServerPlayerStats StartPlayerStats()
    {
        PacketServerPlayerStats p = new()
        {
            CurrentHealth = 20,
            MaxHealth = 20,
            CurrentOxygen = 10,
            MaxOxygen = 10
        };
        return p;
    }

    public void NotifyPlayerStats(int clientid)
    {
        ClientOnServer c = _serverClientService.Clients[clientid];
        if (c.IsPlayerStatsDirty && c.PlayerName != GameConstants.InvalidPlayerName)
        {
            PacketServerPlayerStats stats = GetPlayerStats(c.PlayerName);
            _serverPacketService.SendPacket(clientid, ServerPackets.PlayerStats(stats.CurrentHealth, stats.MaxHealth, stats.CurrentOxygen, stats.MaxOxygen));
            c.IsPlayerStatsDirty = false;
        }
    }
}
