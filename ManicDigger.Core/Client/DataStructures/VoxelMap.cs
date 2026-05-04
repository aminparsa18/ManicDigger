using System.Buffers;

///
public class VoxelMap : IVoxelMap
{
    public Chunk[] Chunks { get; private set; }
    public readonly System.Collections.Concurrent.ConcurrentQueue<int> PhantomChunkIndices = new();

    public int MapSizeX { get; set; }
    public int MapSizeY { get; set; }
    public int MapSizeZ { get; set; }

    /// <inheritdoc/>
    public int GetBlockValid(int x, int y, int z)
    {
        int cx = x >> GameConstants.chunksizebits;
        int cy = y >> GameConstants.chunksizebits;
        int cz = z >> GameConstants.chunksizebits;
        int chunkpos = VectorIndexUtil.Index3d(cx, cy, cz, MapSizeX >> GameConstants.chunksizebits, MapSizeY >> GameConstants.chunksizebits);
        if (Chunks[chunkpos] == null)
        {
            return 0;
        }
        else
        {
            int pos = VectorIndexUtil.Index3d(x & (GameConstants.CHUNK_SIZE - 1), y & (GameConstants.CHUNK_SIZE - 1), z & (GameConstants.CHUNK_SIZE - 1), GameConstants.CHUNK_SIZE, GameConstants.CHUNK_SIZE);
            return Chunks[chunkpos].GetBlock(pos);
        }
    }

    /// <inheritdoc/>
    public Chunk GetChunk(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0)
        {
            Console.WriteLine($"[WARN] GetChunk negative input: ({x}, {y}, {z})");
        }

