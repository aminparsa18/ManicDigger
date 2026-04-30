using ManicDigger;

/// <summary>
/// Handles weapon reload timing and the R key reload trigger.
/// </summary>
public class ModReloadAmmo : ModBase
{
    private readonly IGameService platform;
    private readonly Random random;

    public ModReloadAmmo(IGameService platform)
    {
        this.platform = platform;
        random = new Random();
    }

    public override void OnNewFrameFixed(IGame game, float args)
    {
        if (game.ReloadStartMilliseconds == 0) return;

        float elapsed = (platform.TimeMillisecondsFromStart - game.ReloadStartMilliseconds) / 1000f;
        float reloadDelay = game.BlockTypes[game.ReloadBlock].ReloadDelay;
        if (elapsed <= reloadDelay) return;

        int blockId = game.ReloadBlock;
        game.LoadedAmmo[blockId] = Math.Min(game.BlockTypes[blockId].AmmoMagazine, game.TotalAmmo[blockId]);
        game.ReloadStartMilliseconds = 0;
        game.ReloadBlock = -1;
    }

    public override void OnKeyDown(IGame game, KeyEventArgs args)
    {
        if (game.GuiState != GuiState.Normal || game.GuiTyping != TypingState.None) return;
        if (args.KeyChar != game.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.R)) return;

        InventoryItem item = game.Inventory.RightHand[game.ActiveMaterial];
        if (item == null || item.InventoryItemType != InventoryItemType.Block) return;
        if (!game.BlockTypes[item.BlockId].IsPistol) return;
        if (game.ReloadStartMilliseconds != 0) return;

        var sounds = game.BlockTypes[item.BlockId].Sounds;
        int sound = random.Next() % sounds.Reload.Length;
        game.PlayAudio(sounds.Reload[sound] + ".ogg");
        game.ReloadStartMilliseconds = platform.TimeMillisecondsFromStart;
        game.ReloadBlock = item.BlockId;
        game.SendPacketClient(ClientPackets.Reload());
    }
}