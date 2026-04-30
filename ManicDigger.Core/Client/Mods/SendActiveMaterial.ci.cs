/// <summary>
/// Notifies the server when the player's active material slot changes.
/// </summary>
public class ModSendActiveMaterial : ModBase
{
    private int previousActiveMaterialBlock;

    public ModSendActiveMaterial()
    {
    }

    public override void OnNewFrameFixed(IGame game, float args)
    {
        int activeBlock = game.Inventory.RightHand[game.ActiveMaterial]?.BlockId ?? 0;

        if (activeBlock != previousActiveMaterialBlock)
            game.SendPacketClient(ClientPackets.ActiveMaterialSlot(game.ActiveMaterial));

        previousActiveMaterialBlock = activeBlock;
    }
}