using ManicDigger;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles rendering and input for in-game dialogs (normal and modal).
/// </summary>
public class ModDialog : ModBase
{
    private static readonly string[] Empty = [];
    private const string TypableChars = "abcdefghijklmnopqrstuvwxyz1234567890\t ";

    private readonly ClientPacketHandler packetHandler;
    private readonly IGameService platform;

    public ModDialog(IGameService platform)
    {
        this.platform = platform;
        this.packetHandler = new ClientPacketHandlerDialog(platform);
    }

    public override void OnNewFrameDraw2d(IGame game, float deltaTime)
    {
        game.PacketHandlers[(int)Packet_ServerIdEnum.Dialog] = packetHandler;
        DrawDialogs(game);
    }

    internal void DrawDialogs(IGame game)
    {
        for (int i = 0; i < game.Dialogs.Length; i++)
        {
            VisibleDialog d = game.Dialogs[i];
            if (d == null) continue;

            d.screen.screenx = platform.CanvasWidth / 2 - d.value.Width / 2;
            d.screen.screeny = platform.CanvasHeight / 2 - d.value.Height / 2;
            d.screen.DrawWidgets(game);
        }
    }

    public override void OnKeyPress(IGame game, KeyPressEventArgs args)
    {
        if (game.GuiState != GuiState.ModalDialog && game.GuiState != GuiState.Normal) return;
        if (game.IsTyping) return;

        ForEachDialog(game, d => d.screen.OnKeyPress(game, args));

        for (int k = 0; k < game.Dialogs.Length; k++)
        {
            VisibleDialog d = game.Dialogs[k];
            if (d == null) continue;

            for (int i = 0; i < d.value.Widgets.Length; i++)
            {
                Widget w = d.value.Widgets[i];
                if (w == null) continue;

                // Only typeable characters are handled here; special characters use KeyDown
                if (TypableChars.Contains((char)w.ClickKey) && args.KeyChar == w.ClickKey)
                {
                    game.SendPacketClient(ClientPackets.DialogClick(w.Id, Empty, 0));
                    return;
                }
            }
        }
    }

    public override void OnKeyDown(IGame game, KeyEventArgs args)
    {
        ForEachDialog(game, d => d.screen.OnKeyDown(game, args));

        bool isEsc = args.KeyChar == (int)Keys.Escape;

        if (game.GuiState == GuiState.Normal && isEsc)
        {
            for (int i = 0; i < game.Dialogs.Length; i++)
            {
                VisibleDialog d = game.Dialogs[i];
                if (d == null) continue;
                if (d.value.IsModal)
                {
                    game.Dialogs[i] = null;
                    return;
                }
            }
            game.ShowEscapeMenu();
            args.Handled=true;
            return;
        }

        if (game.GuiState == GuiState.ModalDialog)
        {
            if (isEsc)
            {
                // Close all modal dialogs
                for (int i = 0; i < game.Dialogs.Length; i++)
                {
                    if (game.Dialogs[i]?.value.IsModal == true)
                        game.Dialogs[i] = null;
                }
                game.SendPacketClient(ClientPackets.DialogClick("Esc", Empty, 0));
                game.GuiStateBackToGame();
                args.Handled=true;
            }
            else if (args.KeyChar == game.GetKey(Keys.Tab))
            {
                game.SendPacketClient(ClientPackets.DialogClick("Tab", Empty, 0));
                args.Handled=true;
            }
        }
    }

    public override void OnKeyUp(IGame game, KeyEventArgs args) =>
        ForEachDialog(game, d => d.screen.OnKeyUp(game, args));

    public override void OnMouseDown(IGame game, MouseEventArgs args) =>
        ForEachDialog(game, d => d.screen.OnMouseDown(game, args));
    public override void OnMouseUp(IGame game, MouseEventArgs args) =>
        ForEachDialog(game, d => d.screen.OnMouseUp(game, args));

    /// <summary>Iterates all non-null dialogs and applies an action to each.</summary>
    private static void ForEachDialog(IGame game, Action<VisibleDialog> action)
    {
        for (int i = 0; i < game.Dialogs.Length; i++)
        {
            if (game.Dialogs[i] != null)
                action(game.Dialogs[i]);
        }
    }
}