        x >>= GameConstants.chunksizebits;
        y >>= GameConstants.chunksizebits;
        z >>= GameConstants.chunksizebits;
        return GetChunkAt(x, y, z);
    }

    /// <inheritdoc/>
    public Chunk GetChunkAt(int cx, int cy, int cz)
    {
        int csBits = GameConstants.chunksizebits;
        int mapsizexchunks = MapSizeX >> csBits;
        int mapsizeychunks = MapSizeY >> csBits;
        int flatIndex = VectorIndexUtil.Index3d(cx, cy, cz, mapsizexchunks, mapsizeychunks);
        Chunk chunk = Chunks[flatIndex];

        if (chunk == null)
        {
            int n = GameConstants.CHUNK_SIZE * GameConstants.CHUNK_SIZE * GameConstants.CHUNK_SIZE;

            // Rent from the shared pool; Rent(n) may return a larger array — always use n as the
            // logical size. Clear before use because the pool may return dirty memory.
            byte[] data = ArrayPool<byte>.Shared.Rent(n);
            byte[] baseLight = ArrayPool<byte>.Shared.Rent(n);
            data.AsSpan(0, n).Clear();
            baseLight.AsSpan(0, n).Clear();

            chunk = new Chunk
            {
                Data = data,
                BaseLight = baseLight
            };
            Chunks[flatIndex] = chunk;
            PhantomChunkIndices.Enqueue(flatIndex);
        }

        return chunk;
    }

    /// <inheritdoc/>
    public void SetBlockRaw(int x, int y, int z, int tileType)
    {
        Chunk chunk = GetChunk(x, y, z);
        int pos = VectorIndexUtil.Index3d(x & (GameConstants.CHUNK_SIZE - 1), y & (GameConstants.CHUNK_SIZE - 1), z & (GameConstants.CHUNK_SIZE - 1), GameConstants.CHUNK_SIZE, GameConstants.CHUNK_SIZE);
        chunk.SetBlock(pos, tileType);
    }

    /// <inheritdoc/>
    public void Reset(int sizex, int sizey, int sizez)
    {
        // Release pooled arrays from any existing chunks before discarding the array.
        if (Chunks != null)
        {
            for (int i = 0; i < Chunks.Length; i++)
            {
                Chunks[i]?.Release();
                Chunks[i] = null;
            }
        }

        MapSizeX = sizex;
        MapSizeY = sizey;
        MapSizeZ = sizez;
        Chunks = new Chunk[sizex / GameConstants.CHUNK_SIZE * (sizey / GameConstants.CHUNK_SIZE) * (sizez / GameConstants.CHUNK_SIZE)];
    }

    /// <inheritdoc/>
    public void GetMapPortion(int[] outPortion, int x, int y, int z, int portionsizex, int portionsizey, int portionsizez)
    {
        int csBits = GameConstants.chunksizebits;
        int cs = GameConstants.CHUNK_SIZE;
        int mapchunksx = MapSizeX >> csBits;
        int mapchunksy = MapSizeY >> csBits;
        int mapsizechunks = mapchunksx * mapchunksy * (MapSizeZ >> csBits);

        Array.Clear(outPortion, 0, portionsizex * portionsizey * portionsizez);

        int startCX = x >> csBits;
        int startCY = y >> csBits;
        int startCZ = z >> csBits;
        int endCX = (x + portionsizex - 1) >> csBits;
        int endCY = (y + portionsizey - 1) >> csBits;
        int endCZ = (z + portionsizez - 1) >> csBits;

        for (int cx = startCX; cx <= endCX; cx++)
        {
            for (int cy = startCY; cy <= endCY; cy++)
            {
                for (int cz = startCZ; cz <= endCZ; cz++)
                {
                    int cpos = (((cz * mapchunksy) + cy) * mapchunksx) + cx;
                    if ((uint)cpos >= (uint)mapsizechunks)
                    {
                        continue;
                    }

                    Chunk chunk = Chunks[cpos];
                    if (chunk == null || !chunk.HasData())
                    {
                        continue;
                    }

                    // block range this chunk contributes to the output
                    int chunkGlobalX = cx << csBits;
                    int chunkGlobalY = cy << csBits;
                    int chunkGlobalZ = cz << csBits;

                    int blockX0 = Math.Max(x, chunkGlobalX);
                    int blockY0 = Math.Max(y, chunkGlobalY);
                    int blockZ0 = Math.Max(z, chunkGlobalZ);
                    int blockX1 = Math.Min(x + portionsizex, chunkGlobalX + cs);
                    int blockY1 = Math.Min(y + portionsizey, chunkGlobalY + cs);
                    int blockZ1 = Math.Min(z + portionsizez, chunkGlobalZ + cs);

                    for (int bx = blockX0; bx < blockX1; bx++)
                    {
                        int inChunkX = bx - chunkGlobalX;
                        int outX = bx - x;
                        for (int by = blockY0; by < blockY1; by++)
                        {
                            int inChunkY = by - chunkGlobalY;
                            int outY = by - y;
                            for (int bz = blockZ0; bz < blockZ1; bz++)
                            {
                                int inChunkZ = bz - chunkGlobalZ;
                                int outZ = bz - z;

                                int pos = (((inChunkZ << csBits) + inChunkY) << csBits) + inChunkX;
                                int block = chunk.GetBlock(pos);
                                outPortion[(((outZ * portionsizey) + outY) * portionsizex) + outX] = block;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public bool IsValidPos(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0)
        {
            return false;
        }

        if (x >= MapSizeX || y >= MapSizeY || z >= MapSizeZ)
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public bool IsValidChunkPos(int cx, int cy, int cz)
    {
        return cx >= 0 && cy >= 0 && cz >= 0
            && cx < MapSizeX / GameConstants.CHUNK_SIZE
            && cy < MapSizeY / GameConstants.CHUNK_SIZE
            && cz < MapSizeZ / GameConstants.CHUNK_SIZE;
    }

    /// <inheritdoc/>
    public int GetBlock(int x, int y, int z)
    {
        if (!IsValidPos(x, y, z))
        {
            return 0;
        }

        return GetBlockValid(x, y, z);
    }

    /// <inheritdoc/>
    public void SetChunkDirty(int cx, int cy, int cz, bool dirty, bool blockschanged)
    {
        if (!IsValidChunkPos(cx, cy, cz))
        {
            return;
        }

        Chunk c = Chunks[VectorIndexUtil.Index3d(cx, cy, cz, Mapsizexchunks, Mapsizeychunks)];
        if (c == null)
        {
            return;
        }

        c.Rendered ??= new RenderedChunk();
        c.Rendered.Dirty = dirty;
        if (blockschanged)
        {
            c.BaseLightDirty = true;
        }
    }

    public int Mapsizexchunks => MapSizeX >> GameConstants.chunksizebits;

    public int Mapsizeychunks => MapSizeY >> GameConstants.chunksizebits;

    public int Mapsizezchunks => MapSizeZ >> GameConstants.chunksizebits;

    /// <inheritdoc/>
    public void SetChunksAroundDirty(int cx, int cy, int cz)
    {
        if (IsValidChunkPos(cx - 1, cy, cz))
        {
            SetChunkDirty(cx - 1, cy, cz, true, false);
        }

        if (IsValidChunkPos(cx + 1, cy, cz))
        {
            SetChunkDirty(cx + 1, cy, cz, true, false);
        }

        if (IsValidChunkPos(cx, cy - 1, cz))
        {
            SetChunkDirty(cx, cy - 1, cz, true, false);
        }

        if (IsValidChunkPos(cx, cy + 1, cz))
        {
            SetChunkDirty(cx, cy + 1, cz, true, false);
        }

        if (IsValidChunkPos(cx, cy, cz - 1))
        {
            SetChunkDirty(cx, cy, cz - 1, true, false);
        }

        if (IsValidChunkPos(cx, cy, cz + 1))
        {
            SetChunkDirty(cx, cy, cz + 1, true, false);
        }
    }

    /// <inheritdoc/>
    public void SetMapPortion(int x, int y, int z, int[] chunk, int sizeX, int sizeY, int sizeZ)
    {
        int cs = GameConstants.CHUNK_SIZE;
        int csBits = GameConstants.chunksizebits;
        int chunksX = sizeX >> csBits;
        int chunksY = sizeY >> csBits;
        int chunksZ = sizeZ >> csBits;
        Chunk[] localchunks = new Chunk[chunksX * chunksY * chunksZ];
        for (int cx = 0; cx < chunksX; cx++)
        {
            int worldX = x + (cx << csBits);
            int srcX = cx << csBits;
            for (int cy = 0; cy < chunksY; cy++)
            {
                int worldY = y + (cy << csBits);
                int srcY = cy << csBits;
                for (int cz = 0; cz < chunksZ; cz++)
                {
                    int worldZ = z + (cz << csBits);
                    int srcZ = cz << csBits;

                    int idx = VectorIndexUtil.Index3d(cx, cy, cz, chunksX, chunksY); // computed once
                    Chunk c = GetChunk(worldX, worldY, worldZ);
                    localchunks[idx] = c;

                    FillChunk(c, cs, srcX, srcY, srcZ, chunk, sizeX, sizeY, sizeZ);

                    int ccx = worldX >> csBits;
                    int ccy = worldY >> csBits;
                    int ccz = worldZ >> csBits;
                    SetChunkDirty(ccx, ccy, ccz, true, true);
                    SetChunksAroundDirty(ccx, ccy, ccz);
                }
            }
        }
    }

    /// <summary>
    /// Copies a sub-cube of <paramref name="source"/> (offset by the given source coordinates)
    /// into <paramref name="destination"/>, filling every block position in the destination chunk.
    /// </summary>
    private static void FillChunk(Chunk destination, int dcs, int srcX, int srcY, int srcZ, int[] source, int srcSizeX, int srcSizeY, int srcSizeZ)
    {
        int csBits = GameConstants.chunksizebits;

        for (int z = 0; z < dcs; z++)
        {
            int srcZRow = (z + srcZ) * srcSizeY;
            int dstZRow = z << csBits;
            for (int y = 0; y < dcs; y++)
            {
                int srcBase = ((srcZRow + (y + srcY)) * srcSizeX) + srcX;
                int dstBase = (dstZRow + y) << csBits;
                for (int x = 0; x < dcs; x++)
                {
                    destination.SetBlock(dstBase + x, source[srcBase + x]);
                }
            }
        }
    }

    /// <inheritdoc/>
    public int MaybeGetLight(int x, int y, int z)
    {
        if (!IsValidPos(x, y, z))
        {
            return -1;
        }

        int csBits = GameConstants.chunksizebits;
        int csMask = GameConstants.CHUNK_SIZE - 1;
        int lightCS = GameConstants.CHUNK_SIZE + 2; // light array stride with padding

        int cx = x >> csBits;
        int cy = y >> csBits;
        int cz = z >> csBits;

        if (!IsValidChunkPos(cx, cy, cz))
        {
            return -1;
        }

        Chunk c = Chunks[VectorIndexUtil.Index3d(cx, cy, cz, Mapsizexchunks, Mapsizeychunks)];
        if (c?.Rendered?.Light == null)
        {
            return -1;
        }

        int lx = (x & csMask) + 1;
        int ly = (y & csMask) + 1;
        int lz = (z & csMask) + 1;

        return c.Rendered.Light[VectorIndexUtil.Index3d(lx, ly, lz, lightCS, lightCS)];
    }

    /// <inheritdoc/>
    public void SetBlockDirty(int x, int y, int z)
    {
        int csBits = GameConstants.chunksizebits;

        // center + 6 neighbors
        Span<(int x, int y, int z)> offsets =
        [
        (x, y, z),
        (x - 1, y, z),
        (x + 1, y, z),
        (x, y - 1, z),
        (x, y + 1, z),
        (x, y, z - 1),
        (x, y, z + 1),
        ];

        for (int i = 0; i < offsets.Length; i++)
        {
            (int x, int y, int z) p = offsets[i];

            if ((uint)p.x >= (uint)MapSizeX ||
                (uint)p.y >= (uint)MapSizeY ||
                (uint)p.z >= (uint)MapSizeZ)
            {
                continue;
            }

            SetChunkDirty(
                p.x >> csBits,
                p.y >> csBits,
                p.z >> csBits,
                true,
                true);
        }
    }

    /// <inheritdoc/>
    public bool IsChunkRendered(int cx, int cy, int cz)
    {
        Chunk c = Chunks[VectorIndexUtil.Index3d(cx, cy, cz, Mapsizexchunks, Mapsizeychunks)];
        if (c == null)
        {
            return false;
        }

        return c.Rendered != null && c.Rendered.Ids != null;
    }
}