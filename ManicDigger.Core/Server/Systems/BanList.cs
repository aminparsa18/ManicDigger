using ManicDigger;
using System.Text.Json;
using System.Text.Json.Serialization;

public class ServerSystemBanList : ServerSystem
{
    private const string BanlistFilename = "ServerBanlist.json";

    protected override void Initialize(Server server) => LoadBanlist(server);

    protected override void OnUpdate(Server server, float dt)
    {
        if (server.BanList.ClearTimeBans() > 0)
        {
            SaveBanlist(server);
        }

        foreach (KeyValuePair<int, ClientOnServer> k in server.Clients)
        {
            CheckAndKickBannedClient(server, k.Key, k.Value);
        }
    }

    private static void CheckAndKickBannedClient(Server server, int clientId, ClientOnServer client)
    {
        string ip = client.Socket.RemoteEndPoint().AddressToString();

        if (server.BanList.IsIPBanned(ip))
        {
            string reason = server.BanList.GetIPEntry(ip).Reason ?? "";
            server.SendPacket(clientId, ServerPackets.DisconnectPlayer(
                string.Format(server.Language.ServerIPBanned(), reason)));
            LogAndKick(server, clientId, $"Banned IP {ip} tries to connect.");
            return;
        }

        string username = client.PlayerName;
        if (server.BanList.IsUserBanned(username))
        {
            string reason = server.BanList.GetUserEntry(username).Reason ?? "";
            server.SendPacket(clientId, ServerPackets.DisconnectPlayer(
                string.Format(server.Language.ServerUsernameBanned(), reason)));
            LogAndKick(server, clientId, $"{ip} fails to join (banned username: {username}).");
        }
    }

    private static void LogAndKick(Server server, int clientId, string message)
    {
        Console.WriteLine(message);
        server.ServerEventLog(message);
        server.KillPlayer(clientId);
    }

    // -------------------------------------------------------------------------
    // Command dispatch
    // -------------------------------------------------------------------------

    public override bool OnCommand(Server server, int sourceClientId, string command, string argument)
    {
        string[] args = argument.Split(' ');
        LanguageService lang = server.Language;
        string colorError = server.colorError;

        void SendInvalidArgs()
            => server.SendMessage(sourceClientId, colorError + lang.Get("Server_CommandInvalidArgs"));

        bool TryParseId(out int id) => int.TryParse(args[0], out id);
        bool TryParseDuration(int index, out int duration) => int.TryParse(args[index], out duration);

        string TrailingReason(int fromIndex)
            => args.Length > fromIndex ? string.Join(" ", args, fromIndex, args.Length - fromIndex) : "";

        switch (command)
        {
            case "banip_id":
                if (!TryParseId(out int banipId))
                {
                    SendInvalidArgs();
                    return true;
                }

                BanIP(server, sourceClientId, banipId, TrailingReason(1));
                return true;

            case "banip":
                BanIP(server, sourceClientId, args[0], TrailingReason(1));
                return true;

            case "ban_id":
                if (!TryParseId(out int banId))
                {
                    SendInvalidArgs();
                    return true;
                }

                Ban(server, sourceClientId, banId, TrailingReason(1));
                return true;

            case "ban":
                Ban(server, sourceClientId, args[0], TrailingReason(1));
                return true;

            case "timebanip_id": // /timebanip_id <id> <duration> [reason]
                if (args.Length < 2 || !TryParseId(out int tbanipById) || !TryParseDuration(1, out int tbanipByIdDur))
                {
                    SendInvalidArgs();
                    return true;
                }

                if (tbanipByIdDur <= 0)
                {
                    server.SendMessage(sourceClientId, colorError + lang.Get("Server_CommandTimeBanInvalidValue"));
                    return true;
                }

                TimeBanIP(server, sourceClientId, tbanipById, TrailingReason(2), tbanipByIdDur);
                return true;

            case "timebanip": // /timebanip <name> <duration> [reason]
                if (args.Length < 2 || !TryParseDuration(1, out int tbanipDur))
                {
                    SendInvalidArgs();
                    return true;
                }

                if (tbanipDur <= 0)
                {
                    server.SendMessage(sourceClientId, colorError + lang.Get("Server_CommandTimeBanInvalidValue"));
                    return true;
                }

                TimeBanIP(server, sourceClientId, args[0], TrailingReason(2), tbanipDur);
                return true;

            case "timeban_id": // /timeban_id <id> <duration> [reason]
                if (args.Length < 2 || !TryParseId(out int tbanById) || !TryParseDuration(1, out int tbanByIdDur))
                {
                    SendInvalidArgs();
                    return true;
                }

                if (tbanByIdDur <= 0)
                {
                    server.SendMessage(sourceClientId, colorError + lang.Get("Server_CommandTimeBanInvalidValue"));
                    return true;
                }

                TimeBan(server, sourceClientId, tbanById, TrailingReason(2), tbanByIdDur);
                return true;

            case "timeban": // /timeban <name> <duration> [reason]
                if (args.Length < 2 || !TryParseDuration(1, out int tbanDur))
                {
                    SendInvalidArgs();
                    return true;
                }

                if (tbanDur <= 0)
                {
                    server.SendMessage(sourceClientId, colorError + lang.Get("Server_CommandTimeBanInvalidValue"));
                    return true;
                }

                TimeBan(server, sourceClientId, args[0], TrailingReason(2), tbanDur);
                return true;

            case "ban_offline":
                BanOffline(server, sourceClientId, args[0], TrailingReason(1));
                return true;

            case "unban":
                if (args.Length == 2)
                {
                    Unban(server, sourceClientId, args[0], args[1]);
                    return true;
                }

                SendInvalidArgs();
                return true;

            default:
                return false;
        }
    }

