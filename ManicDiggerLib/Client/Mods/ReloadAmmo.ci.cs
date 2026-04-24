using ManicDigger;

/// <summary>
/// Handles weapon reload timing and the R key reload trigger.
/// </summary>
public class ModReloadAmmo : ModBase
{
    private readonly IGameClient game;
    private readonly IGamePlatform platform;
    private readonly Random random;

    public ModReloadAmmo(IGameClient game, IGamePlatform platform)
    {
        this.game = game;
        this.platform = platform;
        random = new Random();
    }

    public override void OnNewFrameFixed(float args)
    {
        if (game.ReloadStartMilliseconds == 0) return;

        float elapsed = (platform.TimeMillisecondsFromStart - game.ReloadStartMilliseconds) / 1000f;
        float reloadDelay = game.DecodeFixedPoint(game.BlockTypes[game.ReloadBlock].ReloadDelayFloat);
        if (elapsed <= reloadDelay) return;

        int blockId = game.ReloadBlock;
        game.LoadedAmmo[blockId] = Math.Min(game.BlockTypes[blockId].AmmoMagazine, game.TotalAmmo[blockId]);
        game.ReloadStartMilliseconds = 0;
        game.ReloadBlock = -1;
    }

    public override void OnKeyDown(KeyEventArgs args)
    {
        if (game.GuiState != GuiState.Normal || game.GuiTyping != TypingState.None) return;
        if (args.KeyChar != game.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.R)) return;

        Packet_Item item = game.Inventory.RightHand[game.ActiveMaterial];
        if (item == null || item.ItemClass != ItemClass.Block) return;
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