using ManicDigger;

/// <summary>
/// Handles weapon reload timing and the R key reload trigger.
/// </summary>
public class ModReloadAmmo : ModBase
{
    public override void OnNewFrameFixed(Game game, float args)
    {
        if (game.reloadstartMilliseconds == 0) return;

        float elapsed = (game.Platform.TimeMillisecondsFromStart - game.reloadstartMilliseconds) / 1000f;
        float reloadDelay = game.DecodeFixedPoint(game.BlockTypes[game.reloadblock].ReloadDelayFloat);
        if (elapsed <= reloadDelay) return;

        int blockId = game.reloadblock;
        game.LoadedAmmo[blockId] = Math.Min(game.BlockTypes[blockId].AmmoMagazine, game.TotalAmmo[blockId]);
        game.reloadstartMilliseconds = 0;
        game.reloadblock = -1;
    }

    public override void OnKeyDown(Game game, KeyEventArgs args)
    {
        if (game.GuiState != GuiState.Normal || game.GuiTyping != TypingState.None) return;
        if (args.KeyChar != game.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.R)) return;

        Packet_Item item = game.d_Inventory.RightHand[game.ActiveMaterial];
        if (item == null || item.ItemClass != ItemClass.Block) return;
        if (!game.BlockTypes[item.BlockId].IsPistol) return;
        if (game.reloadstartMilliseconds != 0) return;

        var sounds = game.BlockTypes[item.BlockId].Sounds;
        int sound = game.rnd.Next() % sounds.Reload.Length;
        game.PlayAudio(sounds.Reload[sound] + ".ogg");
        game.reloadstartMilliseconds = game.Platform.TimeMillisecondsFromStart;
        game.reloadblock = item.BlockId;
        game.SendPacketClient(ClientPackets.Reload());
    }
}