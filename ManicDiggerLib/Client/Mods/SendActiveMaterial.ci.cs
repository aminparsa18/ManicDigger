/// <summary>
/// Notifies the server when the player's active material slot changes.
/// </summary>
public class ModSendActiveMaterial : ModBase
{
    private int previousActiveMaterialBlock;

    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        int activeBlock = game.d_Inventory.RightHand[game.ActiveMaterial]?.BlockId ?? 0;

        if (activeBlock != previousActiveMaterialBlock)
            game.SendPacketClient(ClientPackets.ActiveMaterialSlot(game.ActiveMaterial));

        previousActiveMaterialBlock = activeBlock;
    }
}