using ManicDigger;

public interface IServerClientService
{
    Dictionary<int, ClientOnServer> Clients { get; set; }

    public ClientOnServer? ServerConsoleClient { get; set; }

    int GenerateClientId();

    ClientOnServer? GetClient(int id);

    ClientOnServer GetClient(string name);

    bool ServerClientNeedsSaving { get; set; }

    ServerClient ServerClient { get; set; }
}

public class ServerClientService : IServerClientService
{
    public Dictionary<int, ClientOnServer> Clients { get; set; } = [];

    public ClientOnServer? ServerConsoleClient { get; set; } = new ClientOnServer()
    {
        Id = GameConstants.ServerConsoleId,
        PlayerName = "Server",
        QueryClient = false
    };

    public ClientOnServer? GetClient(int id)
    {
        return id == GameConstants.ServerConsoleId
            ? ServerConsoleClient
            : !Clients.TryGetValue(id, out ClientOnServer? value) ? null : value;
    }

    public ClientOnServer GetClient(string name)
    {
        if (ServerConsoleClient!.PlayerName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
        {
            return ServerConsoleClient;
        }

        foreach (KeyValuePair<int, ClientOnServer> k in Clients)
        {
            if (k.Value.PlayerName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
            {
                return k.Value;
            }
        }

        return null;
    }

    public int GenerateClientId()
    {
        int i = 0;
        while (Clients.ContainsKey(i))
        {
            i++;
        }

        return i;
    }

    public void AssignGroup(Group group)
    {
        ServerConsoleClient.AssignGroup(group);
    }

    public bool ServerClientNeedsSaving { get; set; }
    public ServerClient ServerClient { get; set; }
}