    // -------------------------------------------------------------------------
    // Ban helpers — name-based overloads resolve to ID-based
    // -------------------------------------------------------------------------

    public static bool Ban(Server server, int sourceClientId, string target, string reason = "")
    {
        ClientOnServer targetClient = server.GetClient(target);
        if (targetClient != null)
        {
            return Ban(server, sourceClientId, targetClient.Id, reason);
        }

        server.SendMessage(sourceClientId, string.Format(
            server.Language.Get("Server_CommandPlayerNotFound"), server.colorError, target));
        return false;
    }

    public static bool Ban(Server server, int sourceClientId, int targetClientId, string reason = "")
    {
        if (!CheckPrivilege(server, sourceClientId, ServerClientMisc.Privilege.ban))
        {
            return false;
        }

        ClientOnServer target = server.GetClient(targetClientId);
        if (target == null)
        {
            return SendNonexistentId(server, sourceClientId, targetClientId);
        }

        if (!CheckTargetRank(server, sourceClientId, target))
        {
            return false;
        }

        reason = FormatReason(server, reason);
        string targetName = target.PlayerName;
        string sourceName = server.GetClient(sourceClientId).PlayerName;

        server.BanList.BanPlayer(targetName, sourceName, reason);
        SaveBanlist(server);
        BroadcastAndKick(server, sourceClientId, targetClientId,
            "Server_CommandBanMessage", "Server_CommandBanNotification",
            targetName, sourceName, target, reason);
        server.ServerEventLog($"{sourceName} bans {targetName}.{reason}");
        return true;
    }

    public static bool BanIP(Server server, int sourceClientId, string target, string reason = "")
    {
        ClientOnServer targetClient = server.GetClient(target);
        if (targetClient != null)
        {
            return BanIP(server, sourceClientId, targetClient.Id, reason);
        }

        server.SendMessage(sourceClientId, string.Format(
            server.Language.Get("Server_CommandPlayerNotFound"), server.colorError, target));
        return false;
    }

