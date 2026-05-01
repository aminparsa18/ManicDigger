public class PacketHandlerCraftingRecipes : ClientPacketHandler
{
    internal ModGuiCrafting mod;

    public PacketHandlerCraftingRecipes(IGameService gameService, IGame game) : base(gameService, game)
    {
    }

    public override void Handle(Packet_Server packet)
    {
        mod.d_CraftingRecipes = packet.CraftingRecipes.CraftingRecipes;
        mod.d_CraftingRecipesCount = packet.CraftingRecipes.CraftingRecipes.Length;
    }
}
