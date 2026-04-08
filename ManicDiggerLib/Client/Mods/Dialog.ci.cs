using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles rendering and input for in-game dialogs (normal and modal).
/// </summary>
public class ModDialog : ModBase
{
    private static readonly string[] Empty = [];
    private const string TypableChars = "abcdefghijklmnopqrstuvwxyz1234567890\t ";

    private readonly ClientPacketHandler packetHandler = new ClientPacketHandlerDialog();

    public override void OnNewFrameDraw2d(Game game, float deltaTime)
    {
        game.packetHandlers[Packet_ServerIdEnum.Dialog] = packetHandler;
        DrawDialogs(game);
    }

    internal static void DrawDialogs(Game game)
    {
        for (int i = 0; i < game.dialogsCount; i++)
        {
            VisibleDialog d = game.dialogs[i];
            if (d == null) continue;

            d.screen.screenx = game.Width() / 2 - d.value.Width / 2;
            d.screen.screeny = game.Height() / 2 - d.value.Height_ / 2;
            d.screen.DrawWidgets();
        }
    }

    public override void OnKeyPress(Game game, KeyPressEventArgs args)
    {
        if (game.guistate != GuiState.ModalDialog && game.guistate != GuiState.Normal) return;
        if (game.IsTyping) return;

        ForEachDialog(game, d => d.screen.OnKeyPress(game, args));

        for (int k = 0; k < game.dialogsCount; k++)
        {
            VisibleDialog d = game.dialogs[k];
            if (d == null) continue;

            for (int i = 0; i < d.value.WidgetsCount; i++)
            {
                Packet_Widget w = d.value.Widgets[i];
                if (w == null) continue;

                // Only typeable characters are handled here; special characters use KeyDown
                if (TypableChars.Contains(game.CharToString(w.ClickKey)) && args.GetKeyChar() == w.ClickKey)
                {
                    game.SendPacketClient(ClientPackets.DialogClick(w.Id, Empty, 0));
                    return;
                }
            }
        }
    }

    public override void OnKeyDown(Game game, KeyEventArgs args)
    {
        ForEachDialog(game, d => d.screen.OnKeyDown(game, args));

        bool isEsc = args.GetKeyCode() == game.GetKey(Keys.Escape);

        if (game.guistate == GuiState.Normal && isEsc)
        {
            for (int i = 0; i < game.dialogsCount; i++)
            {
                VisibleDialog d = game.dialogs[i];
                if (d == null) continue;
                if (d.value.IsModal != 0)
                {
                    game.dialogs[i] = null;
                    return;
                }
            }
            game.ShowEscapeMenu();
            args.SetHandled(true);
            return;
        }

        if (game.guistate == GuiState.ModalDialog)
        {
            if (isEsc)
            {
                // Close all modal dialogs
                for (int i = 0; i < game.dialogsCount; i++)
                {
                    if (game.dialogs[i]?.value.IsModal != 0)
                        game.dialogs[i] = null;
                }
                game.SendPacketClient(ClientPackets.DialogClick("Esc", Empty, 0));
                game.GuiStateBackToGame();
                args.SetHandled(true);
            }
            else if (args.GetKeyCode() == game.GetKey(Keys.Tab))
            {
                game.SendPacketClient(ClientPackets.DialogClick("Tab", Empty, 0));
                args.SetHandled(true);
            }
        }
    }

    public override void OnKeyUp(Game game, KeyEventArgs args) =>
        ForEachDialog(game, d => d.screen.OnKeyUp(game, args));

    public override void OnMouseDown(Game game, MouseEventArgs args) =>
        ForEachDialog(game, d => d.screen.OnMouseDown(game, args));

    public override void OnMouseUp(Game game, MouseEventArgs args) =>
        ForEachDialog(game, d => d.screen.OnMouseUp(game, args));

    /// <summary>Iterates all non-null dialogs and applies an action to each.</summary>
    private static void ForEachDialog(Game game, Action<VisibleDialog> action)
    {
        for (int i = 0; i < game.dialogsCount; i++)
        {
            if (game.dialogs[i] != null)
                action(game.dialogs[i]);
        }
    }
}

