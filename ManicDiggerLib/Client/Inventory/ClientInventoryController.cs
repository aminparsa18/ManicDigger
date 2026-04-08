public class ClientInventoryController : IInventoryController
{
    public static ClientInventoryController Create(Game game)
    {
        ClientInventoryController c = new()
        {
            g = game
        };
        return c;
    }

    private Game g;

    public void InventoryClick(Packet_InventoryPosition pos)
    {
        g.InventoryClick(pos);
    }

    public void WearItem(Packet_InventoryPosition from, Packet_InventoryPosition to)
    {
        g.WearItem(from, to);
    }

    public void MoveToInventory(Packet_InventoryPosition from)
    {
        g.MoveToInventory(from);
    }
}
