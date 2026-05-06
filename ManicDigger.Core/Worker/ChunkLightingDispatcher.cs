namespace ManicDigger.Worker;

using System.Buffers;

/// <summary>
/// IChunkWorkDispatcher for the lighting stage (single-worker pool).
///
/// Handles two work item types:
///   LightingChunkWorkItem         — full relight (LightBase + LightBetweenChunks)
///                                   used for chunk load and sunlight-affecting changes
///   RelightBetweenChunksWorkItem  — partial relight (LightBetweenChunks only)
///                                   used after IncrementalLightBFS has already updated BaseLight
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

    public ChunkLightingDispatcher(
        IChunkWorkQueue tessellationQueue,
        IVoxelMap voxelMap,
        IBlockRegistry blockRegistry)
    {
        _tessellationQueue = tessellationQueue;
        _voxelMap = voxelMap;
        _blockRegistry = blockRegistry;
        _lightBetweenChunks = new(voxelMap);
        _lightBase = new(voxelMap);
    }

    public void InvalidateBlockTypeCache() => _blockTypeCacheDirty = true;

    // ── IChunkWorkDispatcher ──────────────────────────────────────────────────

    public async Task DispatchAsync(ChunkWorkItem item, CancellationToken ct)
    {
        switch (item)
        {
            case LightingChunkWorkItem li:
                await HandleFullRelight(li, ct);
                break;

            case RelightBetweenChunksWorkItem rb:
                await HandleRelightBetweenChunks(rb, ct);
                break;
        }
    }

    // ── Full relight (LightBase + LightBetweenChunks) ────────────────────────

    private async Task HandleFullRelight(LightingChunkWorkItem li, CancellationToken ct)
    {
        Chunk? chunk = li.Chunk ?? _voxelMap.GetChunkAt(li.ChunkX, li.ChunkY, li.ChunkZ);
        if (chunk == null) return;

        chunk.Rendered ??= new RenderedChunk();

        RefreshBlockTypeCache();

        // Recompute BaseLight for any dirty neighbour in the 3×3×3 neighbourhood.
        bool anyBaseLightChanged = false;
        for (int xx = 0; xx < 3; xx++)
            for (int yy = 0; yy < 3; yy++)
                for (int zz = 0; zz < 3; zz++)
                {
                    int cx1 = li.ChunkX + xx - 1;
                    int cy1 = li.ChunkY + yy - 1;
                    int cz1 = li.ChunkZ + zz - 1;
                    if (!_voxelMap.IsValidChunkPos(cx1, cy1, cz1)) continue;

                    Chunk? neighbour = _voxelMap.Chunks[
                        _voxelMap.ChunkFlatIndex(cx1, cy1, cz1)];
                    if (neighbour == null || !neighbour.BaseLightDirty) continue;

                    _lightBase.CalculateChunkBaseLight(
                        cx1, cy1, cz1, _shadowLightRadius, _shadowIsTransparent);
                    neighbour.BaseLightDirty = false;
                    anyBaseLightChanged = true;
                }

        await SnapshotAndEnqueue(li.ChunkX, li.ChunkY, li.ChunkZ,
            chunk, anyBaseLightChanged, li.Completion, ct);
    }

    // ── Partial relight (LightBetweenChunks only) ────────────────────────────

    /// <summary>
    /// Called after IncrementalLightBFS has already updated BaseLight.
    /// Skips LightBase entirely — just re-runs LightBetweenChunks to refresh
    /// Rendered.Light, then hands off a tessellation snapshot.
    /// </summary>
    private async Task HandleRelightBetweenChunks(RelightBetweenChunksWorkItem rb, CancellationToken ct)
    {
        Chunk? chunk = rb.Chunk ?? _voxelMap.GetChunkAt(rb.ChunkX, rb.ChunkY, rb.ChunkZ);
        if (chunk == null) return;

        chunk.Rendered ??= new RenderedChunk();

        RefreshBlockTypeCache();

        // BaseLight is already correct — run LightBetweenChunks only.
        await SnapshotAndEnqueue(rb.ChunkX, rb.ChunkY, rb.ChunkZ,
            chunk, anyBaseLightChanged: true, rb.Completion, ct);
    }

    // ── Shared: snapshot Rendered.Light and enqueue tessellation ─────────────

    private async Task SnapshotAndEnqueue(
        int cx, int cy, int cz,
        Chunk chunk,
        bool anyBaseLightChanged,
        TaskCompletionSource? completion,
        CancellationToken ct)
    {
        RenderedChunk rendered = chunk.Rendered;

        if (rendered.Light == null)
        {
            rendered.Light = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
            rendered.LightRented = true;
            rendered.Light.AsSpan(0, BufferedChunkVolume).Fill(15);
            anyBaseLightChanged = true;
        }

        if (anyBaseLightChanged)
        {
            _lightBetweenChunks.CalculateLightBetweenChunks(
                cx, cy, cz, _shadowLightRadius, _shadowIsTransparent);
        }

        byte[] snapshot = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
        rendered.Light.AsSpan(0, BufferedChunkVolume)
                      .CopyTo(snapshot.AsSpan(0, BufferedChunkVolume));

        await _tessellationQueue.EnqueueAsync(new TessellationChunkWorkItem(
            cx, cy, cz, chunk,
            ShadowBuffer: snapshot,
            ShadowBufferRented: true,
            Completion: completion), ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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


/// <summary>
/// Partial relight — LightBetweenChunks only (BaseLight already updated by IncrementalLightBFS).
/// Used for runtime block changes that do not affect the sunlight heightmap.
/// </summary>
public record RelightBetweenChunksWorkItem(
    int ChunkX,
    int ChunkY,
    int ChunkZ,
    Chunk? Chunk = null,
    TaskCompletionSource? Completion = null
) : ChunkWorkItem(ChunkX, ChunkY, ChunkZ, ChunkWorkType.RelightBetweenChunks, Completion);