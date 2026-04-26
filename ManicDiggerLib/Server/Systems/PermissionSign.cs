using ManicDigger;

/// <summary>
/// Server system that manages Permission Sign entities. A Permission Sign defines
/// a 3D area in the world that restricts block interactions to a specific player
/// or group. Signs are placed with the PermissionSign tool and configured via an
/// in-game dialog. Permission checks are evaluated against all signs in the 3×3×3
/// chunk neighbourhood around the block being acted on.
/// </summary>
public class ServerSystemPermissionSign : ServerSystem
{
    private Server server;

    private const int AreaSize = 32;
    private const int GroupButtonOffsetY = 150;
    private const int GroupButtonStepY = 50;
    private const string GroupIdPrefix = "PermissionSignGroup";
    private const string OkWidgetId = "UsePermissionSign_OK";
    private const string DialogKey = "UseSign";

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void Initialize(Server server)
    {
        this.server = server;
        server.modManager.RegisterOnBlockUseWithTool(OnUseWithTool);
        server.modEventHandlers.onupdateentity.Add(UpdateEntity);
        server.modEventHandlers.onuseentity.Add(OnUseEntity);
        server.modEventHandlers.ondialogclick2.Add(OnDialogClick);
        server.modEventHandlers.onpermission.Add(OnPermission);
    }

    // -------------------------------------------------------------------------
    // Placement
    // -------------------------------------------------------------------------

    /// <summary>
    /// Handles using the PermissionSign tool on a block. Places a new
    /// <see cref="ServerEntityPermissionSign"/> entity at the target position,
    /// facing the placing player, defaulting to the "Admin" group.
    /// </summary>
    private void OnUseWithTool(int player, int x, int y, int z, int tool)
    {
        if (server.modManager.GetBlockName(tool) != "PermissionSign") return;
        if (server.d_Map.GetChunk(x, y, z) == null) return;
        if (!server.CheckBuildPrivileges(player, x, y, z, PacketBlockSetMode.Create)) return;
        if (!CheckAreaPrivilege(player)) return;

        var e = new ServerEntity
        {
            Position = new ServerEntityPositionAndOrientation
            {
                X = x + One / 2,
                Y = z,
                Z = y + One / 2
            },
            PermissionSign = new ServerEntityPermissionSign
            {
                Name = "Admin",
                Type = PermissionSignType.Group
            }
        };

        e.Position.Heading = EntityHeading.GetHeading(
            server.modManager.GetPlayerPositionX(player),
            server.modManager.GetPlayerPositionY(player),
            e.Position.X, e.Position.Z);

        server.AddEntity(x, y, z, e);
    }

    // -------------------------------------------------------------------------
    // Entity update (visual + area)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Runs each tick for every entity. Updates the visual model, display text,
    /// and restricted draw area for permission sign entities. The area extends
    /// in the direction the sign faces, determined by the entity's heading.
    /// </summary>
    private void UpdateEntity(int chunkx, int chunky, int chunkz, int id)
    {
        ServerEntity e = server.GetEntity(chunkx, chunky, chunkz, id);
        if (e.PermissionSign == null) return;

        // Model
        e.DrawModel ??= new ServerEntityAnimatedModel();
        e.DrawModel.Model = "signmodel.txt";
        e.DrawModel.Texture = "permissionsignmodel.png";
        e.DrawModel.ModelHeight = One * 13 / 10;

        // Text (group names are prefixed red)
        e.DrawText ??= new ServerEntityDrawText();
        e.DrawText.Text = e.PermissionSign.Type == PermissionSignType.Group
            ? $"&4{e.PermissionSign.Name}"
            : e.PermissionSign.Name;
        e.DrawText.Dx = One * 3 / 32;
        e.DrawText.Dy = One * 36 / 32;
        e.DrawText.Dz = One * 3 / 32;

        // Name tag
        e.Usable = true;
        e.DrawName ??= new ServerEntityDrawName { Name = "Permission Sign", OnlyWhenSelected = true };
        // Restricted area (extends in the direction the sign faces)
        e.DrawArea ??= new ServerEntityDrawArea();
        UpdateDrawArea(e);
    }

