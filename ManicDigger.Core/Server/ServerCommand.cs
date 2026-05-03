using ManicDigger;

public partial class Server
{
    public void ServerEventLog(string p)
    {
        if (!_config.ServerEventLogging)
        {
            return;
        }

        if (!Directory.Exists(_serverPathLogs))
        {
            Directory.CreateDirectory(_serverPathLogs);
        }

        string filename = Path.Combine(_serverPathLogs, "DiagLog.Write.txt");
        File.AppendAllText(filename, string.Format("{0} {1}\n", DateTime.Now, p));
    }

    public void CommandInterpreter(int sourceClientId, string command, string argument)
    {
        string[] ss;
        int id;

        switch (command)
        {
            case "msg":
            case "pm":
                ss = argument.Split([' ']);
                if (ss.Length >= 2)
                {
                    this.PrivateMessage(sourceClientId, ss[0], string.Join(" ", ss, 1, ss.Length - 1));
                    return;
                }

                _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                return;
            case "re":
                if (!string.IsNullOrEmpty(argument))
                {
                    this.AnswerMessage(sourceClientId, argument);
                    return;
                }

                _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                return;
            case "op":
            case "chgrp":
            case "cg":
                ss = argument.Split([' ']);
                if (ss.Length == 2)
                {
                    this.ChangeGroup(sourceClientId, ss[0], ss[1]);
                    return;
                }

                _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                return;
            case "op_offline":
            case "chgrp_offline":
            case "cg_offline":
                ss = argument.Split([' ']);
                if (ss.Length == 2)
                {
                    this.ChangeGroupOffline(sourceClientId, ss[0], ss[1]);
                    return;
                }

                _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                return;
            case "remove_client":
                ss = argument.Split([' ']);
                if (ss.Length == 1)
                {
                    this.RemoveClientFromConfig(sourceClientId, ss[0]);
                    return;
                }

                _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                return;
            case "login":
                // enables to change temporary group with a group's password (only if group allows it)
                ss = argument.Split([' ']);
                if (ss.Length == 2)
                {
                    this.Login(sourceClientId, ss[0], ss[1]);
                    return;
                }

                _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                return;
            case "welcome":
                this.WelcomeMessage(sourceClientId, argument);
                return;
            case "announcement":
                this.Announcement(sourceClientId, argument);
                return;
            case "logging":
                ss = argument.Split([' ']);
                if (ss.Length == 1)
                {
                    this.SetLogging(sourceClientId, ss[0], "");
                    return;
                }

                if (ss.Length == 2)
                {
                    this.SetLogging(sourceClientId, ss[0], ss[1]);
                    return;
                }

                _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                return;
            case "kick_id":
                ss = argument.Split([' ']);
                if (!int.TryParse(ss[0], out id))
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                if (ss.Length >= 2)
                {
                    this.Kick(sourceClientId, id, string.Join(" ", ss, 1, ss.Length - 1));
                    return;
                }

                this.Kick(sourceClientId, id);
                return;
            case "kick":
                ss = argument.Split([' ']);
                if (ss.Length >= 2)
                {
                    this.Kick(sourceClientId, ss[0], string.Join(" ", ss, 1, ss.Length - 1));
                    return;
                }

                this.Kick(sourceClientId, argument);
                return;
            case "list":
                this.List(sourceClientId, argument);
                return;
            case "giveall":
                this.GiveAll(sourceClientId, argument);
                return;
            case "give":
                ss = argument.Split([' ']);
                if (ss.Length == 3)
                {
                    if (!int.TryParse(ss[2], out int amount))
                    {
                        _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                        return;
                    }
                    else
                    {
                        this.Give(sourceClientId, ss[0], ss[1], amount);
                    }

                    return;
                }

                _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                return;
            case "monsters":
                if (!argument.Equals("off") && !argument.Equals("on"))
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                this.Monsters(sourceClientId, argument);
                return;
            case "area_add":
                int areaId;
                ss = argument.Split([' ']);

                if (ss.Length is < 4 or > 5)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                if (!int.TryParse(ss[0], out areaId))
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                string coords = ss[1];
                string[] permittedGroups = ss[2].ToString().Split([',']);
                string[] permittedUsers = ss[3].ToString().Split([',']);

                int? areaLevel;
                try
                {
                    areaLevel = Convert.ToInt32(ss[4]);
                }
                catch (IndexOutOfRangeException)
                {
                    areaLevel = null;
                }
                catch (FormatException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }
                catch (OverflowException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                this.AreaAdd(sourceClientId, areaId, coords, permittedGroups, permittedUsers, areaLevel);
                return;
            case "area_delete":
                if (!int.TryParse(argument, out areaId))
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                this.AreaDelete(sourceClientId, areaId);
                return;
            case "help":
                this.Help(sourceClientId);
                return;
            case "run":
            case "":
                // JavaScript
                // assume script expression or command coming
                var script = argument;
                RunInClientSandbox(script, sourceClientId);
                return;
            case "crash":
                KillPlayer(sourceClientId);
                return;
            case "set_spawn":
                //           0    1      2 3 4
                // argument: type target x y z
                ss = argument.Split([' ']);

                if (ss.Length is < 3 or > 5)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                // Add an empty target argument, when user sets default spawn.
                if (ss[0].Equals("-d") || ss[0].Equals("-default"))
                {
                    string[] ssTemp = new string[ss.Length + 1];
                    ssTemp[0] = ss[0];
                    ssTemp[1] = "";
                    Array.Copy(ss, 1, ssTemp, 2, ss.Length - 1);
                    ss = ssTemp;
                }

                int x;
                int y;
                int? z;
                try
                {
                    x = Convert.ToInt32(ss[2]);
                    y = Convert.ToInt32(ss[3]);
                }
                catch (IndexOutOfRangeException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidSpawnPosition"));
                    return;
                }
                catch (FormatException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidSpawnPosition"));
                    return;
                }
                catch (OverflowException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidSpawnPosition"));
                    return;
                }

                try
                {
                    z = Convert.ToInt32(ss[4]);
                }
                catch (IndexOutOfRangeException)
                {
                    z = null;
                }
                catch (FormatException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidSpawnPosition"));
                    return;
                }
                catch (OverflowException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidSpawnPosition"));
                    return;
                }

                this.SetSpawnPosition(sourceClientId, ss[0], ss[1], x, y, z);
                return;
            case "set_home":
                // When no coordinates are given, set spawn to players current position.
                if (string.IsNullOrEmpty(argument))
                {
                    this.SetSpawnPosition(sourceClientId,
                                      _serverClientService.GetClient(sourceClientId).PositionMul32GlX / 32,
                                     _serverClientService.GetClient(sourceClientId).PositionMul32GlZ / 32,
                                      _serverClientService.GetClient(sourceClientId).PositionMul32GlY / 32);
                    return;
                }
                //            0 1 2
                // agrument:  x y z
                ss = argument.Split([' ']);

                if (ss.Length is < 2 or > 3)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                try
                {
                    x = Convert.ToInt32(ss[0]);
                    y = Convert.ToInt32(ss[1]);
                }
                catch (IndexOutOfRangeException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidSpawnPosition"));
                    return;
                }
                catch (FormatException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidSpawnPosition"));
                    return;
                }
                catch (OverflowException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidSpawnPosition"));
                    return;
                }

                try
                {
                    z = Convert.ToInt32(ss[2]);
                }
                catch (IndexOutOfRangeException)
                {
                    z = null;
                }
                catch (FormatException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidSpawnPosition"));
                    return;
                }
                catch (OverflowException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidSpawnPosition"));
                    return;
                }

                this.SetSpawnPosition(sourceClientId, x, y, z);
                return;
            case "privilege_add":
                ss = argument.Split([' ']);
                if (ss.Length != 2)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                this.PrivilegeAdd(sourceClientId, ss[0], ss[1]);
                return;
            case "privilege_remove":
                ss = argument.Split([' ']);
                if (ss.Length != 2)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                this.PrivilegeRemove(sourceClientId, ss[0], ss[1]);
                return;
            case "restart":
                this.RestartServer(sourceClientId);
                break;
            case "shutdown":
                this.ShutdownServer(sourceClientId);
                break;
            //case "crashserver": for (; ; ) ;
            case "stats":
                double seconds = (DateTime.UtcNow - statsupdate).TotalSeconds;
                _serverPacketService.SendMessage(sourceClientId, "Packets/s:" + decimal.Round((decimal)(_serverPacketService.StatTotalPackets / seconds), 2, MidpointRounding.AwayFromZero));
                _serverPacketService.SendMessage(sourceClientId, "Total KBytes/s:" + decimal.Round((decimal)(_serverPacketService.StatTotalPacketsLength / seconds / 1024), 2, MidpointRounding.AwayFromZero));
                break;
            case "tp":
                ss = argument.Split([' ']);
                if (ss.Length != 1)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                foreach (KeyValuePair<int, ClientOnServer> k in _serverClientService.Clients)
                {
                    if (k.Value.PlayerName.Equals(ss[0], StringComparison.InvariantCultureIgnoreCase))
                    {
                        this.TeleportToPlayer(sourceClientId, k.Key);
                        return;
                    }
                }

                foreach (KeyValuePair<int, ClientOnServer> k in _serverClientService.Clients)
                {
                    if (k.Value.PlayerName.StartsWith(ss[0], StringComparison.InvariantCultureIgnoreCase))
                    {
                        this.TeleportToPlayer(sourceClientId, k.Key);
                        return;
                    }
                }

                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandNonexistantPlayer"), colorError, ss[0]));
                break;
            case "tp_pos":
                ss = argument.Split([' ']);
                if (ss.Length is < 2 or > 3)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                try
                {
                    x = Convert.ToInt32(ss[0]);
                    y = Convert.ToInt32(ss[1]);
                }
                catch (IndexOutOfRangeException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidPosition"));
                    return;
                }
                catch (FormatException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidPosition"));
                    return;
                }
                catch (OverflowException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidPosition"));
                    return;
                }

                try
                {
                    z = Convert.ToInt32(ss[2]);
                }
                catch (IndexOutOfRangeException)
                {
                    z = null;
                }
                catch (FormatException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidPosition"));
                    return;
                }
                catch (OverflowException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidPosition"));
                    return;
                }

                this.TeleportToPosition(sourceClientId, x, y, z);
                break;
            case "teleport_player":
                ss = argument.Split([' ']);

                if (ss.Length is < 3 or > 4)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }

                try
                {
                    x = Convert.ToInt32(ss[1]);
                    y = Convert.ToInt32(ss[2]);
                }
                catch (IndexOutOfRangeException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidPosition"));
                    return;
                }
                catch (FormatException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidPosition"));
                    return;
                }
                catch (OverflowException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidPosition"));
                    return;
                }

                try
                {
                    z = Convert.ToInt32(ss[3]);
                }
                catch (IndexOutOfRangeException)
                {
                    z = null;
                }
                catch (FormatException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidPosition"));
                    return;
                }
                catch (OverflowException)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidPosition"));
                    return;
                }

                this.TeleportPlayer(sourceClientId, ss[0], x, y, z);
                break;
            case "backup_database":
                if (!_serverClientService.GetClient(sourceClientId).Privileges.Contains(ServerClientMisc.Privilege.backup_database))
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
                    break;
                }

