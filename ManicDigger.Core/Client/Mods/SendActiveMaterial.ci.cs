/// <summary>
/// Notifies the server when the player's active material slot changes.
/// </summary>
public class ModSendActiveMaterial : ModBase
{
    private int previousActiveMaterialBlock;
    private readonly IGame game;

    public ModSendActiveMaterial(IGame game)
    {
        this.game = game;
    }

    public override void OnNewFrameFixed(float args)
    {
        int activeBlock = game.Inventory.RightHand[game.ActiveMaterial]?.BlockId ?? 0;

        if (activeBlock != previousActiveMaterialBlock)
            game.SendPacketClient(ClientPackets.ActiveMaterialSlot(game.ActiveMaterial));

        previousActiveMaterialBlock = activeBlock;
    }
}