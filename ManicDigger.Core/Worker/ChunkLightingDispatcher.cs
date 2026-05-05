namespace ManicDigger.Worker;

using System.Buffers;

/// <summary>
/// IChunkWorkDispatcher implementation for the lighting stage.
/// Runs inside a ChunkWorkerPool configured with workerCount=1 so
/// BaseLightDirty and rendered.Light are never touched concurrently.
///
/// On each LightingChunkWorkItem: computes shadows, snapshots rendered.Light
/// into a rented buffer, then enqueues a TessellationChunkWorkItem to the
/// tessellation pool. The tessellation worker owns and returns the buffer.
/// </summary>
public sealed class ChunkLightingDispatcher : IChunkWorkDispatcher
{
    private const int BufferedChunkVolume = 18 * 18 * 18;

    private readonly IChunkWorkQueue _tessellationQueue;
    private readonly IVoxelMap _voxelMap;
    private readonly IBlockRegistry _blockRegistry;

    private readonly LightBase _lightBase;
    private readonly LightBetweenChunks _lightBetweenChunks;
    private readonly int[] _shadowLightRadius = new int[GameConstants.MAX_BLOCKTYPES];
    private readonly bool[] _shadowIsTransparent = new bool[GameConstants.MAX_BLOCKTYPES];
    private bool _blockTypeCacheDirty = true;
    private readonly int _chunkSize = GameConstants.CHUNK_SIZE;

    public ChunkLightingDispatcher(
        IChunkWorkQueue tessellationQueue,
        IVoxelMap voxelMap,
        ILightManager lightManager,
        IBlockRegistry blockRegistry)
    {
        _tessellationQueue = tessellationQueue;
        _voxelMap = voxelMap;
        _blockRegistry = blockRegistry;
        _lightBetweenChunks = new(voxelMap);
        _lightBase = new(voxelMap, lightManager);
    }

    public void InvalidateBlockTypeCache() => _blockTypeCacheDirty = true;

    // ── IChunkWorkDispatcher ──────────────────────────────────────────────────

    public async Task DispatchAsync(ChunkWorkItem item, CancellationToken ct)
    {
        if (item is not LightingChunkWorkItem li) return;

        byte[] snapshot = ComputeAndSnapshot(li.ChunkX, li.ChunkY, li.ChunkZ, li.Chunk);

        await _tessellationQueue.EnqueueAsync(new TessellationChunkWorkItem(
            li.ChunkX,
            li.ChunkY,
            li.ChunkZ,
            li.Chunk,
            ShadowBuffer: snapshot,
            ShadowBufferRented: true,
            Completion: li.Completion), ct);
    }

    // ── Shadow computation (single-threaded by the pool) ──────────────────────

    private byte[] ComputeAndSnapshot(int cx, int cy, int cz, Chunk target)
    {
        RefreshBlockTypeCache();

        bool anyBaseLightChanged = false;

        for (int xx = 0; xx < 3; xx++)
            for (int yy = 0; yy < 3; yy++)
                for (int zz = 0; zz < 3; zz++)
                {
                    int cx1 = cx + xx - 1;
                    int cy1 = cy + yy - 1;
                    int cz1 = cz + zz - 1;
                    if (!_voxelMap.IsValidChunkPos(cx1, cy1, cz1)) continue;

                    Chunk? neighbour = _voxelMap.Chunks[VectorIndexUtil.Index3d(
                        cx1, cy1, cz1,
                        _voxelMap.Mapsizexchunks,
                        _voxelMap.Mapsizeychunks)];
                    if (neighbour == null) continue;

                    if (!neighbour.BaseLightDirty) continue;

                    _lightBase.CalculateChunkBaseLight(
                        cx1, cy1, cz1, _shadowLightRadius, _shadowIsTransparent);
                    neighbour.BaseLightDirty = false;
                    anyBaseLightChanged = true;
                }

        RenderedChunk rendered = target.Rendered;

        if (rendered.Light == null)
        {
            rendered.Light = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
            rendered.LightRented = true;
            rendered.Light.AsSpan(0, BufferedChunkVolume).Fill(15);
            anyBaseLightChanged = true;
        }

        if (anyBaseLightChanged)
            _lightBetweenChunks.CalculateLightBetweenChunks(
                cx, cy, cz, _shadowLightRadius, _shadowIsTransparent);

        byte[] snapshot = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
        rendered.Light.AsSpan(0, BufferedChunkVolume)
                      .CopyTo(snapshot.AsSpan(0, BufferedChunkVolume));
        return snapshot;
    }

    private void RefreshBlockTypeCache()
    {
        if (!_blockTypeCacheDirty) return;
        foreach ((int id, BlockType blockType) in _blockRegistry.BlockTypes)
        {
            _shadowLightRadius[id] = blockType.LightRadius;
            _shadowIsTransparent[id] = IsTransparentForLight(id);
        }
        _blockTypeCacheDirty = false;
    }

    private bool IsTransparentForLight(int blockId)
    {
        BlockType b = _blockRegistry.BlockTypes[blockId];
        return b.DrawType is not DrawType.Solid and not DrawType.ClosedDoor;
    }
}

/// <summary>
/// Enqueued by the main thread (ModDrawTerrain) when a chunk is dirty.
/// The single ChunkLightingWorker converts this into a
/// TessellationChunkWorkItem after computing shadows.
/// </summary>
public record LightingChunkWorkItem(
    int ChunkX,
    int ChunkY,
    int ChunkZ,
    Chunk Chunk,
    TaskCompletionSource? Completion = null
) : ChunkWorkItem(ChunkX, ChunkY, ChunkZ, ChunkWorkType.RelightFull, Completion);