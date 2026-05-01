public interface IMapStorage
{
    int MapSizeX { get; }
    int MapSizeY { get; }
    int MapSizeZ { get; }
    int GetBlock(int x, int y, int z);
    void SetBlock(int x, int y, int z, int tileType);
}

public class MapStorage(IVoxelMap voxelMap, Action<int, int, int, int> setBlock) : IMapStorage
{
    private readonly IVoxelMap _voxelMap = voxelMap;
    private readonly Action<int, int, int, int> _setBlock = setBlock;

    public int MapSizeX => _voxelMap.MapSizeX;
    public int MapSizeY => _voxelMap.MapSizeY;
    public int MapSizeZ => _voxelMap.MapSizeZ;

    public int GetBlock(int x, int y, int z) => _voxelMap.GetBlock(x, y, z);
    public void SetBlock(int x, int y, int z, int tileType) => _setBlock(x, y, z, tileType);
}
