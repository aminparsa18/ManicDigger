#region Using Statements
using ManicDigger;
using OpenTK.Mathematics;
using static ManicDigger.Mods.ModNetworkProcess;
#endregion

/// <summary>
/// Represents a single monster entity in the world.
/// Persisted as part of the <see cref="ServerChunk"/> it occupies.
/// </summary>
[MemoryPackable]
public partial class Monster
{
    /// <summary>Unique monster ID, assigned by the server at spawn time.</summary>
    public int Id { get; set; }

    /// <summary>Monster type index, used to look up behaviour and appearance.</summary>
    public int MonsterType { get; set; }

    /// <summary>World X position in blocks.</summary>
    public int X { get; set; }

    /// <summary>World Y position in blocks.</summary>
    public int Y { get; set; }

    /// <summary>World Z position in blocks.</summary>
    public int Z { get; set; }

    /// <summary>Current health points. Not persisted — reset on world load.</summary>
    [MemoryPackIgnore]
    public int Health { get; set; }

    /// <summary>Current movement direction. Not persisted — recalculated each tick.</summary>
    [MemoryPackIgnore]
    public Vector3i WalkDirection { get; set; }

    /// <summary>
    /// Fractional progress [0, 1] through the current movement step.
    /// Not persisted — reset on world load.
    /// </summary>
    [MemoryPackIgnore]
    public float WalkProgress { get; set; }
}

/// <summary>
/// A 32³ (or 16³) block volume stored as a flat <see cref="ushort"/> array,
/// along with any monsters and entities that currently occupy it.
/// Persisted to the chunk database and loaded on demand.
/// </summary>
[MemoryPackable]
public partial class ServerChunk
{
    /// <summary>
    /// Legacy block data stored as <see langword="byte[]"/> from older save formats.
    /// When non-null on load, its contents are migrated into <see cref="Data"/>
    /// and this field is cleared.
    /// </summary>
    public byte[]? DataOld { get; set; }

    /// <summary>
    /// Block type IDs for every position in the chunk, stored in XYZ order.
    /// Index = <c>x + y * chunksize + z * chunksize * chunksize</c>.
    /// </summary>
    public ushort[]? Data { get; set; }

    /// <summary>Simulation frame on which this chunk was last modified by the world generator or a player.</summary>
    public long LastUpdate { get; set; }

    /// <summary>When <see langword="true"/>, this chunk has been fully generated and populated with terrain.</summary>
    public bool IsPopulated { get; set; }

    /// <summary>Simulation frame of the most recent block change within this chunk.</summary>
    public int LastChange { get; set; }

    /// <summary>
    /// When <see langword="true"/>, this chunk has unsaved changes and must be
    /// written to the database on the next save pass.
    /// Not persisted — always resets to <see langword="false"/> on load.
    /// </summary>
    [MemoryPackIgnore]
    public bool DirtyForSaving { get; set; }

    /// <summary>Monsters currently residing in this chunk.</summary>
    public List<Monster> Monsters { get; set; } = [];

    /// <summary>Number of valid entries in <see cref="Entities"/>.</summary>
    public int EntitiesCount { get; set; }

    /// <summary>Server entities (signs, push zones, interactive objects) located in this chunk.</summary>
    public ServerEntity[]? Entities { get; set; }
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
            return chunk.Data[VectorIndexUtil.Index3d(ModuloChunk(x), ModuloChunk(y), ModuloChunk(z), chunksize, chunksize)];
        }
    }

    public void SetBlock(int x, int y, int z, int tileType)
    {
        ServerChunk chunk = GetChunk(x, y, z);
        unchecked
        {
            chunk.Data[VectorIndexUtil.Index3d(ModuloChunk(x), ModuloChunk(y), ModuloChunk(z), chunksize, chunksize)] = (ushort)tileType;
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
            chunk.Data[VectorIndexUtil.Index3d(ModuloChunk(x), ModuloChunk(y), ModuloChunk(z), chunksize, chunksize)] = (ushort)tileType;
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
            int existingNonZero = 0;
            if (chunk.Data != null)
                for (int i = 0; i < chunk.Data.Length; i++)
                    if (chunk.Data[i] != 0) existingNonZero++;
            DiagLog.Write($"[Chunk] ({x},{y},{z}) already existed: dataLen={chunk.Data?.Length ?? -1} nonZero={existingNonZero} IsPopulated={chunk.IsPopulated}");
        }
        if (chunk == null)
        {
            wasChunkGenerated = true;
            unchecked
            {
                byte[] serializedChunk = ChunkDb.GetChunk(d_ChunkDb, x, y, z);
                DiagLog.Write($"[DB] ({x},{y},{z}) got={serializedChunk != null} len={serializedChunk?.Length ?? -1}");

                if (serializedChunk != null)
                {
                    ServerChunk deserialized;
                    try
                    {
                        deserialized = DeserializeChunk(serializedChunk);
                        int deserNonZero = 0;
                        if (deserialized.Data != null)
                            for (int i = 0; i < deserialized.Data.Length; i++)
                                if (deserialized.Data[i] != 0) deserNonZero++;
                        DiagLog.Write($"[Deserialize] ({x},{y},{z}) data={deserialized.Data?.Length ?? -1} dataOld={deserialized.DataOld?.Length ?? -1} nonZero={deserNonZero}");
                    }
                    catch (Exception ex)
                    {
                        DiagLog.Write($"[Deserialize] ({x},{y},{z}) EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                        return null;
                    }

                    SetChunkValid(x, y, z, deserialized);
                    UpdateChunkHeight(x, y, z);
                    return GetChunkValid(x, y, z);
                }

                DiagLog.Write($"[Generate] ({x},{y},{z}) not in DB, generators={server.modEventHandlers.getchunk.Count}");
                ushort[] newchunk = new ushort[chunksize * chunksize * chunksize];
                for (int i = 0; i < server.modEventHandlers.getchunk.Count; i++)
                    server.modEventHandlers.getchunk[i](x, y, z, newchunk);

                int genNonZero = 0;
                for (int i = 0; i < newchunk.Length; i++)
                    if (newchunk[i] != 0) genNonZero++;
                DiagLog.Write($"[Generate] ({x},{y},{z}) done, nonZero={genNonZero}");

                SetChunkValid(x, y, z, new ServerChunk() { Data = newchunk });
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
                    int inChunkHeight = GetColumnHeightInChunk(GetChunkValid(x, y, z).Data, xx, yy);
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
        ServerChunk c = MemoryPackSerializer.Deserialize<ServerChunk>(serializedChunk);
        unchecked
        {
            if (c.DataOld != null)
            {
                c.Data = new ushort[chunksize * chunksize * chunksize];
                for (int i = 0; i < c.DataOld.Length; i++)
                {
                    c.Data[i] = c.DataOld[i];
                }
                c.DataOld = null;
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