using ManicDigger;

/// <summary>
/// Handles weapon reload timing and the R key reload trigger.
/// </summary>
public class ModReloadAmmo : ModBase
{
    private readonly IGameClient game;

    public ModReloadAmmo(IGameClient game)
    {
        this.game = game;
    }

    public override void OnNewFrameFixed(float args)
    {
        if (game.ReloadStartMilliseconds == 0) return;

        float elapsed = (game.Platform.TimeMillisecondsFromStart - game.ReloadStartMilliseconds) / 1000f;
        float reloadDelay = game.DecodeFixedPoint(game.BlockTypes[game.ReloadBlock].ReloadDelayFloat);
        if (elapsed <= reloadDelay) return;

        int blockId = game.ReloadBlock;
        game.LoadedAmmo[blockId] = Math.Min(game.BlockTypes[blockId].AmmoMagazine, game.TotalAmmo[blockId]);
        game.ReloadStartMilliseconds = 0;
        game.ReloadBlock = -1;
    }

    public override void OnKeyDown(Game game, KeyEventArgs args)
    {
        if (game.GuiState != GuiState.Normal || game.GuiTyping != TypingState.None) return;
        if (args.KeyChar != game.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.R)) return;

        Packet_Item item = game.Inventory.RightHand[game.ActiveMaterial];
        if (item == null || item.ItemClass != ItemClass.Block) return;
        if (!game.BlockTypes[item.BlockId].IsPistol) return;
        if (game.ReloadStartMilliseconds != 0) return;

        var sounds = game.BlockTypes[item.BlockId].Sounds;
        int sound = game.rnd.Next() % sounds.Reload.Length;
        game.PlayAudio(sounds.Reload[sound] + ".ogg");
        game.ReloadStartMilliseconds = game.Platform.TimeMillisecondsFromStart;
        game.ReloadBlock = item.BlockId;
        game.SendPacketClient(ClientPackets.Reload());
    }
}