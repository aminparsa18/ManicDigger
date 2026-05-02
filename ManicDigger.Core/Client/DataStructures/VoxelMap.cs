using System.Buffers;

/// <summary>
/// Represents the voxel world, storing block data in a sparse grid of <see cref="Chunk"/> objects.
/// Block-space coordinates are in individual block units; chunk coordinates are derived by
/// dividing by <see cref="GameConstants.CHUNK_SIZE"/> (or right-shifting by <see cref="GameConstants.chunksizebits"/>).
/// </summary>
public class VoxelMap : IVoxelMap
{
    public Chunk[] Chunks { get; private set; }
    public readonly System.Collections.Concurrent.ConcurrentQueue<int> PhantomChunkIndices = new();

    public int MapSizeX { get; set; }
    public int MapSizeY { get; set; }
    public int MapSizeZ { get; set; }

    /// <summary>
    /// Converts 3D coordinates into a flat array index using the layout: <c>(h * sizeY + y) * sizeX + x</c>.
    /// Used for both block-space and chunk-space indexing depending on the size arguments passed.
    /// </summary>
    private static int Index3d(int x, int y, int h, int sizex, int sizey) => (((h * sizey) + y) * sizex) + x;

    /// <summary>
    /// Returns the block type at the given block-space position without performing map-bounds validation.
    /// Returns 0 (air) if the owning chunk has not been allocated yet.
    /// Use <see cref="GetBlock"/> for safe access that also checks bounds.
    /// </summary>
    public int GetBlockValid(int x, int y, int z)
    {
        int cx = x >> GameConstants.chunksizebits;
        int cy = y >> GameConstants.chunksizebits;
        int cz = z >> GameConstants.chunksizebits;
        int chunkpos = Index3d(cx, cy, cz, MapSizeX >> GameConstants.chunksizebits, MapSizeY >> GameConstants.chunksizebits);
        if (Chunks[chunkpos] == null)
        {
            return 0;
        }
        else
        {
            int pos = Index3d(x & (GameConstants.CHUNK_SIZE - 1), y & (GameConstants.CHUNK_SIZE - 1), z & (GameConstants.CHUNK_SIZE - 1), GameConstants.CHUNK_SIZE, GameConstants.CHUNK_SIZE);
            return Chunks[chunkpos].GetBlock(pos);
        }
    }

    /// <summary>
    /// Returns the chunk that contains the block at block-space coordinates,
    /// allocating a new chunk if one does not already exist.
    /// </summary>
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