public class ClientPacketHandlerDialog : ClientPacketHandler
{
    public override void Handle(Game game, Packet_Server packet)
    {
        Packet_ServerDialog d = packet.Dialog;
        if (d.Dialog == null)
        {
            if (game.GetDialogId(d.DialogId) != -1 && game.dialogs[game.GetDialogId(d.DialogId)].value.IsModal != 0)
            {
                game.GuiStateBackToGame();
            }
            if (game.GetDialogId(d.DialogId) != -1)
            {
                game.dialogs[game.GetDialogId(d.DialogId)] = null;
            }
            if (game.DialogsCount_() == 0)
            {
                game.SetFreeMouse(false);
            }
        }
        else
        {
            VisibleDialog d2 = new()
            {
                key = d.DialogId,
                value = d.Dialog
            };
            d2.screen = ConvertDialog(game, d2.value);
            d2.screen.game = game;
            if (game.GetDialogId(d.DialogId) == -1)
            {
                for (int i = 0; i < game.dialogsCount; i++)
                {
                    if (game.dialogs[i] == null)
                    {
                        game.dialogs[i] = d2;
                        break;
                    }
                }
            }
            else
            {
                game.dialogs[game.GetDialogId(d.DialogId)] = d2;
            }
            if (d.Dialog.IsModal != 0)
            {
                game.guistate = GuiState.ModalDialog;
                game.SetFreeMouse(true);
            }
        }
    }

    private static GameScreen ConvertDialog(Game game, Packet_Dialog p)
    {
        DialogScreen s = new()
        {
            widgets = new MenuWidget[p.WidgetsCount],
            WidgetCount = p.WidgetsCount
        };
        for (int i = 0; i < p.WidgetsCount; i++)
        {
            Packet_Widget a = p.Widgets[i];
            MenuWidget b = new();
            if (a.Type == Packet_WidgetTypeEnum.Text)
            {
                b.type = WidgetType.Label;
            }
            if (a.Type == Packet_WidgetTypeEnum.Image)
            {
                b.type = WidgetType.Button;
            }
            if (a.Type == Packet_WidgetTypeEnum.TextBox)
            {
                b.type = WidgetType.Textbox;
            }
            b.x = a.X;
            b.y = a.Y;
            b.sizex = a.Width;
            b.sizey = a.Height_;
            b.text = a.Text;
            if (b.text != null)
            {
                b.text = b.text.Replace("!SERVER_IP!", game.ServerInfo.connectdata.Ip);
            }
            if (b.text != null)
            {
                b.text = b.text.Replace("!SERVER_PORT!", game.ServerInfo.connectdata.Port.ToString());
            }
            b.color = a.Color;
            if (a.Font != null)
            {
                b.font = new FontCi
                {
                    family = game.ValidFont(a.Font.FamilyName),
                    size = game.DeserializeFloat(a.Font.SizeFloat),
                    style = a.Font.FontStyle
                };
            }
            b.id = a.Id;
            b.isbutton = a.ClickKey != 0;
            if (a.Image == "Solid")
            {
                b.image = null;
            }
            else if (a.Image != null)
            {
                b.image = string.Concat(a.Image, ".png");
            }
            s.widgets[i] = b;
        }
        for (int i = 0; i < s.WidgetCount; i++)
        {
            if (s.widgets[i] == null) { continue; }
            if (s.widgets[i].type == WidgetType.Textbox)
            {
                s.widgets[i].editing = true;
                break;
            }
        }
        return s;
    }
}

public class DialogScreen : GameScreen
{
    public override void OnButton(MenuWidget w)
    {
        if (w.isbutton)
        {
            string[] textValues = new string[WidgetCount];
            for (int i = 0; i < WidgetCount; i++)
            {
                string s = widgets[i].text;
                if (s == null)
                {
                    s = "";
                }
                textValues[i] = s;
            }
            game.SendPacketClient(ClientPackets.DialogClick(w.id, textValues, WidgetCount));
        }
    }
}
