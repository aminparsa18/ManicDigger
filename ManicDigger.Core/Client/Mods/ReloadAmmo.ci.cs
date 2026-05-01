using ManicDigger;

/// <summary>
/// Handles weapon reload timing and the R key reload trigger.
/// </summary>
public class ModReloadAmmo : ModBase
{
    private readonly IGameService platform;
    private readonly Random random;

    public ModReloadAmmo(IGameService platform, IGame game) : base(game)
    {
        this.platform = platform;
        random = new Random();
    }

    public override void OnNewFrameFixed(float args)
    {
        if (Game.ReloadStartMilliseconds == 0) return;

        float elapsed = (platform.TimeMillisecondsFromStart - Game.ReloadStartMilliseconds) / 1000f;
        float reloadDelay = Game.BlockTypes[Game.ReloadBlock].ReloadDelay;
        if (elapsed <= reloadDelay) return;

        int blockId = Game.ReloadBlock;
        Game.LoadedAmmo[blockId] = Math.Min(Game.BlockTypes[blockId].AmmoMagazine, Game.TotalAmmo[blockId]);
        Game.ReloadStartMilliseconds = 0;
        Game.ReloadBlock = -1;
    }

    public override void OnKeyDown(KeyEventArgs args)
    {
        if (Game.GuiState != GuiState.Normal || Game.GuiTyping != TypingState.None) return;
        if (args.KeyChar != Game.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.R)) return;

        InventoryItem item = Game.Inventory.RightHand[Game.ActiveMaterial];
        if (item == null || item.InventoryItemType != InventoryItemType.Block) return;
        if (!Game.BlockTypes[item.BlockId].IsPistol) return;
        if (Game.ReloadStartMilliseconds != 0) return;

        var sounds = Game.BlockTypes[item.BlockId].Sounds;
        int sound = random.Next() % sounds.Reload.Length;
        Game.PlayAudio(sounds.Reload[sound] + ".ogg");
        Game.ReloadStartMilliseconds = platform.TimeMillisecondsFromStart;
        Game.ReloadBlock = item.BlockId;
        Game.SendPacketClient(ClientPackets.Reload());
    }
}