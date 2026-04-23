#region Using Statements
using OpenTK.Mathematics;
using ProtoBuf;
#endregion

[ProtoContract()]
public class Monster
{
    [ProtoMember(1, IsRequired = false)]
    public int Id;
    [ProtoMember(2, IsRequired = false)]
    public int MonsterType;
    [ProtoMember(3, IsRequired = false)]
    public int X;
    [ProtoMember(4, IsRequired = false)]
    public int Y;
    [ProtoMember(5, IsRequired = false)]
    public int Z;
    public int Health;
    public Vector3i WalkDirection;
    public float WalkProgress = 0;
}
[ProtoContract()]
public class ServerChunk
{
    [ProtoMember(1, IsRequired = false)]
    public byte[] dataOld;
    [ProtoMember(6, IsRequired = false)]
    public ushort[] data;
    [ProtoMember(2, IsRequired = false)]
    public long LastUpdate;
    [ProtoMember(3, IsRequired = false)]
    public bool IsPopulated;
    [ProtoMember(4, IsRequired = false)]
    public int LastChange;
    public bool DirtyForSaving;
    [ProtoMember(5, IsRequired = false)]
    public List<Monster> Monsters = new();
    [ProtoMember(7, IsRequired = false)]
    public int EntitiesCount;
    [ProtoMember(8, IsRequired = false)]
    public ServerEntity[] Entities;
}

public class ServerMapStorage : IMapStorage
{
    internal Server server;
    internal IChunkDb d_ChunkDb;
    internal ICurrentTime d_CurrentTime;
    internal ServerChunk[][] chunks;
    internal bool wasChunkGenerated;

    internal int mapSizeX;
    internal int mapSizeY;
    internal int mapSizeZ;
    public int MapSizeX => mapSizeX;
    public int MapSizeY => mapSizeY;
    public int MapSizeZ => mapSizeZ;

    public int GetBlock(int x, int y, int z)
    {
        ServerChunk chunk = GetChunk(x, y, z);
        unchecked
        {
            return chunk.data[VectorIndexUtil.Index3d(ModuloChunk(x), ModuloChunk(y), ModuloChunk(z), chunksize, chunksize)];
        }
    }

    public void SetBlock(int x, int y, int z, int tileType)
    {
        ServerChunk chunk = GetChunk(x, y, z);
        unchecked
        {
            chunk.data[VectorIndexUtil.Index3d(ModuloChunk(x), ModuloChunk(y), ModuloChunk(z), chunksize, chunksize)] = (ushort)tileType;
        }
        chunk.LastChange = d_CurrentTime.GetSimulationCurrentFrame();
        chunk.DirtyForSaving = true;
        UpdateColumnHeight(x, y);
    }

    private void UpdateColumnHeight(int x, int y)
    {
        //todo faster
        int height = mapSizeZ - 1;
        for (int i = mapSizeZ - 1; i >= 0; i--)
        {
            height = i;
            if (!Server.IsTransparentForLight(server.BlockTypes[GetBlock(x, y, i)]))
            {
                break;
            }
        }
        d_Heightmap.SetBlock(x, y, height);
    }