    public static bool BanIP(Server server, int sourceClientId, int targetClientId, string reason = "")
    {
        if (!CheckPrivilege(server, sourceClientId, ServerClientMisc.Privilege.banip))
        {
            return false;
        }

        ClientOnServer target = server.GetClient(targetClientId);
        if (target == null)
        {
            return SendNonexistentId(server, sourceClientId, targetClientId);
        }

        if (!CheckTargetRank(server, sourceClientId, target))
        {
            return false;
        }

        reason = FormatReason(server, reason);
        string targetName = target.PlayerName;
        string sourceName = server.GetClient(sourceClientId).PlayerName;

        server.BanList.BanIP(target.Socket.RemoteEndPoint().AddressToString(), sourceName, reason);
        SaveBanlist(server);
        BroadcastAndKick(server, sourceClientId, targetClientId,
            "Server_CommandIPBanMessage", "Server_CommandIPBanNotification",
            targetName, sourceName, target, reason);
        server.ServerEventLog($"{sourceName} IP bans {targetName}.{reason}");
        return true;
    }

    public static bool TimeBan(Server server, int sourceClientId, string target, string reason, int duration)
    {
        ClientOnServer targetClient = server.GetClient(target);
        if (targetClient != null)
        {
            return TimeBan(server, sourceClientId, targetClient.Id, reason, duration);
        }

        server.SendMessage(sourceClientId, string.Format(
            server.Language.Get("Server_CommandPlayerNotFound"), server.colorError, target));
        return false;
    }

    public static bool TimeBan(Server server, int sourceClientId, int targetClientId, string reason, int duration)
    {
        if (!CheckPrivilege(server, sourceClientId, ServerClientMisc.Privilege.ban))
        {
            return false;
        }

        ClientOnServer target = server.GetClient(targetClientId);
        if (target == null)
        {
            return SendNonexistentId(server, sourceClientId, targetClientId);
        }

        if (!CheckTargetRank(server, sourceClientId, target))
        {
            return false;
        }

        reason = FormatReason(server, reason);
        string targetName = target.PlayerName;
        string sourceName = server.GetClient(sourceClientId).PlayerName;

        server.BanList.TimeBanPlayer(targetName, sourceName, reason, duration);
        SaveBanlist(server);
        server.SendMessageToAll(string.Format(server.Language.Get("Server_CommandTimeBanMessage"),
            server.colorImportant, target.ColoredPlayername(server.colorImportant),
            server.GetClient(sourceClientId).ColoredPlayername(server.colorImportant), duration, reason));
        server.SendPacket(targetClientId, ServerPackets.DisconnectPlayer(
            string.Format(server.Language.Get("Server_CommandTimeBanNotification"), duration, reason)));
        server.ServerEventLog($"{sourceName} bans {targetName} for {duration} minutes.{reason}");
        server.KillPlayer(targetClientId);
        return true;
    }

    public static bool TimeBanIP(Server server, int sourceClientId, string target, string reason, int duration)
    {
        ClientOnServer targetClient = server.GetClient(target);
        if (targetClient != null)
        {
            return TimeBanIP(server, sourceClientId, targetClient.Id, reason, duration);
        }

        server.SendMessage(sourceClientId, string.Format(
            server.Language.Get("Server_CommandPlayerNotFound"), server.colorError, target));
        return false;
    }

