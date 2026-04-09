public interface IMapStorage
{
    int MapSizeX { get; }
    int MapSizeY { get; }
    int MapSizeZ { get; }
    int GetBlock(int x, int y, int z);
    void SetBlock(int x, int y, int z, int tileType);
}

public class MapStorage : IMapStorage
{
    private readonly Game game;

    public MapStorage(Game game)
    {
        this.game = game;
    }

    public int MapSizeX => game.VoxelMap.MapSizeX;
    public int MapSizeY => game.VoxelMap.MapSizeY;
    public int MapSizeZ => game.VoxelMap.MapSizeZ;

    public int GetBlock(int x, int y, int z) => game.VoxelMap.GetBlock(x, y, z);
    public void SetBlock(int x, int y, int z, int tileType) => game.SetBlock(x, y, z, tileType);
}