    public void SetBlockNotMakingDirty(int x, int y, int z, int tileType)
    {
        ServerChunk chunk = GetChunk(x, y, z);
        unchecked
        {
            chunk.data[VectorIndexUtil.Index3d(ModuloChunk(x), ModuloChunk(y), ModuloChunk(z), chunksize, chunksize)] = (ushort)tileType;
        }
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

    public ServerChunk GetChunk_(int chunkx, int chunky, int chunkz)
    {
        return GetChunk(chunkx * chunksize, chunky * chunksize, chunkz * chunksize);
    }

    public ServerChunk GetChunk(int x, int y, int z)
    {
        x = InvertChunk(x);
        y = InvertChunk(y);
        z = InvertChunk(z);
        ServerChunk chunk = GetChunkValid(x, y, z);
        if (chunk != null)
        {
            bool allZero = true;
            for (int i = 0; i < chunk.data?.Length; i++)
                if (chunk.data[i] != 0) { allZero = false; break; }
            Console.WriteLine($"[Chunk] ({x},{y},{z}) already existed, allZero={allZero}, IsPopulated={chunk.IsPopulated}");
        }
        if (chunk == null)
        {
            wasChunkGenerated = true;
            unchecked
            {
                byte[] serializedChunk = ChunkDb.GetChunk(d_ChunkDb, x, y, z);
                Console.WriteLine($"[DB] ({x},{y},{z}) key={MapUtil.ToMapPos(x, y, z)} got={serializedChunk != null} len={serializedChunk?.Length}");
                if (serializedChunk != null)
                {
                    SetChunkValid(x, y, z, DeserializeChunk(serializedChunk));
                    UpdateChunkHeight(x, y, z);
                    return GetChunkValid(x, y, z);
                }

                ushort[] newchunk = new ushort[chunksize * chunksize * chunksize];
                for (int i = 0; i < server.modEventHandlers.getchunk.Count; i++)
                {
                    server.modEventHandlers.getchunk[i](x, y, z, newchunk);
                }
                SetChunkValid(x, y, z, new ServerChunk() { data = newchunk });
                GetChunkValid(x, y, z).DirtyForSaving = true;
                UpdateChunkHeight(x, y, z);
                return GetChunkValid(x, y, z);
            }
        }
        return chunk;
    }

    private void UpdateChunkHeight(int x, int y, int z)
    {
        unchecked
        {
            for (int xx = 0; xx < chunksize; xx++)
            {
                for (int yy = 0; yy < chunksize; yy++)
                {
                    int inChunkHeight = GetColumnHeightInChunk(GetChunkValid(x, y, z).data, xx, yy);
                    if (inChunkHeight != 0)
                    {
                        int oldHeight = d_Heightmap.GetBlock(x * chunksize + xx, y * chunksize + yy);
                        d_Heightmap.SetBlock(x * chunksize + xx, y * chunksize + yy, Math.Max(oldHeight, inChunkHeight + z * chunksize));
                    }
                }
            }
        }
    }

    private int GetColumnHeightInChunk(ushort[] chunk, int xx, int yy)
    {
        int height = chunksize - 1;
        unchecked
        {
            for (int i = chunksize - 1; i >= 0; i--)
            {
                height = i;
                if (!Server.IsTransparentForLight(server.BlockTypes[chunk[VectorIndexUtil.Index3d(xx, yy, i, chunksize, chunksize)]]))
                {
                    break;
                }
            }
            return height;
        }
    }

    private ServerChunk DeserializeChunk(byte[] serializedChunk)
    {
        ServerChunk c = Serializer.Deserialize<ServerChunk>(new MemoryStream(serializedChunk));
        unchecked
        {
            if (c.dataOld != null)
            {
                c.data = new ushort[chunksize * chunksize * chunksize];
                for (int i = 0; i < c.dataOld.Length; i++)
                {
                    c.data[i] = c.dataOld[i];
                }
                c.dataOld = null;
            }
            if (c.Entities != null)
            {
                c.EntitiesCount = c.Entities.Length;
            }
            return c;
        }
    }

    private int chunksize = 16;
    private double invertedChunkSize = 1.0 / 16;
    private bool isPower2Chunk = true;
    public int ChunkSize
    {
        get { return chunksize; }
        set
        {
            chunksize = value;
            invertedChunkSize = 1.0 / chunksize;
            isPower2Chunk = (chunksize & (chunksize - 1)) == 0 && chunksize != 0;
        }
    }

    public int ModuloChunk(int num)
    {
        if (isPower2Chunk)
            return num & (chunksize - 1);
        else
            return num % chunksize;
    }

    public int InvertChunk(int num)
    {
        return (int)(num * invertedChunkSize);
    }

    public void Reset(int sizex, int sizey, int sizez)
    {
        mapSizeX = sizex;
        mapSizeY = sizey;
        mapSizeZ = sizez;
        chunks = new ServerChunk[InvertChunk(sizex) * InvertChunk(sizey)][];
        d_Heightmap.Restart();
    }

    public InfiniteMapChunked2dServer d_Heightmap;

    public ushort[] GetHeightmapChunk(int x, int y)
    {
        unchecked
        {
            // todo: avoid copy
            ushort[] source = d_Heightmap.GetChunk(x, y);
            ushort[] copy = new ushort[chunksize * chunksize];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }
    }

    public ServerChunk GetChunkValid(int cx, int cy, int cz)
    {
        unchecked
        {
            ServerChunk[] column = chunks[VectorIndexUtil.Index2d(cx, cy, InvertChunk(mapSizeX))];
            if (column == null)
            {
                return null;
            }
            return column[cz];
        }
    }

    public void SetChunkValid(int cx, int cy, int cz, ServerChunk chunk)
    {
        unchecked
        {
            ServerChunk[] column = chunks[VectorIndexUtil.Index2d(cx, cy, InvertChunk(mapSizeX))];
            if (column == null)
            {
                column = new ServerChunk[InvertChunk(mapSizeZ)];
                chunks[VectorIndexUtil.Index2d(cx, cy, InvertChunk(mapSizeX))] = column;
            }
            column[cz] = chunk;
        }
    }

    public void Clear()
    {
        Array.Clear(chunks, 0, chunks.Length);
    }
}