/// <summary>
/// A flat (2D) block map stored as a sparse array of fixed-size chunks.
/// Typically used for heightmap data that mirrors a <see cref="VoxelMap"/>.
/// <para>
/// <b>Note:</b> <see cref="ChunkSize"/> is hardcoded to 16 and must match
/// <see cref="ServerMapStorage.ChunkSize"/> if used alongside it.
/// </para>
/// </summary>
public class ChunkedMap2d
{
    /// <summary>The <see cref="Game"/> instance, used to read map dimensions on <see cref="Restart"/>.</summary>
    internal Game _game;

    /// <summary>Number of blocks along one axis of a chunk. Must match the 3D map's chunk size.</summary>
    public const int ChunkSize = 16;

    internal int[][] _chunks;

    /// <summary>Returns the block value at block-space coordinates (<paramref name="x"/>, <paramref name="y"/>),
    /// allocating the owning chunk if it does not yet exist.</summary>
    public int GetBlock(int x, int y)
    {
        int[] chunk = GetChunk(x, y);
        return chunk[VectorIndexUtil.Index2d(x % ChunkSize, y % ChunkSize, ChunkSize)];
    }

    /// <summary>
    /// Returns the chunk that contains block-space coordinates (<paramref name="x"/>, <paramref name="y"/>),
    /// allocating a zero-filled chunk if one does not yet exist.
    /// </summary>
    public int[] GetChunk(int x, int y)
    {
        int kx = x / ChunkSize;
        int ky = y / ChunkSize;
        int index = VectorIndexUtil.Index2d(kx, ky, _game.VoxelMap.MapSizeX / ChunkSize);

        if (_chunks[index] == null)
        {
            // int[] is zero-initialised by the runtime — no manual fill needed.
            _chunks[index] = new int[ChunkSize * ChunkSize];
        }

        return _chunks[index];
    }

    /// <summary>Sets the block value at block-space coordinates (<paramref name="x"/>, <paramref name="y"/>),
    /// allocating the owning chunk if it does not yet exist.</summary>
    public void SetBlock(int x, int y, int blocktype)
    {
        GetChunk(x, y)[VectorIndexUtil.Index2d(x % ChunkSize, y % ChunkSize, ChunkSize)] = blocktype;
    }

    /// <summary>
    /// Reinitialises the chunk array to match the current map dimensions, discarding all block data.
    /// Must be called whenever the map is resized.
    /// </summary>
    public void Restart()
    {
        int n = (_game.VoxelMap.MapSizeX / ChunkSize) * (_game.VoxelMap.MapSizeY / ChunkSize);
        // Array of reference types is null-initialised by the runtime — no manual fill needed.
        _chunks = new int[n][];
    }

    /// <summary>
    /// Discards the chunk that contains block-space coordinates (<paramref name="x"/>, <paramref name="y"/>),
    /// freeing its memory. The chunk will be reallocated on the next access.
    /// </summary>
    public void ClearChunk(int x, int y)
    {
        int px = x / ChunkSize;
        int py = y / ChunkSize;
        _chunks[VectorIndexUtil.Index2d(px, py, _game.VoxelMap.MapSizeX / ChunkSize)] = null;
    }
}