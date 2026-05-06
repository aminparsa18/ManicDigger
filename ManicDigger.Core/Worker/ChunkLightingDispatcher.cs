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
    private readonly ThreadLocal<LightingThreadContext> _context;

    public ChunkLightingDispatcher(
        IChunkWorkQueue tessellationQueue,
        IVoxelMap voxelMap,
        IBlockRegistry blockRegistry)
    {
        _tessellationQueue = tessellationQueue;
        _voxelMap = voxelMap;
        _blockRegistry = blockRegistry;
        _context = new ThreadLocal<LightingThreadContext>(
            () => new LightingThreadContext(voxelMap),
            trackAllValues: false);
    }

    public void InvalidateBlockTypeCache()
    {
        // Each thread context has its own cache — mark all dirty.
        // ThreadLocal doesn't expose all values when trackAllValues=false,
        // so we use a shared volatile flag that each context checks on next use.
        Volatile.Write(ref _globalCacheVersion, _globalCacheVersion + 1);
    }

    private volatile int _globalCacheVersion;

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

        LightingThreadContext ctx = _context.Value;
        ctx.RefreshCacheIfNeeded(_blockRegistry, _globalCacheVersion);

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
                    if (neighbour == null) continue;

                    // Atomic claim — only one worker computes BaseLight for this neighbour.
                    if (!neighbour.TryClaimBaseLightDirty()) continue;

#if DEBUG
                    int neighbourIndex = _voxelMap.ChunkFlatIndex(cx1, cy1, cz1);
                    BaseLightRaceDetector.BeginWrite(neighbourIndex, "LightBase");
#endif

                    ctx.LightBase.CalculateChunkBaseLight(
                        cx1, cy1, cz1,
                        ctx.ShadowLightRadius,
                        ctx.ShadowIsTransparent);

#if DEBUG
                    BaseLightRaceDetector.EndWrite(neighbourIndex);
#endif

                    anyBaseLightChanged = true;
                }

        await SnapshotAndEnqueue(li.ChunkX, li.ChunkY, li.ChunkZ,
            chunk, ctx, anyBaseLightChanged, li.Completion, ct);
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

        if (chunk.Rendered.Light == null)
        {
            chunk.Rendered.Light = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
            chunk.Rendered.LightRented = true;
            chunk.Rendered.Light.AsSpan(0, BufferedChunkVolume).Fill(15);
        }

        LightingThreadContext ctx = _context.Value;
        ctx.LightBetweenChunks.RefreshRenderedLight(rb.ChunkX, rb.ChunkY, rb.ChunkZ);

        byte[] snapshot = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
        chunk.Rendered.Light.AsSpan(0, BufferedChunkVolume)
                            .CopyTo(snapshot.AsSpan(0, BufferedChunkVolume));

        await _tessellationQueue.EnqueueAsync(new TessellationChunkWorkItem(
            rb.ChunkX, rb.ChunkY, rb.ChunkZ, chunk,
            ShadowBuffer: snapshot,
            ShadowBufferRented: true,
            Completion: rb.Completion), ct);
    }

    // ── Shared: snapshot Rendered.Light and enqueue tessellation ─────────────

    private async Task SnapshotAndEnqueue(
         int cx, int cy, int cz,
         Chunk chunk,
         LightingThreadContext ctx,
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
            ctx.LightBetweenChunks.CalculateLightBetweenChunks(
                cx, cy, cz,
                ctx.ShadowLightRadius,
                ctx.ShadowIsTransparent);
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

// ── Per-thread context ────────────────────────────────────────────────────────

/// <summary>
/// All mutable state needed by one lighting worker thread.
/// Allocated once per thread via ThreadLocal — no sharing, no locks.
/// </summary>
internal sealed class LightingThreadContext
{
    public readonly LightBase LightBase;
    public readonly LightBetweenChunks LightBetweenChunks;
    public readonly int[] ShadowLightRadius = new int[GameConstants.MAX_BLOCKTYPES];
    public readonly bool[] ShadowIsTransparent = new bool[GameConstants.MAX_BLOCKTYPES];

    private int _knownCacheVersion = -1;

    public LightingThreadContext(IVoxelMap voxelMap)
    {
        LightBase = new LightBase(voxelMap);
        LightBetweenChunks = new LightBetweenChunks(voxelMap);
    }

    /// <summary>
    /// Rebuilds the block-type lookup arrays if the global cache version has
    /// advanced since this thread last refreshed.
    /// </summary>
    public void RefreshCacheIfNeeded(IBlockRegistry blockRegistry, int globalVersion)
    {
        if (_knownCacheVersion == globalVersion) return;

        foreach ((int id, BlockType blockType) in blockRegistry.BlockTypes)
        {
            ShadowLightRadius[id] = blockType.LightRadius;
            ShadowIsTransparent[id] = blockType.DrawType
                is not DrawType.Solid
                and not DrawType.ClosedDoor;
        }

        _knownCacheVersion = globalVersion;
    }
}