    /// <summary>
    /// Positions the draw area based on the sign's heading. The area always
    /// extends one full <see cref="AreaSize"/> in the direction the sign faces.
    /// </summary>
    private static void UpdateDrawArea(ServerEntity e)
    {
        int px = (int)e.Position.X;
        int py = (int)e.Position.Y;
        int pz = (int)e.Position.Z;
        int half = AreaSize / 2;

        // Quantise heading into 4 cardinal directions (north/east/south/west)
        int rotDir = (byte)(e.Position.Heading + 255 / 8) / 64;
        e.DrawArea.X = rotDir switch
        {
            1 => px,
            3 => px - AreaSize,
            _ => px - half
        };
        e.DrawArea.Y = py - half;
        e.DrawArea.Z = rotDir switch
        {
            0 => pz - AreaSize,
            2 => pz,
            _ => pz - half
        };
        e.DrawArea.SizeX = AreaSize;
        e.DrawArea.SizeY = AreaSize;
        e.DrawArea.SizeZ = AreaSize;
    }

    // -------------------------------------------------------------------------
    // Dialog — open
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens the permission sign configuration dialog for the interacting player.
    /// The dialog contains a text box for entering a player name, a "Set player"
    /// button, and one button per server group.
    /// </summary>
    private void OnUseEntity(int player, int chunkx, int chunky, int chunkz, int id)
    {
        ServerEntity e = server.GetEntity(chunkx, chunky, chunkz, id);
        if (e.PermissionSign == null) return;
        if (!CheckAreaPrivilege(player)) return;

        var font = new DialogFont("Verdana", 11f, DialogFontStyle.Bold);
        var groups = server.serverClient.Groups;
        int widgetCount = 0;

        var d = new Dialog
        {
            Width = 400,
            Height = 400,
            IsModal = true,
            Widgets = new Widget[4 + groups.Count * 2]
        };

        d.Widgets[widgetCount++] = Widget.MakeSolid(0, 0, 400, 400, ColorUtils.ColorFromArgb(255, 50, 50, 50));
        d.Widgets[widgetCount++] = Widget.MakeTextBox(e.PermissionSign.Name, font, 50, 50, 200, 50, ColorUtils.ColorFromArgb(255, 0, 0, 0));

        for (int i = 0; i < groups.Count; i++)
        {
            int buttonY = GroupButtonOffsetY + i * GroupButtonStepY;
            Widget groupButton = Widget.MakeSolid(50, buttonY, 100, 40, ColorUtils.ColorFromArgb(255, 100, 100, 100));
            groupButton.ClickKey = (char)13;
            groupButton.Id = GroupIdPrefix + groups[i].Name;
            d.Widgets[widgetCount++] = groupButton;
            d.Widgets[widgetCount++] = Widget.MakeText(groups[i].Name, font, 50, buttonY, ColorUtils.ColorFromArgb(255, 0, 0, 0));
        }

        Widget okButton = Widget.MakeSolid(200, 50, 100, 50, ColorUtils.ColorFromArgb(255, 100, 100, 100));
        okButton.ClickKey = (char)13;
        okButton.Id = OkWidgetId;
        d.Widgets[widgetCount++] = okButton;
        d.Widgets[widgetCount++] = Widget.MakeText("Set player", font, 200, 50, ColorUtils.ColorFromArgb(255, 0, 0, 0));

        server.clients[player].editingSign = new ServerEntityId
        {
            chunkx = chunkx,
            chunky = chunky,
            chunkz = chunkz,
            id = id
        };
        server.SendDialog(player, DialogKey, d);
    }

    // -------------------------------------------------------------------------
    // Dialog — click
    // -------------------------------------------------------------------------

    /// <summary>
    /// Handles a button click in the permission sign dialog.
    /// <list type="bullet">
    ///   <item><c>UsePermissionSign_OK</c> — sets the sign to the typed player name,
    ///         promoting to group type if the name matches an existing group.</item>
    ///   <item><c>PermissionSignGroup*</c> — sets the sign directly to the clicked group.</item>
    /// </list>
    /// An empty name despawns the sign; a non-empty name updates and marks it dirty.
    /// </summary>
    private void OnDialogClick(DialogClickArgs args)
    {
        if (!TryResolveDialogTarget(args, out string name, out PermissionSignType type))
            return;

        ClientOnServer client = server.clients[args.Player];
        ServerEntityId id = client.editingSign;
        client.editingSign = null;

        if (!string.IsNullOrEmpty(name))
        {
            ServerEntity e = server.GetEntity(id.chunkx, id.chunky, id.chunkz, id.id);
            e.PermissionSign.Name = name;
            e.PermissionSign.Type = type;
            server.SetEntityDirty(id);
        }
        else
        {
            server.DespawnEntity(id);
        }

        server.SendDialog(args.Player, DialogKey, null);
    }