                if (!_saveGameService.BackupDatabase(argument))
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandBackupFailed"), colorError));
                }
                else
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandBackupCreated"), colorSuccess));
                    ServerEventLog(string.Format("{0} backups database: {1}.", _serverClientService.GetClient(sourceClientId).PlayerName, argument));
                }

                break;
            /*
        case "load":
            if (!GetClient(sourceClientId).privileges.Contains(ServerClientMisc.Privilege.load))
            {
                SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
                break;
            }
            if (!GameStorePath.IsValidName(argument))
            {
                SendMessage(sourceClientId, string.Format("Invalid load filename: {0}", argument));
                break;
            }
            if (!LoadDatabase(argument))
            {
                SendMessage(sourceClientId, string.Format("{0}World could not be loaded. Check filename.", colorError));
            }
            else
            {
                SendMessage(sourceClientId, string.Format("{0}World loaded.", colorSuccess));
                ServerEventLog(String.Format("{0} loads world: {1}.", GetClient(sourceClientId).playername, argument));
            }
            break;
            */
            case "reset_inventory":
                this.ResetInventory(sourceClientId, argument);
                return;
            case "fill_limit":
                //           0    1      2
                // agrument: type target maxFill
                ss = argument.Split([' ']);
                if (ss.Length is < 2 or > 3)
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }
                // Add an empty target argument, when user sets default max-fill.
                if (ss[0].Equals("-d") || ss[0].Equals("-default"))
                {
                    string[] ssTemp = new string[ss.Length + 1];
                    ssTemp[0] = ss[0];
                    ssTemp[1] = "";
                    Array.Copy(ss, 1, ssTemp, 2, ss.Length - 1);
                    ss = ssTemp;
                }

                if (!int.TryParse(ss[2], out int maxFill))
                {
                    _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandInvalidArgs"));
                    return;
                }
                else
                {
                    this.SetFillAreaLimit(sourceClientId, ss[0], ss[1], maxFill);
                }

                return;
            case "time":
                {
                    TimeCommand(sourceClientId, argument);
                }

                break;
            default:
                for (int i = 0; i < Systems.Count; i++)
                {
                    if (Systems[i] == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (Systems[i].OnCommand(this, sourceClientId, command, argument))
                        {
                            return;
                        }
                    }
                    catch
                    {
                        _serverPacketService.SendMessage(sourceClientId, _languageService.Get("Server_CommandException"));
                    }
                }

                if (_modEvents.RaiseCommand(sourceClientId, command, argument))
                    return;

                _serverPacketService.SendMessage(sourceClientId, colorError + _languageService.Get("Server_CommandUnknown") + command);
                return;
        }
    }

    public void Help(int sourceClientId)
    {
        _serverPacketService.SendMessage(sourceClientId, colorHelp + "Available privileges:");
        foreach (string privilege in _serverClientService.GetClient(sourceClientId).Privileges)
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format("{0}{1}: {2}", colorHelp, privilege.ToString(), this.CommandHelp(privilege.ToString())));
        }
    }

    private string CommandHelp(string command)
    {
        switch (command)
        {
            case "msg":
            case "pm":
                return "/msg [username] [message]";
            case "kick":
                return "/kick [username] {reason}";
            case "kick_id":
                return "kick_id [player id] {reason}";
            case "ban":
                return "/ban [username] {reason}";
            case "ban_id":
                return "/ban_id [player id] {reason}";
            case "banip":
                return "/banip [username] {reason}";
            case "banip_id":
                return "/banip_id [player id] {reason}";
            case "ban_offline":
                return "/ban_offline [username] {reason}";
            case "unban":
                return "/unban [-p playername | -ip ipaddress]";
            case "run":
                return "/run [JavaScript (max. length 4096 char.)]";
            case "op":
                return "/op [username] [group]";
            case "chgrp":
                return "/chgrp [username] [group]";
            case "op_offline":
                return "/op_offline [username] [group]";
            case "chgrp_offline":
                return "/chgrp_offline [username] [group]";
            case "remove_client":
                return "/remove_client [username]";
            case "login":
                return "/login [group] [password]";
            case "welcome":
                return "/welcome [login motd message]";
            case "logging":
                return "/logging [-s | -b | -se | -c] {on | off}";
            case "list_clients":
                return "/list [-clients]";
            case "list_saved_clients":
                return "/list [-saved_clients]";
            case "list_groups":
                return "/list [-groups]";
            case "list_banned_users":
                return "/list [-bannedusers | -bannedips]";
            case "list_areas":
                return "/list [-areas]";
            case "give":
                return "/give [username] blockname amount";
            case "giveall":
                return "/giveall [username]";
            case "monsters":
                return "/monsters [on|off]";
            case "area_add":
                return "/area_add [ID] [x1,x2,y1,y2,z1,z2] [group1,group2,..] [user1,user2,..] {level}";
            case "area_delete":
                return "/area_delete [ID]";
            case "announcement":
                return "/announcement [message]";
            case "set_spawn":
                return "/set_spawn [-default|-group|-player] [target] [x] [y] {z}";
            case "set_home":
                return "/set_home {[x] [y] {z}}";
            case "privilege_add":
                return "/privilege_add [username] [privilege]";
            case "privilege_remove":
                return "/privilege_remove [username] [privilege]";
            case "restart":
                return "/restart";
            case "teleport_player":
                return "/teleport_player [target] [x] [y] {z}";
            case "time":
                return "/time {[set|add|speed] [value]}";
            case "tp":
                return "/tp [username]";
            case "tp_pos":
                return "/tp_pos [x] [y] {z}";
            case "backup_database":
                return "/backup_database [filename]";
            case "reset_inventory":
                return "/reset_inventory [target]";
            case "fill_limit":
                return "/fill_limit [-default|-group|-player] [limit]";
            default:
                if (commandhelps.ContainsKey(command))
                {
                    return commandhelps[command];
                }

                return "No description available.";
        }
    }

    public Dictionary<string, string> commandhelps = new();
    public Dictionary<string, string> lastSender = new();

    public bool PrivateMessage(int sourceClientId, string recipient, string message)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.pm))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        ClientOnServer targetClient = _serverClientService.GetClient(recipient);
        ClientOnServer sourceClient = _serverClientService.GetClient(sourceClientId);
        if (targetClient != null)
        {
            _serverPacketService.SendMessage(targetClient.Id, string.Format("PM {0}: {1}", sourceClient.ColoredPlayername(colorNormal), message));
            _serverPacketService.SendMessage(sourceClientId, string.Format("PM -> {0}: {1}", targetClient.ColoredPlayername(colorNormal), message));
            lastSender[targetClient.PlayerName] = sourceClient.PlayerName;
            // TODO: move message sound to client
            if (targetClient.Id != GameConstants.ServerConsoleId)
            {
                SendSound(targetClient.Id, "message.wav", 0, 0, 0);
            }

            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandPlayerNotFound"), colorError, recipient));
        return false;
    }

    public bool AnswerMessage(int sourceClientId, string message)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.pm))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        ClientOnServer sourceClient = _serverClientService.GetClient(sourceClientId);
        if (!lastSender.ContainsKey(sourceClient.PlayerName))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandPMNoAnswer"), colorError));
            return false;
        }

        ClientOnServer targetClient = _serverClientService.GetClient(lastSender[sourceClient.PlayerName]);
        if (targetClient != null)
        {
            _serverPacketService.SendMessage(targetClient.Id, string.Format("PM {0}: {1}", sourceClient.ColoredPlayername(colorNormal), message));
            _serverPacketService.SendMessage(sourceClientId, string.Format("PM -> {0}: {1}", targetClient.ColoredPlayername(colorNormal), message));
            lastSender[targetClient.PlayerName] = sourceClient.PlayerName;
            // TODO: move message sound to client
            if (targetClient.Id != GameConstants.ServerConsoleId)
            {
                SendSound(targetClient.Id, "message.wav", 0, 0, 0);
            }

            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandPlayerNotFound"), colorError, lastSender[sourceClient.PlayerName]));
        return false;
    }

    public bool ChangeGroup(int sourceClientId, string target, string newGroupName)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.chgrp))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        // Get related group from config file.
        Group? newGroup = _serverClientService.ServerClient.Groups.Find(
            delegate (Group grp)
            {
                return grp.Name.Equals(newGroupName, StringComparison.InvariantCultureIgnoreCase);
            }
        );
        if (newGroup == null)
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandGroupNotFound"), colorError, newGroupName));
            return false;
        }

        // Forbid to assign groups with levels higher then the source's client group level.
        if (newGroup.IsSuperior(_serverClientService.GetClient(sourceClientId).ClientGroup))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandTargetGroupSuperior"), colorError));
            return false;
        }

        // Get related client from config file
        Client? clientConfig = _serverClientService.ServerClient.Clients.Find(
            delegate (Client client)
            {
                return client.Name.Equals(target, StringComparison.InvariantCultureIgnoreCase);
            }
        );

        // Get related client.
        ClientOnServer targetClient = _serverClientService.GetClient(target);

        if (targetClient != null)
        {
            if (targetClient.ClientGroup.IsSuperior(_serverClientService.GetClient(sourceClientId).ClientGroup) || targetClient.ClientGroup.EqualLevel(_serverClientService.GetClient(sourceClientId).ClientGroup))
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandTargetUserSuperior"), colorError));
                return false;
            }
            // Add or change group membership in config file.

            // Client is not yet in config file. Create a new entry.
            if (clientConfig == null)
            {
                clientConfig = new Client
                {
                    Name = targetClient.PlayerName,
                    Group = newGroup.Name
                };
                _serverClientService.ServerClient.Clients.Add(clientConfig);
            }
            else
            {
                clientConfig.Group = newGroup.Name;
            }

            _serverClientService.ServerClientNeedsSaving = true;
            SendMessageToAll(string.Format(_languageService.Get("Server_CommandSetGroupTo"), colorSuccess, _serverClientService.GetClient(sourceClientId).ColoredPlayername(colorSuccess), targetClient.ColoredPlayername(colorSuccess), newGroup.GroupColorString() + newGroupName));
            ServerEventLog(string.Format("{0} sets group of {1} to {2}.", _serverClientService.GetClient(sourceClientId).PlayerName, targetClient.PlayerName, newGroupName));
            targetClient.AssignGroup(newGroup);
            SendFreemoveState(targetClient.Id, targetClient.Privileges.Contains(ServerClientMisc.Privilege.freemove));
            SetFillAreaLimit(targetClient.Id);
            return true;
        }

        // Target is at the moment not online.
        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandOpTargetOffline"), colorError, target));
        return false;
    }

    public bool ChangeGroupOffline(int sourceClientId, string target, string newGroupName)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.chgrp_offline))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        // Get related group from config file.
        Group? newGroup = _serverClientService.ServerClient.Groups.Find(
            delegate (Group grp)
            {
                return grp.Name.Equals(newGroupName, StringComparison.InvariantCultureIgnoreCase);
            }
        );
        if (newGroup == null)
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandGroupNotFound"), colorError, newGroupName));
            return false;
        }

        // Forbid to assign groups with levels higher then the source's client group level.
        if (newGroup.IsSuperior(_serverClientService.GetClient(sourceClientId).ClientGroup))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandTargetGroupSuperior"), colorError));
            return false;
        }

        // Get related client from config file.
        Client? clientConfig = _serverClientService.ServerClient.Clients.Find(
            delegate (Client client)
            {
                return client.Name.Equals(target, StringComparison.InvariantCultureIgnoreCase);
            }
        );

        // Get related client.
        ClientOnServer targetClient = _serverClientService.GetClient(target);

        if (targetClient != null)
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandOpTargetOnline"), colorError, target));
            return false;
        }

        // Target is at the moment not online. Create or change a entry in ServerClient.
        if (clientConfig == null)
        {
            clientConfig = new Client
            {
                Name = target,
                Group = newGroup.Name
            };
            _serverClientService.ServerClient.Clients.Add(clientConfig);
        }
        else
        {
            // Check if target's current group is superior.
            Group? oldGroup = _serverClientService.ServerClient.Groups.Find(
                delegate (Group grp)
                {
                    return grp.Name.Equals(clientConfig.Group);
                }
            );
            if (oldGroup == null)
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInvalidGroup"), colorError));
                return false;
            }

            if (oldGroup.IsSuperior(_serverClientService.GetClient(sourceClientId).ClientGroup) || oldGroup.EqualLevel(_serverClientService.GetClient(sourceClientId).ClientGroup))
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandTargetUserSuperior"), colorError));
                return false;
            }

            clientConfig.Group = newGroup.Name;
        }

        _serverClientService.ServerClientNeedsSaving = true;
        SendMessageToAll(string.Format(_languageService.Get("Server_CommandSetOfflineGroupTo"), colorSuccess, _serverClientService.GetClient(sourceClientId).ColoredPlayername(colorSuccess), target, newGroup.GroupColorString() + newGroupName));
        ServerEventLog(string.Format("{0} sets group of {1} to {2} (offline).", _serverClientService.GetClient(sourceClientId).PlayerName, target, newGroupName));
        return true;
    }

    public bool RemoveClientFromConfig(int sourceClientId, string target)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.remove_client))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        // Get related client from config file
        Client? targetClient = _serverClientService.ServerClient.Clients.Find(
            delegate (Client client)
            {
                return client.Name.Equals(target, StringComparison.InvariantCultureIgnoreCase);
            }
        );
        // Entry exists.
        if (targetClient != null)
        {
            // Get target's group.
            Group? targetGroup = _serverClientService.ServerClient.Groups.Find(
                delegate (Group grp)
                {
                    return grp.Name.Equals(targetClient.Group);
                }
            );
            if (targetGroup == null)
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInvalidGroup"), colorError));
                return false;
            }
            // Check if target's group is superior.
            if (targetGroup.IsSuperior(_serverClientService.GetClient(sourceClientId).ClientGroup) || targetGroup.EqualLevel(_serverClientService.GetClient(sourceClientId).ClientGroup))
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandTargetUserSuperior"), colorError));
                return false;
            }
            // Remove target's entry.
            _serverClientService.ServerClient.Clients.Remove(targetClient);
            _serverClientService.ServerClientNeedsSaving = true;
            // If client is online, change his group
            if (_serverClientService.GetClient(target) != null)
            {
                _serverClientService.GetClient(target).AssignGroup(this.DefaultGroupGuest);
                SendMessageToAll(string.Format(_languageService.Get("Server_CommandSetGroupTo"), colorSuccess, _serverClientService.GetClient(sourceClientId).ColoredPlayername(colorSuccess), _serverClientService.GetClient(target).ColoredPlayername(colorSuccess), this.DefaultGroupGuest.GroupColorString() + DefaultGroupGuest.Name));
            }

            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandRemoveSuccess"), colorSuccess, target));
            ServerEventLog(string.Format("{0} removes client {1} from config.", _serverClientService.GetClient(sourceClientId).PlayerName, target));
            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandRemoveNotFound"), colorError, target));
        return false;
    }

    public bool Login(int sourceClientId, string targetGroupString, string password)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.login))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        Group? targetGroup = _serverClientService.ServerClient.Groups.Find(
            delegate (Group grp)
            {
                return grp.Name.Equals(targetGroupString, StringComparison.InvariantCultureIgnoreCase);
            }
        );
        if (targetGroup == null)
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandGroupNotFound"), colorError, targetGroupString));
            return false;
        }

        if (string.IsNullOrEmpty(targetGroup.Password))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandLoginNoPW"), colorError, targetGroupString));
            return false;
        }

        if (targetGroup.Password.Equals(password))
        {
            _serverClientService.GetClient(sourceClientId).AssignGroup(targetGroup);
            SendFreemoveState(sourceClientId, _serverClientService.GetClient(sourceClientId).Privileges.Contains(ServerClientMisc.Privilege.freemove));
            SendMessageToAll(string.Format(_languageService.Get("Server_CommandLoginSuccess"), colorSuccess, _serverClientService.GetClient(sourceClientId).ColoredPlayername(colorSuccess), targetGroupString));
            _serverPacketService.SendMessage(sourceClientId, _languageService.Get("Server_CommandLoginInfo"));
            ServerEventLog(string.Format("{0} logs in group {1}.", _serverClientService.GetClient(sourceClientId).PlayerName, targetGroupString));
            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandLoginInvalidPassword"), colorError));
        ServerEventLog(string.Format("{0} fails to log in (invalid password: {1}).", _serverClientService.GetClient(sourceClientId).PlayerName, password));
        return false;
    }

    public bool WelcomeMessage(int sourceClientId, string welcomeMessage)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.welcome))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        _config.WelcomeMessage = welcomeMessage;
        SendMessageToAll(string.Format(_languageService.Get("Server_CommandWelcomeChanged"), colorSuccess, _serverClientService.GetClient(sourceClientId).ColoredPlayername(colorSuccess), welcomeMessage));
        ServerEventLog(string.Format("{0} changes welcome message to {1}.", _serverClientService.GetClient(sourceClientId).PlayerName, welcomeMessage));
        _config.ConfigNeedsSaving = true;
        return true;
    }

    public bool SetLogging(int sourceClientId, string type, string option)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.logging))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        switch (type)
        {
            // all logging state
            case "-s":
                _serverPacketService.SendMessage(sourceClientId, $"Build: {_config.BuildLogging}");
                _serverPacketService.SendMessage(sourceClientId, $"Server events: {_config.ServerEventLogging}");
                _serverPacketService.SendMessage(sourceClientId, $"Chat: {_config.ChatLogging}");
                return true;
            case "-b":
                if (option.Equals("on"))
                {
                    _config.BuildLogging = true;
                    _config.ConfigNeedsSaving = true;
                    _serverPacketService.SendMessage(sourceClientId, string.Format("{0}Build logging enabled.", colorSuccess));
                    ServerEventLog(string.Format("{0} enables build logging.", _serverClientService.GetClient(sourceClientId).PlayerName));
                    return true;
                }

                if (option.Equals("off"))
                {
                    _config.BuildLogging = false;
                    _config.ConfigNeedsSaving = true;
                    _serverPacketService.SendMessage(sourceClientId, string.Format("{0}Build logging disabled.", colorSuccess));
                    ServerEventLog(string.Format("{0} disables build logging.", _serverClientService.GetClient(sourceClientId).PlayerName));
                    return true;
                }

                _serverPacketService.SendMessage(sourceClientId, string.Format("{0}Build logging: {1}", colorNormal, _config.BuildLogging));
                return true;
            case "-se":
                if (option.Equals("on"))
                {
                    _config.ServerEventLogging = true;
                    _config.ConfigNeedsSaving = true;
                    _serverPacketService.SendMessage(sourceClientId, string.Format("{0}Server event logging enabled.", colorSuccess));
                    ServerEventLog(string.Format("{0} enables server event logging.", _serverClientService.GetClient(sourceClientId).PlayerName));
                    return true;
                }

                if (option.Equals("off"))
                {
                    ServerEventLog(string.Format("{0} disables server event logging.", _serverClientService.GetClient(sourceClientId).PlayerName));
                    _config.ServerEventLogging = false;
                    _config.ConfigNeedsSaving = true;
                    _serverPacketService.SendMessage(sourceClientId, string.Format("{0}Server event logging disabled.", colorSuccess));
                    return true;
                }

                _serverPacketService.SendMessage(sourceClientId, string.Format("{0}Server event logging: {1}", colorNormal, _config.ServerEventLogging));
                return true;
            case "-c":
                if (option.Equals("on"))
                {
                    _config.ChatLogging = true;
                    _config.ConfigNeedsSaving = true;
                    _serverPacketService.SendMessage(sourceClientId, string.Format("{0}Chat logging enabled.", colorSuccess));
                    ServerEventLog(string.Format("{0} enables chat logging.", _serverClientService.GetClient(sourceClientId).PlayerName));
                    return true;
                }

                if (option.Equals("off"))
                {
                    _config.ChatLogging = false;
                    _config.ConfigNeedsSaving = true;
                    _serverPacketService.SendMessage(sourceClientId, string.Format("{0}Chat logging disabled.", colorSuccess));
                    ServerEventLog(string.Format("{0} disables chat logging.", _serverClientService.GetClient(sourceClientId).PlayerName));
                    return true;
                }

                _serverPacketService.SendMessage(sourceClientId, string.Format("{0}Chat logging: {1}", colorNormal, _config.ChatLogging));
                return true;
            default:
                _serverPacketService.SendMessage(sourceClientId, string.Format("{0}Invalid type: {1}", colorError, type));
                return false;
        }
    }

    public bool Kick(int sourceClientId, string target) => Kick(sourceClientId, target, "");

    public bool Kick(int sourceClientId, string target, string reason)
    {
        ClientOnServer targetClient = _serverClientService.GetClient(target);
        if (targetClient != null)
        {
            return this.Kick(sourceClientId, targetClient.Id, reason);
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandPlayerNotFound"), colorError, target));
        return false;
    }

    public bool Kick(int sourceClientId, int targetClientId) => this.Kick(sourceClientId, targetClientId, "");

    public bool Kick(int sourceClientId, int targetClientId, string reason)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.kick))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        if (!reason.Equals(""))
        {
            reason = _languageService.Get("Server_CommandKickBanReason") + reason + ".";
        }

        ClientOnServer targetClient = _serverClientService.GetClient(targetClientId);
        if (targetClient != null)
        {
            if (targetClient.ClientGroup.IsSuperior(_serverClientService.GetClient(sourceClientId).ClientGroup) || targetClient.ClientGroup.EqualLevel(_serverClientService.GetClient(sourceClientId).ClientGroup))
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandTargetUserSuperior"), colorError));
                return false;
            }

            string targetName = targetClient.PlayerName;
            string sourceName = _serverClientService.GetClient(sourceClientId).PlayerName;
            string targetNameColored = targetClient.ColoredPlayername(colorImportant);
            string sourceNameColored = _serverClientService.GetClient(sourceClientId).ColoredPlayername(colorImportant);
            SendMessageToAll(string.Format(_languageService.Get("Server_CommandKickMessage"), colorImportant, targetNameColored, sourceNameColored, reason));
            ServerEventLog(string.Format("{0} kicks {1}.{2}", sourceName, targetName, reason));
            _serverPacketService.SendPacket(targetClientId, ServerPackets.DisconnectPlayer(string.Format(_languageService.Get("Server_CommandKickNotification"), reason)));
            KillPlayer(targetClientId);
            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandNonexistantID"), colorError, targetClientId));
        return false;
    }

    public bool List(int sourceClientId, string type)
    {
        switch (type)
        {
            case "-clients":
            case "-c":
                if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.list_clients))
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
                    return false;
                }

                _serverPacketService.SendMessage(sourceClientId, colorImportant + "List of Players:");
                foreach (KeyValuePair<int, ClientOnServer> k in _serverClientService.Clients)
                {
                    // Format: Key Playername IP
                    _serverPacketService.SendMessage(sourceClientId, string.Format("[{0}] {1} {2}", k.Key, k.Value.ColoredPlayername(colorNormal), k.Value.Socket.RemoteEndPoint().AddressToString()));
                }

                return true;
            case "-clients2":
            case "-c2":
                if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.list_clients))
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
                    return false;
                }

                _serverPacketService.SendMessage(sourceClientId, colorImportant + "List of Players:");
                foreach (KeyValuePair<int, ClientOnServer> k in _serverClientService.Clients)
                {
                    // Format: Key Playername:Group:Privileges IP
                    _serverPacketService.SendMessage(sourceClientId, string.Format("[{0}] {1}", k.Key, k.Value.ToString()));
                }

                return true;
            case "-areas":
            case "-a":
                if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.list_areas))
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
                    return false;
                }

                _serverPacketService.SendMessage(sourceClientId, $"{colorImportant}List of Areas:");
                foreach (AreaConfig area in _config.Areas)
                {
                    _serverPacketService.SendMessage(sourceClientId, area.ToString());
                }

                return true;
            case "-bannedusers":
            case "-bu":
                if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.list_banned_users))
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
                    return false;
                }

                _serverPacketService.SendMessage(sourceClientId, $"{colorImportant}List of Banned Users:");
                foreach (UserEntry currentUser in BanList.BannedUsers)
                {
                    //Format:	Name: Reason
                    string reason = currentUser.Reason;
                    if (string.IsNullOrEmpty(reason))
                    {
                        reason = "";
                    }

                    _serverPacketService.SendMessage(sourceClientId, string.Format("{0}:{1}", currentUser.UserName, reason));
                }

                return true;
            case "-bannedips":
            case "-bip":
                if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.list_banned_users))
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
                    return false;
                }

                _serverPacketService.SendMessage(sourceClientId, $"{colorImportant}List of Banned IPs:");
                foreach (IPEntry currentIP in BanList.BannedIPs)
                {
                    //Format:	IP: Reason
                    string reason = currentIP.Reason;
                    if (string.IsNullOrEmpty(reason))
                    {
                        reason = "";
                    }

                    _serverPacketService.SendMessage(sourceClientId, string.Format("{0}:{1}", currentIP.IPAdress, reason));
                }

                return true;
            case "-groups":
            case "-g":
                if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.list_groups))
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
                    return false;
                }

                _serverPacketService.SendMessage(sourceClientId, $"{colorImportant}List of groups:");
                foreach (Group currenGroup in _serverClientService.ServerClient.Groups)
                {
                    _serverPacketService.SendMessage(sourceClientId, currenGroup.ToString());
                }

                return true;
            case "-saved_clients":
            case "-sc":
                if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.list_saved_clients))
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
                    return false;
                }

                _serverPacketService.SendMessage(sourceClientId, colorImportant + "List of saved clients:");
                foreach (Client currenClient in _serverClientService.ServerClient.Clients)
                {

                    _serverPacketService.SendMessage(sourceClientId, currenClient.ToString());
                }

                return true;
            default:
                _serverPacketService.SendMessage(sourceClientId, "Invalid parameter.");
                return false;
        }
    }

    public bool GiveAll(int sourceClientId, string target)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.giveall))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        ClientOnServer targetClient = _serverClientService.GetClient(target);
        if (targetClient != null)
        {
            string targetName = targetClient.PlayerName;
            string sourcename = _serverClientService.GetClient(sourceClientId).PlayerName;
            int maxStack = 9999; //TODO: Fetch this dynamically for each item - stacking
            foreach ((int id, BlockType? blockType) in _blockRegistry.BlockTypes)
            {
                if (!blockType.IsBuildable)
                {
                    continue;
                }

                Inventory inventory = GetPlayerInventory(targetName);
                InventoryUtil util = GetInventoryUtil(inventory);

                // Try to find existing stack
                bool found = false;
                for (int yy = 0; yy < util.CellCountY && !found; yy++)
                {
                    for (int xx = 0; xx < util.CellCountX && !found; xx++)
                    {
                        GridPoint key = new(xx, yy);
                        if (!inventory.Items.TryGetValue(key, out InventoryItem currentItem))
                        {
                            continue;
                        }

                        if (currentItem?.InventoryItemType == InventoryItemType.Block && currentItem.BlockId == id)
                        {
                            currentItem.BlockCount = maxStack;
                            found = true;
                        }
                    }
                }

                // Find empty slot
                if (!found)
                {
                    for (int yy = 0; yy < util.CellCountY && !found; yy++)
                    {
                        for (int xx = 0; xx < util.CellCountX && !found; xx++)
                        {
                            Point cell = new(xx, yy);
                            if (util.ItemAtCell(cell) == null)
                            {
                                inventory.Items[new GridPoint(xx, yy)] = new InventoryItem
                                {
                                    InventoryItemType = InventoryItemType.Block,
                                    BlockId = id,
                                    BlockCount = maxStack
                                };
                                found = true;
                            }
                        }
                    }
                }

                targetClient.IsInventoryDirty = true;
            }

            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandGiveAll"), colorSuccess, targetName));
            ServerEventLog(string.Format("{0} gives all to {1}.", sourcename, targetName));
            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandPlayerNotFound"), colorError, target));
        return false;
    }

    public bool Give(int sourceClientId, string target, string blockname, int amount)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.give))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        ClientOnServer targetClient = _serverClientService.GetClient(target);
        if (targetClient != null)
        {
            string targetName = targetClient.PlayerName;
            string sourcename = _serverClientService.GetClient(sourceClientId).PlayerName;
            int maxStack = 9999; //TODO: Fetch this dynamically for each item - stacking
            if (amount < 0)
            {
                return false;
            }

            if (amount > maxStack)
            {
                amount = maxStack;
            }

            Inventory inventory = GetPlayerInventory(targetName);
            InventoryUtil util = GetInventoryUtil(inventory);
            foreach ((int id, BlockType? blockType) in _blockRegistry.BlockTypes)
            {
                if (!blockType.IsBuildable)
                {
                    continue;
                }

                if (!blockType.Name.Equals(blockname, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                // Try to find existing stack
                bool found = false;
                for (int xx = 0; xx < util.CellCountX && !found; xx++)
                {
                    for (int yy = 0; yy < util.CellCountY && !found; yy++)
                    {
                        GridPoint key = new(xx, yy);
                        if (!inventory.Items.TryGetValue(key, out InventoryItem currentItem))
                        {
                            continue;
                        }

                        if (currentItem?.InventoryItemType != InventoryItemType.Block || currentItem.BlockId != id)
                        {
                            continue;
                        }

                        if (amount == 0)
                        {
                            inventory.Items.Remove(key);
                        }
                        else
                        {
                            currentItem.BlockCount = Math.Min(currentItem.BlockCount + amount, maxStack);
                        }

                        found = true;
                    }
                }

                // Block not yet in inventory — add to first free slot
                if (!found)
                {
                    for (int xx = 0; xx < util.CellCountX && !found; xx++)
                    {
                        for (int yy = 0; yy < util.CellCountY && !found; yy++)
                        {
                            if (util.ItemAtCell(new Point(xx, yy)) != null)
                            {
                                continue;
                            }

                            inventory.Items[new GridPoint(xx, yy)] = new InventoryItem
                            {
                                InventoryItemType = InventoryItemType.Block,
                                BlockId = id,
                                BlockCount = amount
                            };
                            found = true;
                        }
                    }
                }

                targetClient.IsInventoryDirty = true;
            }

            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandGiveSuccess"), colorSuccess, amount, blockname, targetName));
            ServerEventLog(string.Format("{0} gives {1} {2} to {3}.", sourcename, amount, blockname, targetName));
            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandPlayerNotFound"), colorError, target));
        return false;
    }

    public bool ResetInventory(int sourceClientId, string target)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.reset_inventory))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        ClientOnServer targetClient = _serverClientService.GetClient(target);
        if (targetClient != null)
        {
            ResetPlayerInventory(targetClient);
            SendMessageToAll(string.Format(_languageService.Get("Server_CommandResetInventorySuccess"), colorImportant, _serverClientService.GetClient(sourceClientId).ColoredPlayername(colorImportant), targetClient.ColoredPlayername(colorImportant)));
            ServerEventLog(string.Format("{0} resets inventory of {1}.", _serverClientService.GetClient(sourceClientId).PlayerName, targetClient.PlayerName));
            return true;
        }
        // Player is not online.
        if (Inventory != null && Inventory.ContainsKey(target))
        {
            Inventory.Remove(target);
            SendMessageToAll(string.Format(_languageService.Get("Server_CommandResetInventoryOfflineSuccess"), colorImportant, _serverClientService.GetClient(sourceClientId).ColoredPlayername(colorImportant), target));
            ServerEventLog(string.Format("{0} resets inventory of {1} (offline).", _serverClientService.GetClient(sourceClientId).PlayerName, target));
            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandPlayerNotFound"), colorError, target));
        return false;
    }

    public bool Monsters(int sourceClientId, string option)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.monsters))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        _config.Monsters = option.Equals("off") ? false : true;
        _config.ConfigNeedsSaving = true;
        if (!_config.Monsters)
        {
            foreach (KeyValuePair<int, ClientOnServer> k in _serverClientService.Clients)
            {
                _serverPacketService.SendPacket(k.Key, Serialize(new Packet_Server()
                {
                    Id = Packet_ServerIdEnum.RemoveMonsters
                }));
            }
        }

        SendMessageToAll(string.Format(_languageService.Get("Server_CommandMonstersToggle"), _serverClientService.GetClient(sourceClientId).ColoredPlayername(colorSuccess), option));
        ServerEventLog(string.Format("{0} turns monsters {1}.", _serverClientService.GetClient(sourceClientId).PlayerName, option));
        return true;
    }

    public bool AreaAdd(int sourceClientId, int id, string coords, string[] permittedGroups, string[] permittedUsers, int? level)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.area_add))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        if (_config.Areas.Find(v => v.Id == id) != null)
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandAreaAddIdInUse"), colorError));
            return false;
        }

        AreaConfig newArea = new() { Id = id, Coords = coords };
        if (permittedGroups != null)
        {
            for (int i = 0; i < permittedGroups.Length; i++)
            {
                newArea.PermittedGroups.Add(permittedGroups[i]);
            }
        }

        if (permittedUsers != null)
        {
            for (int i = 0; i < permittedUsers.Length; i++)
            {
                newArea.PermittedUsers.Add(permittedUsers[i]);
            }
        }

        if (level != null)
        {
            newArea.Level = level;
        }

        _config.Areas.Add(newArea);
        _config.ConfigNeedsSaving = true;
        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandAreaAddSuccess"), colorSuccess, newArea.ToString()));
        ServerEventLog(string.Format("{0} adds area: {1}.", _serverClientService.GetClient(sourceClientId), newArea.ToString()));
        return true;
    }

    public bool AreaDelete(int sourceClientId, int id)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.area_delete))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        AreaConfig targetArea = _config.Areas.Find(v => v.Id == id);
        if (targetArea == null)
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandAreaDeleteNonexistant"), colorError));
            return false;
        }

        _config.Areas.Remove(targetArea);
        _config.ConfigNeedsSaving = true;
        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandAreaDeleteSuccess"), colorSuccess));
        ServerEventLog(string.Format("{0} deletes area: {1}.", _serverClientService.GetClient(sourceClientId).PlayerName, id));
        return true;
    }

    public bool Announcement(int sourceClientId, string message)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.announcement))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        ServerEventLog(string.Format("{0} announced: {1}.", _serverClientService.GetClient(sourceClientId).PlayerName, message));
        SendMessageToAll(string.Format(_languageService.Get("Server_CommandAnnouncementMessage"), colorError, message));
        return true;
    }

    public bool ClearInterpreter(int sourceClientId)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.run))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        _serverClientService.GetClient(sourceClientId).Interpreter = null;
        _serverPacketService.SendMessage(sourceClientId, "Interpreter cleared.");
        return true;
    }

    public bool SetSpawnPosition(int sourceClientId, string targetType, string target, int x, int y, int? z)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.set_spawn))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        // validate spawn coordinates
        int rZ = 0;
        if (z == null)
        {
            if (!VectorUtils.IsValidPos(_serverMapStorage, x, y))
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandSetSpawnInvalidCoordinates"), colorError));
                return false;
            }

            rZ = VectorUtils.BlockHeight(_serverMapStorage, 0, x, y);
        }
        else
        {
            rZ = z.Value;
        }

        if (!VectorUtils.IsValidPos(_serverMapStorage, x, y, rZ))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandSetSpawnInvalidCoordinates"), colorError));
            return false;
        }

        switch (targetType)
        {
            case "-default":
            case "-d":
                _serverClientService.ServerClient.DefaultSpawn = new Spawn() { x = x, y = y, z = z };
                _serverClientService.ServerClientNeedsSaving = true;
                // Inform related players.
                bool hasEntry = false;
                foreach (KeyValuePair<int, ClientOnServer> k in _serverClientService.Clients)
                {
                    hasEntry = false;
                    if (k.Value.ClientGroup.Spawn != null)
                    {
                        hasEntry = true;
                    }
                    else
                    {
                        foreach (Client client in _serverClientService.ServerClient.Clients)
                        {
                            if (client.Name.Equals(k.Value.PlayerName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (client.Spawn != null)
                                {
                                    hasEntry = true;
                                }

                                break;
                            }
                        }
                    }

                    if (!hasEntry)
                    {
                        this.SendPlayerSpawnPosition(k.Key, x, y, rZ);
                    }
                }

                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandSetSpawnDefaultSuccess"), colorSuccess, x, y, rZ));
                ServerEventLog(string.Format("{0} sets default spawn to {1},{2}{3}.", _serverClientService.GetClient(sourceClientId).PlayerName, x, y, z == null ? "" : "," + z.Value));
                return true;
            case "-group":
            case "-g":
                // Check if group even exists.
                Group? targetGroup = _serverClientService.ServerClient.Groups.Find(
                    delegate (Group grp)
                    {
                        return grp.Name.Equals(target, StringComparison.InvariantCultureIgnoreCase);
                    }
                );
                if (targetGroup == null)
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandGroupNotFound"), colorError, target));
                    return false;
                }

                targetGroup.Spawn = new Spawn()
                {
                    x = x,
                    y = y,
                    z = z,
                };
                _serverClientService.ServerClientNeedsSaving = true;
                // Inform related players.
                hasEntry = false;
                foreach (KeyValuePair<int, ClientOnServer> k in _serverClientService.Clients)
                {
                    if (k.Value.ClientGroup.Name.Equals(targetGroup.Name))
                    {
                        // Inform only if there is no spawn set under clients.
                        foreach (Client client in _serverClientService.ServerClient.Clients)
                        {
                            if (client.Name.Equals(k.Value.PlayerName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (client.Spawn != null)
                                {
                                    hasEntry = true;
                                }

                                break;
                            }
                        }

                        if (!hasEntry)
                        {
                            this.SendPlayerSpawnPosition(k.Key, x, y, rZ);
                        }
                    }
                }

                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandSetSpawnGroupSuccess"), colorSuccess, targetGroup.Name, x, y, rZ));
                ServerEventLog(string.Format("{0} sets spawn of group {1} to {2},{3}{4}.", _serverClientService.GetClient(sourceClientId).PlayerName, targetGroup.Name, x, y, z == null ? "" : "," + z.Value));
                return true;
            case "-player":
            case "-p":
                // Get related client.
                ClientOnServer targetClient = _serverClientService.GetClient(target);
                int? targetClientId = null;
                if (targetClient != null)
                {
                    targetClientId = targetClient.Id;
                }

                string targetClientPlayername = targetClient == null ? target : targetClient.PlayerName;

                Client? clientEntry = _serverClientService.ServerClient.Clients.Find(
                    delegate (Client client)
                    {
                        return client.Name.Equals(targetClientPlayername, StringComparison.InvariantCultureIgnoreCase);
                    }
                );
                if (clientEntry == null)
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandPlayerNotFound"), colorError, target));
                    return false;
                }
                // Change or add spawn entry of client.
                clientEntry.Spawn = new Spawn()
                {
                    x = x,
                    y = y,
                    z = z,
                };
                _serverClientService.ServerClientNeedsSaving = true;
                // Inform player if he's online.
                if (targetClientId != null)
                {
                    this.SendPlayerSpawnPosition(targetClientId.Value, x, y, rZ);
                }

                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandSetSpawnPlayerSuccess"), colorSuccess, targetClientPlayername, x, y, rZ));
                ServerEventLog(string.Format("{0} sets spawn of player {1} to {2},{3}{4}.", _serverClientService.GetClient(sourceClientId).PlayerName, targetClientPlayername, x, y, z == null ? "" : "," + z.Value));
                return true;
            default:
                _serverPacketService.SendMessage(sourceClientId, _languageService.Get("Server_CommandInvalidType"));
                return false;
        }
    }

    public bool SetSpawnPosition(int sourceClientId, int x, int y, int? z)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.set_home))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        Console.WriteLine($"{x} {y} {z}");

        // Validate spawn position.
        int rZ = 0;
        if (z == null)
        {
            if (!VectorUtils.IsValidPos(_serverMapStorage, x, y))
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandSetSpawnInvalidCoordinates"), colorError));
                return false;
            }

            rZ = VectorUtils.BlockHeight(_serverMapStorage, 0, x, y);
        }
        else
        {
            rZ = z.Value;
        }

        if (!VectorUtils.IsValidPos(_serverMapStorage, x, y, rZ))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandSetSpawnInvalidCoordinates"), colorError));
            return false;
        }

        // Get related client entry.
        Client? clientEntry = _serverClientService.ServerClient.Clients.Find(
            delegate (Client client)
            {
                return client.Name.Equals(_serverClientService.GetClient(sourceClientId).PlayerName, StringComparison.InvariantCultureIgnoreCase);
            }
        );
        // TODO: When guests have "set_home" privilege, count of client entries can quickly grow.
        if (clientEntry == null)
        {
            clientEntry = new Client
            {
                Name = _serverClientService.GetClient(sourceClientId).PlayerName,
                Group = _serverClientService.GetClient(sourceClientId).ClientGroup.Name
            };
            _serverClientService.ServerClient.Clients.Add(clientEntry);
        }
        // Change or add spawn entry of client.
        clientEntry.Spawn = new Spawn()
        {
            x = x,
            y = y,
            z = z,
        };
        _serverClientService.ServerClientNeedsSaving = true;
        // Send player new spawn position.
        this.SendPlayerSpawnPosition(sourceClientId, x, y, rZ);
        return true;
    }

    public bool PrivilegeAdd(int sourceClientId, string target, string privilege)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.privilege_add))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        ClientOnServer targetClient = _serverClientService.GetClient(target);
        if (targetClient != null)
        {
            if (targetClient.Privileges.Contains(privilege))
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandPrivilegeAddHasAlready"), colorError, target, privilege.ToString()));
                return false;
            }

            targetClient.Privileges.Add(privilege);
            if (privilege.Equals(ServerClientMisc.Privilege.freemove))
            {
                SendFreemoveState(targetClient.Id, targetClient.Privileges.Contains(ServerClientMisc.Privilege.freemove));
            }

            SendMessageToAll(string.Format(_languageService.Get("Server_CommandPrivilegeAddSuccess"), colorSuccess, targetClient.ColoredPlayername(colorSuccess), privilege.ToString()));
            ServerEventLog(string.Format("{0} gives {1} privilege {2}.", _serverClientService.GetClient(sourceClientId).PlayerName, targetClient.PlayerName, privilege.ToString()));
            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandNonexistantPlayer"), colorError, target));
        return false;
    }

    public bool PrivilegeRemove(int sourceClientId, string target, string privilege)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.privilege_remove))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        ClientOnServer targetClient = _serverClientService.GetClient(target);
        if (targetClient != null)
        {
            if (!targetClient.Privileges.Remove(privilege))
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandPrivilegeRemoveNoPriv"), colorError, target, privilege.ToString()));
                return false;
            }

            if (privilege.Equals(ServerClientMisc.Privilege.freemove))
            {
                SendFreemoveState(targetClient.Id, targetClient.Privileges.Contains(ServerClientMisc.Privilege.freemove));
            }

            SendMessageToAll(string.Format(_languageService.Get("Server_CommandPrivilegeRemoveSuccess"), colorImportant, targetClient.ColoredPlayername(colorImportant), privilege.ToString()));
            ServerEventLog(string.Format("{0} removes {1} privilege {2}.", _serverClientService.GetClient(sourceClientId).PlayerName, targetClient.PlayerName, privilege.ToString()));
            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandNonexistantPlayer"), colorError, target));
        return false;
    }

    public bool RestartServer(int sourceClientId)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.restart))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        SendMessageToAll(string.Format(_languageService.Get("Server_CommandRestartSuccess"), colorImportant, _serverClientService.GetClient(sourceClientId).ColoredPlayername(colorImportant)));
        ServerEventLog(string.Format("{0} restarts server.", _serverClientService.GetClient(sourceClientId).PlayerName));
        Restart();
        return true;
    }

    public bool ShutdownServer(int sourceClientId)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.shutdown))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        SendMessageToAll(string.Format(_languageService.Get("Server_CommandShutdownSuccess"), colorImportant, _serverClientService.GetClient(sourceClientId).ColoredPlayername(colorImportant)));
        ServerEventLog(string.Format("{0} shuts down server.", _serverClientService.GetClient(sourceClientId).PlayerName));
        Exit();
        return true;
    }

    public bool TeleportToPlayer(int sourceClientId, int clientTo)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.tp))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        ClientOnServer t = _serverClientService.GetClient(clientTo);
        ServerEntityPositionAndOrientation pos = t.Entity.Position.Clone();
        _serverClientService.GetClient(sourceClientId).PositionOverride = pos;
        return true;
    }

    public bool TeleportToPosition(int sourceClientId, int x, int y, int? z)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.tp_pos))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        // validate target position
        int rZ;
        if (z == null)
        {
            if (!VectorUtils.IsValidPos(_serverMapStorage, x, y))
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandTeleportInvalidCoordinates"), colorError));
                return false;
            }

            rZ = VectorUtils.BlockHeight(_serverMapStorage, 0, x, y);
        }
        else
        {
            rZ = z.Value;
        }

        if (!VectorUtils.IsValidPos(_serverMapStorage, x, y, rZ))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandTeleportInvalidCoordinates"), colorError));
            return false;
        }

        ClientOnServer client = _serverClientService.GetClient(sourceClientId);
        ServerEntityPositionAndOrientation pos = client.Entity.Position.Clone();
        pos.X = x;
        pos.Y = rZ;
        pos.Z = y;
        client.PositionOverride = pos;
        _serverPacketService.SendMessage(client.Id, string.Format(_languageService.Get("Server_CommandTeleportSuccess"), colorSuccess, x, y, rZ));
        return true;
    }

    public bool TeleportPlayer(int sourceClientId, string target, int x, int y, int? z)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.teleport_player))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        // validate target position
        int rZ;
        if (z == null)
        {
            if (!VectorUtils.IsValidPos(_serverMapStorage, x, y))
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandTeleportInvalidCoordinates"), colorError));
                return false;
            }

            rZ = VectorUtils.BlockHeight(_serverMapStorage, 0, x, y);
        }
        else
        {
            rZ = z.Value;
        }

        if (!VectorUtils.IsValidPos(_serverMapStorage, x, y, rZ))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandTeleportInvalidCoordinates"), colorError));
            return false;
        }

        ClientOnServer targetClient = _serverClientService.GetClient(target);
        if (targetClient != null)
        {
            ServerEntityPositionAndOrientation pos = _serverClientService.Clients[targetClient.Id].Entity.Position;
            pos.X = x;
            pos.Y = rZ;
            pos.Z = y;
            _serverClientService.Clients[targetClient.Id].PositionOverride = pos;
            _serverPacketService.SendMessage(targetClient.Id, string.Format(_languageService.Get("Server_CommandTeleportTargetMessage"), colorImportant, x, y, rZ, _serverClientService.GetClient(sourceClientId).ColoredPlayername(colorImportant)));
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandTeleportSourceMessage"), colorSuccess, targetClient.ColoredPlayername(colorSuccess), x, y, rZ));
            ServerEventLog(string.Format("{0} teleports {1} to {2} {3} {4}.", _serverClientService.GetClient(sourceClientId).PlayerName, targetClient.PlayerName, x, y, rZ));
            return true;
        }

        _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandNonexistantPlayer"), colorError, target));
        return false;
    }

    public bool SetFillAreaLimit(int sourceClientId, string targetType, string target, int maxFill)
    {
        if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.fill_limit))
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
            return false;
        }

        switch (targetType)
        {
            case "-default":
            case "-d":
                _serverClientService.ServerClient.DefaultFillLimit = maxFill;
                _serverClientService.ServerClientNeedsSaving = true;
                // Inform related players.
                bool hasEntry = false;
                foreach (KeyValuePair<int, ClientOnServer> k in _serverClientService.Clients)
                {
                    hasEntry = false;
                    if (k.Value.ClientGroup.FillLimit != null)
                    {
                        hasEntry = true;
                    }
                    else
                    {
                        foreach (Client client in _serverClientService.ServerClient.Clients)
                        {
                            if (client.Name.Equals(k.Value.PlayerName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (client.FillLimit != null)
                                {
                                    hasEntry = true;
                                }

                                break;
                            }
                        }
                    }

                    if (!hasEntry)
                    {
                        this.SetFillAreaLimit(k.Key);
                    }
                }

                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandFillLimitDefaultSuccess"), colorSuccess, maxFill));
                ServerEventLog(string.Format("{0} sets default fill area limit to {1}.", _serverClientService.GetClient(sourceClientId).PlayerName, maxFill));
                return true;
            case "-group":
            case "-g":
                // Check if group even exists.
                Group? targetGroup = _serverClientService.ServerClient.Groups.Find(
                    delegate (Group grp)
                    {
                        return grp.Name.Equals(target, StringComparison.InvariantCultureIgnoreCase);
                    }
                );
                if (targetGroup == null)
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandGroupNotFound"), colorError, target));
                    return false;
                }

                targetGroup.FillLimit = maxFill;
                _serverClientService.ServerClientNeedsSaving = true;
                // Inform related players.
                hasEntry = false;
                foreach (KeyValuePair<int, ClientOnServer> k in _serverClientService.Clients)
                {
                    if (k.Value.ClientGroup.Name.Equals(targetGroup.Name))
                    {
                        // Inform only if there is no spawn set under clients.
                        foreach (Client client in _serverClientService.ServerClient.Clients)
                        {
                            if (client.Name.Equals(k.Value.PlayerName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                if (client.FillLimit != null)
                                {
                                    hasEntry = true;
                                }

                                break;
                            }
                        }

                        if (!hasEntry)
                        {
                            this.SetFillAreaLimit(k.Key);
                        }
                    }
                }

                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandFillLimitGroupSuccess"), colorSuccess, targetGroup.Name, maxFill));
                ServerEventLog(string.Format("{0} sets spawn of group {1} to {2}.", _serverClientService.GetClient(sourceClientId).PlayerName, targetGroup.Name, maxFill));
                return true;
            case "-player":
            case "-p":
                // Get related client.
                ClientOnServer targetClient = _serverClientService.GetClient(target);
                int? targetClientId = null;
                if (targetClient != null)
                {
                    targetClientId = targetClient.Id;
                }

                string targetClientPlayername = targetClient == null ? target : targetClient.PlayerName;

                Client? clientEntry = _serverClientService.ServerClient.Clients.Find(
                    delegate (Client client)
                    {
                        return client.Name.Equals(targetClientPlayername, StringComparison.InvariantCultureIgnoreCase);
                    }
                );
                if (clientEntry == null)
                {
                    _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandPlayerNotFound"), colorError, target));
                    return false;
                }
                // Change or add spawn entry of client.
                clientEntry.FillLimit = maxFill;
                _serverClientService.ServerClientNeedsSaving = true;
                // Inform player if he's online.
                if (targetClientId != null)
                {
                    this.SetFillAreaLimit(targetClientId.Value);
                }

                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandFillLimitPlayerSuccess"), colorSuccess, targetClientPlayername, maxFill));
                ServerEventLog(string.Format("{0} sets fill area limit of player {1} to {2}.", _serverClientService.GetClient(sourceClientId).PlayerName, targetClientPlayername, maxFill));
                return true;
            default:
                _serverPacketService.SendMessage(sourceClientId, _languageService.Get("Server_CommandInvalidType"));
                return false;
        }
    }

    public bool TimeCommand(int sourceClientId, string argument)
    {
        string[] strSplit = argument.Split(' ');

        if (strSplit.Length == 2)
        {
            //We assume that all parameterized commands require a privilege
            if (!PlayerHasPrivilege(sourceClientId, ServerClientMisc.Privilege.time))
            {
                _serverPacketService.SendMessage(sourceClientId, string.Format(_languageService.Get("Server_CommandInsufficientPrivileges"), colorError));
                return false;
            }

            //We expect a operation and a value
            string strValue = strSplit[1];

            switch (strSplit[0])
            {
                case "set":
                    {
                        if (!strValue.Contains(':'))
                        {
                            //If only a number is present, the days will be set to the given number
                            //since we don't want that, a ":" is enforced
                            _serverPacketService.SendMessage(sourceClientId, $"{colorError}{_languageService.Get("Server_CommandException")} unable to convert \"{strValue}\" to a time");
                        }
                        else if (TimeSpan.TryParse(strValue, out TimeSpan time))
                        {
                            _gameTimer.Set(time);
                            _serverPacketService.SendMessage(sourceClientId, $"The time is: {_gameTimer.Time}");
                        }
                        else
                        {
                            _serverPacketService.SendMessage(sourceClientId, $"{colorError}{_languageService.Get("Server_CommandException")} unable to convert \"{strValue}\" to a time");
                        }
                    }

                    break;
                case "add":
                    {

                        if (int.TryParse(strValue, out int nMinuts))
                        {
                            //only a number
                            //take it as minutes
                            _gameTimer.Add(TimeSpan.FromMinutes(nMinuts));
                            _serverPacketService.SendMessage(sourceClientId, $"The time is: {_gameTimer.Time}");
                        }
                        else if (TimeSpan.TryParse(strValue, out TimeSpan time))
                        {
                            _gameTimer.Add(time);
                            _serverPacketService.SendMessage(sourceClientId, $"The time is: {_gameTimer.Time}");
                        }
                        else
                        {
                            _serverPacketService.SendMessage(sourceClientId, $"{colorError}{_languageService.Get("Server_CommandException")} unable to convert \"{strValue}\" to a time");
                        }
                    }

                    break;
                case "speed":
                    {

                        if (!int.TryParse(strValue, out int nSpeed))
                        {
                            _serverPacketService.SendMessage(sourceClientId, $"{colorError}{_languageService.Get("Server_CommandException")} unable to convert \"{strValue}\" to a number");
                        }
                        else
                        {
                            _gameTimer.SpeedOfTime = nSpeed;
                            _serverPacketService.SendMessage(sourceClientId, "speed of time changed");
                        }
                    }

                    break;
            }
        }
        else
        {
            _serverPacketService.SendMessage(sourceClientId, string.Format("Current time: Year {0}, Day {1}, {2}:{3}:{4}", _gameTimer.Year, _gameTimer.Day, _gameTimer.Time.Hours, _gameTimer.Time.Minutes, _gameTimer.Time.Seconds));
        }

        return true;
    }
}
