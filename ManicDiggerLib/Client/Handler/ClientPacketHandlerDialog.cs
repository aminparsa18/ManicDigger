using ManicDigger;

/// <summary>
/// Handles <see cref="Packet_ServerIdEnum.Dialog"/> packets,
/// opening, updating, or closing server-driven modal and non-modal dialogs.
/// </summary>
public class ClientPacketHandlerDialog : ClientPacketHandler
{
    public override void Handle(IGameClient game, Packet_Server packet)
    {
        Packet_ServerDialog d = packet.Dialog;

        // ── Cache the lookup result — previously called up to 4 times per path ─
        int dialogIdx = game.GetDialogId(d.DialogId);

        if (d.Dialog == null)
        {
            // Server is closing this dialog.
            if (dialogIdx != -1 && game.Dialogs[dialogIdx].value.IsModal)
                game.GuiStateBackToGame();

            if (dialogIdx != -1)
                game.Dialogs[dialogIdx] = null;

            if (game.Dialogs.Length == 0)
                game.SetFreeMouse(false);
        }
        else
        {
            // Server is opening or updating a dialog.
            VisibleDialog d2 = new()
            {
                key = d.DialogId,
                value = d.Dialog,
            };
            d2.screen = ConvertDialog(game, d2.value);
            d2.screen.SetGame(game);

            if (dialogIdx == -1)
            {
                // Find the first empty slot.
                for (int i = 0; i < game.Dialogs.Length; i++)
                {
                    if (game.Dialogs[i] == null)
                    {
                        game.Dialogs[i] = d2;
                        break;
                    }
                }
            }
            else
            {
                game.Dialogs[dialogIdx] = d2;
            }

            if (d.Dialog.IsModal)
            {
                game.GuiState = GuiState.ModalDialog;
                game.SetFreeMouse(true);
            }
        }
    }

    private GameScreen ConvertDialog(IGameClient game, Dialog p)
    {
        DialogScreen s = new(game, default)
        {
            widgets = new MenuWidget[p.Widgets.Length],
            WidgetCount = p.Widgets.Length,
        };

        for (int i = 0; i < p.Widgets.Length; i++)
        {
            Widget a = p.Widgets[i];
            MenuWidget b = new();

            // ── Single switch instead of four sequential if-blocks ────────────
            b.type = a.Type switch
            {
                WidgetType.Text => UIWidgetType.Label,
                WidgetType.Image => UIWidgetType.Button,
                WidgetType.TextBox => UIWidgetType.Textbox,
                _ => b.type,
            };

            b.x = a.X;
            b.y = a.Y;
            b.sizex = a.Width;
            b.sizey = a.Height;
            b.text = a.Text;

            // ── Single null-check, chained Replace calls ──────────────────────
            if (b.text != null)
            {
                b.text = b.text
                    .Replace("!SERVER_IP!", game.ServerInfo.ConnectData.Ip)
                    .Replace("!SERVER_PORT!", game.ServerInfo.ConnectData.Port.ToString());
            }

            b.color = a.Color;
            b.id = a.Id;
            b.isbutton = a.ClickKey != 0;

            if (a.Font != null)
            {
                b.font = new Font(
                    game.ValidFont(a.Font.FamilyName),
                    game.DecodeFixedPoint((int)a.Font.Size),
                    (FontStyle)a.Font.FontStyle);
            }

            b.image = a.Image switch
            {
                "Solid" => null,
                null => null,
                _ => string.Concat(a.Image, ".png"),
            };

            s.widgets[i] = b;
        }

        // Auto-focus the first textbox widget.
        for (int i = 0; i < s.WidgetCount; i++)
        {
            if (s.widgets[i]?.type == UIWidgetType.Textbox)
            {
                s.widgets[i].editing = true;
                break;
            }
        }

        return s;
    }
}