    /// <summary>
    /// Parses the clicked widget ID to determine the target name and type.
    /// Returns <c>false</c> if the widget belongs to an unrelated dialog.
    /// </summary>
    private bool TryResolveDialogTarget(DialogClickArgs args, out string name, out PermissionSignType type)
    {
        name = null;
        type = PermissionSignType.Player;

        if (args.WidgetId == OkWidgetId)
        {
            string candidate = args.TextBoxValue[1];
            Group matchedGroup = server.serverClient.Groups
                .FirstOrDefault(g => g.Name == candidate);
            name = candidate;
            if (matchedGroup != null)
                type = PermissionSignType.Group;
            return true;
        }

        if (args.WidgetId.StartsWith(GroupIdPrefix))
        {
            Group matchedGroup = server.serverClient.Groups
                .FirstOrDefault(g => GroupIdPrefix + g.Name == args.WidgetId);
            if (matchedGroup == null) return false;
            name = matchedGroup.Name;
            type = PermissionSignType.Group;
            return true;
        }

        return false;
    }

    // -------------------------------------------------------------------------
    // Permission check
    // -------------------------------------------------------------------------

    /// <summary>
    /// Evaluates all permission signs in the 3×3×3 chunk neighbourhood around
    /// the block position in <paramref name="args"/>. If any sign's draw area
    /// contains the block and its assigned player or group matches the acting
    /// player, permission is granted and the check short-circuits.
    /// </summary>
    private void OnPermission(PermissionArgs args)
    {
        int blockX = args.X;
        int blockY = args.Y;
        int blockZ = args.Z;

        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int cx = blockX / Server.chunksize + dx;
                    int cy = blockY / Server.chunksize + dy;
                    int cz = blockZ / Server.chunksize + dz;

                    if (!MapUtil.IsValidChunkPos(server.d_Map, cx, cy, cz, Server.chunksize)) continue;

                    ServerChunk chunk = server.d_Map.GetChunk_(cx, cy, cz);
                    if (chunk == null) return;

                    for (int i = 0; i < chunk.EntitiesCount; i++)
                    {
                        ServerEntity e = chunk.Entities[i];
                        if (e?.PermissionSign == null || e.DrawArea == null) continue;

                        if (!InArea(blockX, blockY, blockZ,
                                e.DrawArea.X, e.DrawArea.Z, e.DrawArea.Y,
                                e.DrawArea.SizeX, e.DrawArea.SizeZ, e.DrawArea.SizeY))
                            continue;

                        ClientOnServer client = server.clients[args.Player];

                        bool allowed = e.PermissionSign.Type switch
                        {
                            PermissionSignType.Group => e.PermissionSign.Name == client.clientGroup.Name,
                            PermissionSignType.Player => e.PermissionSign.Name == client.playername,
                            _ => false
                        };

                        if (allowed)
                        {
                            args.Allowed=(true);
                            return;
                        }
                    }
                }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Checks whether <paramref name="player"/> has the <c>area_add</c> privilege,
    /// sending an error message and returning <c>false</c> if not.
    /// </summary>
    private bool CheckAreaPrivilege(int player)
    {
        if (server.PlayerHasPrivilege(player, ServerClientMisc.Privilege.area_add))
            return true;

        server.SendMessage(player,
            server.colorError + server.language.Get("Server_CommandInsufficientPrivileges"));
        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if the point (<paramref name="x"/>, <paramref name="y"/>,
    /// <paramref name="z"/>) lies within the axis-aligned box defined by the given
    /// origin and size.
    /// </summary>
    private static bool InArea(int x, int y, int z,
        int areaX, int areaY, int areaZ,
        int areaSizeX, int areaSizeY, int areaSizeZ) =>
        x >= areaX && x < areaX + areaSizeX &&
        y >= areaY && y < areaY + areaSizeY &&
        z >= areaZ && z < areaZ + areaSizeZ;
}