    public static bool TimeBanIP(Server server, int sourceClientId, int targetClientId, string reason, int duration)
    {
        if (!CheckPrivilege(server, sourceClientId, ServerClientMisc.Privilege.banip))
        {
            return false;
        }

        ClientOnServer target = server.GetClient(targetClientId);
        if (target == null)
        {
            return SendNonexistentId(server, sourceClientId, targetClientId);
        }

        if (!CheckTargetRank(server, sourceClientId, target))
        {
            return false;
        }

        reason = FormatReason(server, reason);
        string targetName = target.PlayerName;
        string sourceName = server.GetClient(sourceClientId).PlayerName;

        server.BanList.TimeBanIP(target.Socket.RemoteEndPoint().AddressToString(), sourceName, reason, duration);
        SaveBanlist(server);
        server.SendMessageToAll(string.Format(server.Language.Get("Server_CommandTimeIPBanMessage"),
            server.colorImportant, target.ColoredPlayername(server.colorImportant),
            server.GetClient(sourceClientId).ColoredPlayername(server.colorImportant), duration, reason));
        server.SendPacket(targetClientId, ServerPackets.DisconnectPlayer(
            string.Format(server.Language.Get("Server_CommandTimeIPBanNotification"), duration, reason)));
        server.ServerEventLog($"{sourceName} IP bans {targetName} for {duration} minutes.{reason}");
        server.KillPlayer(targetClientId);
        return true;
    }

    public static bool BanOffline(Server server, int sourceClientId, string target, string reason = "")
    {
        if (!CheckPrivilege(server, sourceClientId, ServerClientMisc.Privilege.ban_offline))
        {
            return false;
        }

        if (server.GetClient(target) != null)
        {
            server.SendMessage(sourceClientId, string.Format(
                server.Language.Get("Server_CommandBanOfflineTargetOnline"), server.colorError, target));
            return false;
        }

        reason = FormatReason(server, reason);

        // If the player has a config entry, verify rank and remove it
        Client targetConfigEntry = server.ServerClient.Clients.Find(
            c => c.Name.Equals(target, StringComparison.InvariantCultureIgnoreCase));

        if (targetConfigEntry != null)
        {
            Group targetGroup = server.ServerClient.Groups.Find(g => g.Name.Equals(targetConfigEntry.Group));
            if (targetGroup == null)
            {
                server.SendMessage(sourceClientId, string.Format(
                    server.Language.Get("Server_CommandInvalidGroup"), server.colorError));
                return false;
            }

            ClientOnServer sourceClient = server.GetClient(sourceClientId);
            if (targetGroup.IsSuperior(sourceClient.ClientGroup) || targetGroup.EqualLevel(sourceClient.ClientGroup))
            {
                server.SendMessage(sourceClientId, string.Format(
                    server.Language.Get("Server_CommandTargetUserSuperior"), server.colorError));
                return false;
            }

            server.ServerClient.Clients.Remove(targetConfigEntry);
            server.ServerClientNeedsSaving = true;
        }

        ClientOnServer source = server.GetClient(sourceClientId);
        server.BanList.BanPlayer(target, source.PlayerName, reason);
        SaveBanlist(server);
        server.SendMessageToAll(string.Format(server.Language.Get("Server_CommandBanOfflineMessage"),
            server.colorImportant, target, source.ColoredPlayername(server.colorImportant), reason));
        server.ServerEventLog($"{source.PlayerName} bans {target}.{reason}");
        return true;
    }

    public static bool Unban(Server server, int sourceClientId, string type, string target)
    {
        if (!CheckPrivilege(server, sourceClientId, ServerClientMisc.Privilege.unban))
        {
            return false;
        }

        if (type == "-p")
        {
            bool exists = server.BanList.UnbanPlayer(target);
            SaveBanlist(server);
            if (!exists)
            {
                server.SendMessage(sourceClientId, string.Format(server.Language.Get("Server_CommandPlayerNotFound"), server.colorError, target));
            }
            else
            {
                server.SendMessage(sourceClientId, string.Format(server.Language.Get("Server_CommandUnbanSuccess"), server.colorSuccess, target));
                server.ServerEventLog($"{server.GetClient(sourceClientId).PlayerName} unbans player {target}.");
            }

            return true;
        }

        if (type == "-ip")
        {
            bool exists = server.BanList.UnbanIP(target);
            SaveBanlist(server);
            if (!exists)
            {
                server.SendMessage(sourceClientId, string.Format(server.Language.Get("Server_CommandUnbanIPNotFound"), server.colorError, target));
            }
            else
            {
                server.SendMessage(sourceClientId, string.Format(server.Language.Get("Server_CommandUnbanIPSuccess"), server.colorSuccess, target));
                server.ServerEventLog($"{server.GetClient(sourceClientId).PlayerName} unbans IP {target}.");
            }

            return true;
        }

        server.SendMessage(sourceClientId, $"{server.colorError}Invalid type: {type}");
        return false;
    }

