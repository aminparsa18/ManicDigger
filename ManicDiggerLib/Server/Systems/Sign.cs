using ManicDigger;

public class ServerSystemSign : ServerSystem
{
    private Server server;

    protected override void Initialize(Server server)
    {
        this.server = server;
        server.modManager.RegisterOnBlockUseWithTool(OnUseWithTool);
        server.modEventHandlers.onupdateentity.Add(UpdateEntity);
        server.modEventHandlers.onuseentity.Add(OnUseEntity);
        server.modEventHandlers.ondialogclick2.Add(OnDialogClick);
    }

    private void OnUseWithTool(int player, int x, int y, int z, int tool)
    {
        if (server.modManager.GetBlockName(tool) != "Sign") return;
        if (server.d_Map.GetChunk(x, y, z) == null) return;
        if (!server.CheckBuildPrivileges(player, x, y, z, PacketBlockSetMode.Create)) return;

        var e = new ServerEntity
        {
            Position = new ServerEntityPositionAndOrientation
            {
                X = x + One / 2,
                Y = z,
                Z = y + One / 2
            },
            Sign = new ServerEntitySign { Text = "Hello world!" }
        };

        e.Position.Heading = EntityHeading.GetHeading(
            server.modManager.GetPlayerPositionX(player),
            server.modManager.GetPlayerPositionY(player),
            e.Position.X,
            e.Position.Z
        );

        server.AddEntity(x, y, z, e);
    }

    private void UpdateEntity(int chunkx, int chunky, int chunkz, int id)
    {
        ServerEntity e = server.GetEntity(chunkx, chunky, chunkz, id);
        if (e.Sign == null) return;

        e.DrawModel ??= new ServerEntityAnimatedModel();
        e.DrawModel.Model = "signmodel.txt";
        e.DrawModel.Texture = "signmodel.png";
        e.DrawModel.ModelHeight = One * 13 / 10;

        e.DrawText ??= new ServerEntityDrawText();
        e.DrawText.Text = e.Sign.Text;
        e.DrawText.Dx = One * 3 / 32;
        e.DrawText.Dy = One * 36 / 32;
        e.DrawText.Dz = One * 3 / 32;

        e.Usable = true;
        e.DrawName ??= new ServerEntityDrawName
        {
            Name = "Sign",
            OnlyWhenSelected = true
        };
    }

    private void OnUseEntity(int player, int chunkx, int chunky, int chunkz, int id)
    {
        ServerEntity e = server.GetEntity(chunkx, chunky, chunkz, id);
        if (e.Sign == null) return;
        if (!server.CheckBuildPrivileges(player, (int)e.Position.X, (int)e.Position.Z, (int)e.Position.Y, PacketBlockSetMode.Use)) return;

        var font = new DialogFont("Verdana", 11f, DialogFontStyle.Bold);

        Widget okButton = Widget.MakeSolid(100, 100, 100, 50, ColorUtils.ColorFromArgb(255, 100, 100, 100));
        okButton.ClickKey = (char)13;
        okButton.Id = "UseSign_OK";

        var d = new Dialog
        {
            Width = 400,
            Height = 200,
            IsModal = true,
            Widgets =
            [
            Widget.MakeSolid(0, 0, 300, 200, ColorUtils.ColorFromArgb(255, 50, 50, 50)),
            Widget.MakeTextBox(e.Sign.Text, font, 50, 50, 200, 50, ColorUtils.ColorFromArgb(255, 0, 0, 0)),
            okButton,
            Widget.MakeText("OK", font, 100, 100, ColorUtils.ColorFromArgb(255, 0, 0, 0))
            ]
        };

        server.clients[player].editingSign = new ServerEntityId
        {
            chunkx = chunkx,
            chunky = chunky,
            chunkz = chunkz,
            id = id
        };

        server.SendDialog(player, "UseSign", d);
    }

    private void OnDialogClick(DialogClickArgs args)
    {
        if (args.WidgetId != "UseSign_OK") return;

        var client = server.clients[args.Player];
        string newText = args.TextBoxValue[1];
        ServerEntityId id = client.editingSign;

        client.editingSign = null;

        if (newText != "")
        {
            ServerEntity e = server.GetEntity(id.chunkx, id.chunky, id.chunkz, id.id);
            e.Sign.Text = newText;
            server.SetEntityDirty(id);
        }
        else
        {
            server.DespawnEntity(id);
        }

        server.SendDialog(args.Player, "UseSign", null);
    }
}