    /// <summary>
    /// Returns the chunk at the given chunk-space coordinates, allocating a new one if absent.
    /// Backing arrays are rented from <see cref="ArrayPool{T}.Shared"/> and zeroed before use
    /// so the caller always receives clean, initialised memory.
    /// </summary>
    public Chunk GetChunkAt(int cx, int cy, int cz)
    {
        int csBits = GameConstants.chunksizebits;
        int mapsizexchunks = MapSizeX >> csBits;
        int mapsizeychunks = MapSizeY >> csBits;
        int flatIndex = Index3d(cx, cy, cz, mapsizexchunks, mapsizeychunks);
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
                data = data,
                baseLight = baseLight
            };
            Chunks[flatIndex] = chunk;
            PhantomChunkIndices.Enqueue(flatIndex);
        }

        return chunk;
    }

    /// <summary>
    /// Writes a block type directly into the map without marking any chunks dirty.
    /// Prefer this during bulk operations such as world generation to avoid redundant re-render triggers.
    /// For interactive single-block edits, dirty-marking must be handled separately via <see cref="SetBlockDirty"/>.
    /// </summary>
    public void SetBlockRaw(int x, int y, int z, int tileType)
    {
        Chunk chunk = GetChunk(x, y, z);
        int pos = Index3d(x & (GameConstants.CHUNK_SIZE - 1), y & (GameConstants.CHUNK_SIZE - 1), z & (GameConstants.CHUNK_SIZE - 1), GameConstants.CHUNK_SIZE, GameConstants.CHUNK_SIZE);
        chunk.SetBlock(pos, tileType);
    }

    /// <summary>
    /// Reinitialises the map with new dimensions, discarding all existing chunk data.
    /// All sizes must be exact multiples of <see cref="GameConstants.CHUNK_SIZE"/>.
    /// </summary>
    /// <remarks>
    /// Every live chunk's pooled arrays are returned to <see cref="ArrayPool{T}.Shared"/>
    /// before the chunk array is replaced, preventing pool leaks on world reload.
    /// </remarks>
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

    /// <summary>
    /// Reads a rectangular sub-region of the map into a flat array.
    /// Unallocated or out-of-bounds positions are written as 0 (air).
    /// Output array layout: <c>index = (z * sizeY + y) * sizeX + x</c>.
    /// </summary>
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

    /// <summary>Returns true when block-space coordinates fall within map bounds.</summary>
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

    /// <summary>Returns true when chunk-space coordinates fall within map bounds.</summary>
    public bool IsValidChunkPos(int cx, int cy, int cz)
    {
        return cx >= 0 && cy >= 0 && cz >= 0
            && cx < MapSizeX / GameConstants.CHUNK_SIZE
            && cy < MapSizeY / GameConstants.CHUNK_SIZE
            && cz < MapSizeZ / GameConstants.CHUNK_SIZE;
    }

    /// <summary>
    /// Returns the block type at the given block-space position,
    /// or 0 (air) when the position is outside map bounds.
    /// </summary>
    public int GetBlock(int x, int y, int z)
    {
        if (!IsValidPos(x, y, z))
        {
            return 0;
        }

        return GetBlockValid(x, y, z);
    }

    /// <summary>
    /// Marks the chunk at chunk-space coordinates as dirty, optionally flagging that its block
    /// data has changed (which also invalidates base lighting via <see cref="Chunk.baseLightDirty"/>).
    /// Does nothing if the position is out of range or the chunk is null.
    /// </summary>
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

        c.rendered ??= new RenderedChunk();
        c.rendered.Dirty = dirty;
        if (blockschanged)
        {
            c.baseLightDirty = true;
        }
    }

    /// <summary>Width of the map measured in chunks.</summary>
    public int Mapsizexchunks => MapSizeX >> GameConstants.chunksizebits;

    /// <summary>Depth of the map measured in chunks.</summary>
    public int Mapsizeychunks => MapSizeY >> GameConstants.chunksizebits;

    /// <summary>Height of the map measured in chunks.</summary>
    public int Mapsizezchunks => MapSizeZ >> GameConstants.chunksizebits;

    /// <summary>
    /// Marks the six face-adjacent neighbour chunks of the chunk at the given chunk-space coordinates
    /// as dirty for re-rendering (without invalidating lighting).
    /// </summary>
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

    /// <summary>
    /// Writes a rectangular block of data into the map and marks all affected chunks
    /// (and their neighbours) dirty for re-rendering.
    /// Input array layout must match <see cref="GetMapPortion"/>.
    /// </summary>
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

                    int idx = Index3d(cx, cy, cz, chunksX, chunksY); // computed once
                    var c = GetChunk(worldX, worldY, worldZ);
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

    /// <summary>
    /// Attempts to read the baked light value at a block-space position.
    /// Returns -1 if the position is invalid, the chunk is unallocated,
    /// or the chunk's light buffer has not been computed yet.
    /// </summary>
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
        if (c?.rendered?.Light == null)
        {
            return -1;
        }

        int lx = (x & csMask) + 1;
        int ly = (y & csMask) + 1;
        int lz = (z & csMask) + 1;

        return c.rendered.Light[VectorIndexUtil.Index3d(lx, ly, lz, lightCS, lightCS)];
    }

    /// <summary>
    /// Marks the chunk containing the given block and all chunks containing its 6 face-adjacent
    /// neighbours as dirty with light invalidation. Call after any single-block edit.
    /// </summary>
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
            var p = offsets[i];

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

    /// <summary>
    /// Returns true when the chunk has been rendered at least once
    /// (i.e. its render mesh exists and contains vertex/index data).
    /// </summary>
    public bool IsChunkRendered(int cx, int cy, int cz)
    {
        Chunk c = Chunks[VectorIndexUtil.Index3d(cx, cy, cz, Mapsizexchunks, Mapsizeychunks)];
        if (c == null)
        {
            return false;
        }

        return c.rendered != null && c.rendered.Ids != null;
    }
}