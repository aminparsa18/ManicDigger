public class ServerMapStorage : IMapStorage
{
    private ServerChunk[][] chunks;
    private int mapSizeX;
    private int mapSizeY;
    private int mapSizeZ;

    public Server server { get; set; }
    public IChunkDb d_ChunkDb { get; set; }
    public int MapSizeX => mapSizeX;
    public int MapSizeY => mapSizeY;
    public int MapSizeZ => mapSizeZ;

    public int GetBlock(int x, int y, int z)
    {
        ServerChunk chunk = GetChunk(x, y, z);
        return chunk.Data[VectorIndexUtil.Index3d(BlockInChunk(x), BlockInChunk(y), BlockInChunk(z), chunksize, chunksize)];
    }

    private void UpdateColumnHeight(int x, int y)
    {
        int bx = BlockInChunk(x);
        int by = BlockInChunk(y);
        int cx = BlockInChunk(x);
        int cy = BlockInChunk(y);

        for (int i = mapSizeZ - 1; i >= 0; i--)
        {
            int cz = BlockInChunk(i);
            int bz = BlockInChunk(i);
            ServerChunk chunk = GetChunkValid(cx, cy, cz);
            if (chunk?.Data == null)
            {
                continue;
            }

            if (!Game.IsTransparentForLight(server.BlockTypes[chunk.Data[VectorIndexUtil.Index3d(bx, by, bz, chunksize, chunksize)]]))
            {
                Heightmap.SetBlock(x, y, i);
                return;
            }
        }

        Heightmap.SetBlock(x, y, 0);
    }

    public void SetBlockNotMakingDirty(int x, int y, int z, int tileType)
    {
        ServerChunk chunk = GetChunk(x, y, z);
        chunk.Data[VectorIndexUtil.Index3d(BlockInChunk(x), BlockInChunk(y), BlockInChunk(z), chunksize, chunksize)] = (ushort)tileType;
        chunk.DirtyForSaving = true;
        UpdateColumnHeight(x, y);
    }

    public void LoadChunk(int cx, int cy, int cz)
    {
        ServerChunk chunk = GetChunkValid(cx, cy, cz);
        if (chunk == null)
        {
            GetChunk(cx * chunksize, cy * chunksize, cz * chunksize);
        }
    }

    public ServerChunk GetChunkAt(int chunkx, int chunky, int chunkz) => GetChunk(chunkx * chunksize, chunky * chunksize, chunkz * chunksize);

    public ServerChunk GetChunk(int x, int y, int z)
    {
        x = BlockToChunk(x);
        y = BlockToChunk(y);
        z = BlockToChunk(z);
        ServerChunk chunk = GetChunkValid(x, y, z);
        if (chunk != null)
        {
            return chunk;
        }

        byte[] serializedChunk = ChunkDbHelper.GetChunk(d_ChunkDb, x, y, z);

        if (serializedChunk != null)
        {
            ServerChunk deserialized = DeserializeChunk(serializedChunk);
            SetChunkValid(x, y, z, deserialized);
            UpdateChunkHeight(x, y, z);
            return GetChunkValid(x, y, z);
        }

        ushort[] newchunk = new ushort[chunksize * chunksize * chunksize];
        for (int i = 0; i < server.ModEventHandlers.getchunk.Count; i++)
        {
            server.ModEventHandlers.getchunk[i](x, y, z, newchunk);
        }

        SetChunkValid(x, y, z, new ServerChunk() { Data = newchunk });
        GetChunkValid(x, y, z).DirtyForSaving = true;
        UpdateChunkHeight(x, y, z);
        return GetChunkValid(x, y, z);

    }

    private void UpdateChunkHeight(int x, int y, int z)
    {
        for (int xx = 0; xx < chunksize; xx++)
        {
            for (int yy = 0; yy < chunksize; yy++)
            {
                int inChunkHeight = GetColumnHeightInChunk(GetChunkValid(x, y, z).Data, xx, yy);
                if (inChunkHeight != 0)
                {
                    int oldHeight = Heightmap.GetBlock((x * chunksize) + xx, (y * chunksize) + yy);
                    Heightmap.SetBlock((x * chunksize) + xx, (y * chunksize) + yy, Math.Max(oldHeight, inChunkHeight + (z * chunksize)));
                }
            }
        }
    }

    private int GetColumnHeightInChunk(ushort[] chunk, int xx, int yy)
    {
        int height = chunksize - 1;
        for (int i = chunksize - 1; i >= 0; i--)
        {
            height = i;
            if (!Game.IsTransparentForLight(server.BlockTypes[chunk[VectorIndexUtil.Index3d(xx, yy, i, chunksize, chunksize)]]))
            {
                break;
            }
        }

        return height;
    }

    private ServerChunk DeserializeChunk(byte[] serializedChunk) 
        => MemoryPackSerializer.Deserialize<ServerChunk>(serializedChunk);

    private int chunksize = 16;
    private int chunksizebits = 4; // log2(16)
    private bool isPower2Chunk = true;
    public int ChunkSize
    {
        get => chunksize;
        set
        {
            chunksize = value;
            isPower2Chunk = (chunksize & (chunksize - 1)) == 0 && chunksize != 0;
            chunksizebits = (int)Math.Log2(value);
        }
    }

    private int BlockInChunk(int num) => isPower2Chunk ? num & (chunksize - 1) : num % chunksize;

    private int BlockToChunk(int num) => num >> chunksizebits;

    public void Reset(int sizex, int sizey, int sizez)
    {
        mapSizeX = sizex;
        mapSizeY = sizey;
        mapSizeZ = sizez;
        chunks = new ServerChunk[BlockToChunk(sizex) * BlockToChunk(sizey)][];
        Heightmap.Restart();
    }

    public InfiniteMapChunked2dServer Heightmap { get; set; }

    public ServerChunk GetChunkValid(int cx, int cy, int cz)
    {
        ServerChunk[] column = chunks[VectorIndexUtil.Index2d(cx, cy, BlockToChunk(mapSizeX))];
        return column == null || (uint)cz >= (uint)column.Length ? null : column[cz];
    }

    public void SetChunkValid(int cx, int cy, int cz, ServerChunk chunk)
    {
        ServerChunk[] column = chunks[VectorIndexUtil.Index2d(cx, cy, BlockToChunk(mapSizeX))];
        if (column == null)
        {
            column = new ServerChunk[BlockToChunk(mapSizeZ)];
            chunks[VectorIndexUtil.Index2d(cx, cy, BlockToChunk(mapSizeX))] = column;
        }

        column[cz] = chunk;
    }

    public void Clear() => Array.Clear(chunks, 0, chunks.Length);
}