    // -------------------------------------------------------------------------
    // Banlist persistence
    // -------------------------------------------------------------------------
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void LoadBanlist(Server server)
    {
        string path = Path.Combine(GameStorePath.gamepathconfig, BanlistFilename);

        if (!File.Exists(path))
        {
            Console.WriteLine(server.Language.ServerBanlistNotFound());
            SaveBanlist(server);
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            server.BanList = JsonSerializer.Deserialize<ServerBanlist>(json, JsonOptions)
                             ?? new ServerBanlist();
        }
        catch
        {
            try
            {
                File.Copy(path, path + ".old");
                Console.WriteLine(server.Language.ServerBanlistCorrupt());
            }
            catch
            {
                Console.WriteLine(server.Language.ServerBanlistCorruptNoBackup());
            }

            server.BanList = null;
            SaveBanlist(server);
            return;
        }

        SaveBanlist(server);
        Console.WriteLine(server.Language.ServerBanlistLoaded());
    }

    public static void SaveBanlist(Server server)
    {
        Directory.CreateDirectory(GameStorePath.gamepathconfig);
        server.BanList ??= new ServerBanlist();

        File.WriteAllText(
            Path.Combine(GameStorePath.gamepathconfig, BanlistFilename),
            JsonSerializer.Serialize(server.BanList, JsonOptions));
    }

    // -------------------------------------------------------------------------
    // Shared guard helpers
    // -------------------------------------------------------------------------

    private static bool CheckPrivilege(Server server, int sourceClientId, string privilege)
    {
        if (server.PlayerHasPrivilege(sourceClientId, privilege))
        {
            return true;
        }

        server.SendMessage(sourceClientId, string.Format(
            server.Language.Get("Server_CommandInsufficientPrivileges"), server.colorError));
        return false;
    }

    private static bool CheckTargetRank(Server server, int sourceClientId, ClientOnServer target)
    {
        ClientOnServer source = server.GetClient(sourceClientId);
        if (target.ClientGroup.IsSuperior(source.ClientGroup) || target.ClientGroup.EqualLevel(source.ClientGroup))
        {
            server.SendMessage(sourceClientId, string.Format(
                server.Language.Get("Server_CommandTargetUserSuperior"), server.colorError));
            return false;
        }

        return true;
    }

    private static bool SendNonexistentId(Server server, int sourceClientId, int targetClientId)
    {
        server.SendMessage(sourceClientId, string.Format(
            server.Language.Get("Server_CommandNonexistantID"), server.colorError, targetClientId));
        return false;
    }

    private static string FormatReason(Server server, string reason)
        => string.IsNullOrEmpty(reason) ? "" : server.Language.Get("Server_CommandKickBanReason") + reason + ".";

    private static void BroadcastAndKick(Server server, int sourceClientId, int targetClientId,
        string broadcastKey, string notificationKey,
        string targetName, string sourceName, ClientOnServer target, string reason)
    {
        server.SendMessageToAll(string.Format(server.Language.Get(broadcastKey),
            server.colorImportant,
            target.ColoredPlayername(server.colorImportant),
            server.GetClient(sourceClientId).ColoredPlayername(server.colorImportant),
            reason));
        server.SendPacket(targetClientId, ServerPackets.DisconnectPlayer(
            string.Format(server.Language.Get(notificationKey), reason)));
        server.KillPlayer(targetClientId);
    }
}