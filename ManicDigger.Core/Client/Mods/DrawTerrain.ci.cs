using ManicDigger;
using OpenTK.Mathematics;
using System.Buffers;

/// <summary>
/// Client-side mod responsible for tessellating, lighting, and drawing the voxel terrain.
/// Runs chunk redraw logic on a background thread and commits GPU uploads on the main thread.
/// </summary>
public class ModDrawTerrain : ModBase
{
    /// <summary>Maximum light level used throughout the lighting system.</summary>
    public const int MaxLight = 15;

    /// <summary>
    /// Half of √3, used to compute the bounding-sphere radius of a chunk
    /// (√3 / 2 × chunkSize is the circumradius of a cube).
    /// </summary>
    private float _sqrt3Half;

    /// <summary>Sentinel value meaning "no chunk found".</summary>
    private const int NoChunk = -1;

    /// <summary>
    /// Size of the extended (buffered) chunk buffer in one axis.
    /// A 16³ chunk needs one block of overlap on each side → 18.
    /// </summary>
    private const int BufferedChunkEdge = 18;

    /// <summary>Volume of the extended chunk buffer (<see cref="BufferedChunkEdge"/>³).</summary>
    private const int BufferedChunkVolume = BufferedChunkEdge * BufferedChunkEdge * BufferedChunkEdge;

    private readonly IGameService _platform;
    private readonly IVoxelMap _voxelMap;
    private readonly IMeshBatcher meshBatcher;
    private readonly ITaskScheduler taskScheduler;
    private readonly IBlockRegistry _blockTypeRegistry;

    private readonly LightBase _lightBase;
    private readonly LightBetweenChunks _lightBetweenChunks;

    private bool _terrainStarted;

    internal int chunksize;
    internal int bufferedChunkSize;
    internal float invertedChunkSize;

    private readonly TerrainRendererRedraw[] _redrawQueue;
    private int _redrawQueueCount;

    private readonly int[] _currentChunk;
    private readonly byte[] _currentChunkShadows;
    private readonly int[] _batcherIds;
    private int _batcherIdsCount;

    private readonly int[] _shadowLightRadius;
    private readonly bool[] _shadowIsTransparent;
    private bool _blockTypeCacheDirty = true;

    private int _chunkUpdates;
    private int _lastPerfUpdateMs;
    private int _lastChunkUpdatesSnapshot;

    private readonly Vector3i[] _blocksAround7Buffer = new Vector3i[7];

    public ModDrawTerrain(IGameService platform, IVoxelMap voxelMap, IMeshBatcher meshBatcher,
        IBlockRegistry blockRegistry, ITaskScheduler taskScheduler, IGame game) : base(game)
    {
        _platform = platform;
        _voxelMap = voxelMap;
        _blockTypeRegistry = blockRegistry;
        this.meshBatcher = meshBatcher;
        this.taskScheduler = taskScheduler;
        _currentChunk = new int[BufferedChunkVolume];
        _currentChunkShadows = new byte[BufferedChunkVolume];
        _batcherIds = new int[1024];
        _redrawQueue = new TerrainRendererRedraw[128];
        _shadowLightRadius = new int[GameConstants.MAX_BLOCKTYPES];
        _shadowIsTransparent = new bool[GameConstants.MAX_BLOCKTYPES];
        _lightBase = new LightBase(voxelMap);
        _lightBetweenChunks = new LightBetweenChunks(voxelMap);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public int TrianglesCount() => meshBatcher.TotalTriangleCount();

    internal int InvertChunk(int num) => (int)(num * invertedChunkSize);

    // ── ModBase overrides ─────────────────────────────────────────────────────

    public override void OnNewFrameDraw3d(float _)
    {
        if (Game.ShouldRedrawAllBlocks)
        {
            Game.ShouldRedrawAllBlocks = false;
            RedrawAllBlocks(Game);
        }

        DrawTerrain(Game);
        UpdatePerformanceInfo(Game);
    }

    public override void OnReadOnlyBackgroundThread(float dt)
    {
        UpdateTerrain(Game);
        taskScheduler.Enqueue(MainThreadCommit);
    }

    //public override void Dispose() => Clear();

    // ── Initialisation ────────────────────────────────────────────────────────

    public void StartTerrain(IGame _game)
    {
        _sqrt3Half = MathF.Sqrt(3) * 0.5f;
        chunksize = GameConstants.CHUNK_SIZE;
        bufferedChunkSize = chunksize + 2;
        invertedChunkSize = 1.0f / chunksize;
        _game.TerrainChunkTesselator.Start();
        _terrainStarted = true;
    }

    public void RedrawAllBlocks(IGame _game)
    {
        if (!_terrainStarted)
        {
            StartTerrain(_game);
        }

        int chunksLength = InvertChunk(_game.MapSizeX)
                         * InvertChunk(_game.MapSizeY)
                         * InvertChunk(_game.MapSizeZ);

        for (int i = 0; i < chunksLength; i++)
        {
            Chunk c = _voxelMap.Chunks[i];
            if (c == null)
            {
                continue;
            }

            c.rendered ??= new RenderedChunk();
            c.rendered.Dirty = true;
            c.baseLightDirty = true;
        }
    }

    // ── Background thread ─────────────────────────────────────────────────────

    public void UpdateTerrain(IGame _game)
    {
        if (!_terrainStarted)
        {
            return;
        }

        RedrawChunksAroundLastPlacedBlock(_game);

        (int x, int y, int z)? nearest = NearestDirty(_game);
        if (nearest.HasValue)
        {
            RedrawChunk(_game, nearest.Value.x, nearest.Value.y, nearest.Value.z);
        }
    }

    private void RedrawChunksAroundLastPlacedBlock(IGame _game)
    {
        if (_game.LastplacedblockX == NoChunk
         && _game.LastplacedblockY == NoChunk
         && _game.LastplacedblockZ == NoChunk)
        {
            return;
        }

        int mapSizeX = InvertChunk(_voxelMap.MapSizeX);
        int mapSizeY = InvertChunk(_voxelMap.MapSizeY);
        int mapSizeZ = InvertChunk(_voxelMap.MapSizeZ);

        BlocksAround7Inplace(
            new(_game.LastplacedblockX, _game.LastplacedblockY, _game.LastplacedblockZ),
            _blocksAround7Buffer);

        for (int i = 0; i < 7; i++)
        {
            Vector3i a = _blocksAround7Buffer[i];
            int cx = InvertChunk(a.X), cy = InvertChunk(a.Y), cz = InvertChunk(a.Z);

            if (cx < 0 || cy < 0 || cz < 0
             || cx >= mapSizeX || cy >= mapSizeY || cz >= mapSizeZ)
            {
                continue;
            }

            int idx = VectorIndexUtil.Index3d(cx, cy, cz, mapSizeX, mapSizeY);
            Chunk c = _voxelMap.Chunks[idx];
            if (c?.rendered == null)
            {
                continue;
            }

            c.rendered.Dirty = true;
        }

        _game.LastplacedblockX = NoChunk;
        _game.LastplacedblockY = NoChunk;
        _game.LastplacedblockZ = NoChunk;
    }

    private static void BlocksAround7Inplace(Vector3i pos, Vector3i[] buffer)
    {
        buffer[0] = pos;
        buffer[1] = new(pos.X + 1, pos.Y, pos.Z);
        buffer[2] = new(pos.X - 1, pos.Y, pos.Z);
        buffer[3] = new(pos.X, pos.Y + 1, pos.Z);
        buffer[4] = new(pos.X, pos.Y - 1, pos.Z);
        buffer[5] = new(pos.X, pos.Y, pos.Z + 1);
        buffer[6] = new(pos.X, pos.Y, pos.Z - 1);
    }

    /// <summary>
    /// Scans the view-distance window around the player for the nearest dirty chunk.
    /// O(V³) over the view volume — same scope as the original, avoids iterating
    /// the full pre-allocated map array which is mostly null slots.
    /// </summary>
    private (int x, int y, int z)? NearestDirty(IGame _game)
    {
        if (_voxelMap?.Chunks == null)
        {
            return null;
        }

        int px = InvertChunk((int)_game.LocalPositionX);
        int py = InvertChunk((int)_game.LocalPositionZ);
        int pz = InvertChunk((int)_game.LocalPositionY);

        int half = InvertChunk((int)_game.Config3d.ViewDistance);

        int startX = Math.Max(px - half, 0);
        int startY = Math.Max(py - half, 0);
        int startZ = Math.Max(pz - half, 0);
        int endX = Math.Min(px + half, MapsizeXChunks(_game) - 1);
        int endY = Math.Min(py + half, MapsizeYChunks(_game) - 1);
        int endZ = Math.Min(pz + half, MapsizeZChunks(_game) - 1);

        int mxc = MapsizeXChunks(_game);
        int myc = MapsizeYChunks(_game);

        int bestIdx = -1;
        int bestDist = int.MaxValue;

        for (int ix = startX; ix <= endX; ix++)
        {
            for (int iy = startY; iy <= endY; iy++)
            {
                for (int iz = startZ; iz <= endZ; iz++)
                {
                    int i = VectorIndexUtil.Index3d(ix, iy, iz, mxc, myc);
                    Chunk c = _voxelMap.Chunks[i];
                    if (c?.rendered == null || !c.rendered.Dirty)
                    {
                        continue;
                    }

                    int dx = px - ix, dy = py - iy, dz = pz - iz;
                    int dist = (dx * dx) + (dy * dy) + (dz * dz);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = i;
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        if (bestIdx == -1)
        {
            return null;
        }

        _voxelMap.Chunks[bestIdx].rendered.Dirty = false;

        int biz = bestIdx / (mxc * myc);
        int biy = (bestIdx % (mxc * myc)) / mxc;
        int bix = bestIdx % mxc;
        return (bix, biy, biz);
    }

    // ── Main-thread commit ────────────────────────────────────────────────────

    public void MainThreadCommit()
    {
        for (int i = 0; i < _redrawQueueCount; i++)
        {
            DoRedraw(_redrawQueue[i]);
            if (_redrawQueue[i].DataRented)
            {
                ArrayPool<VerticesIndicesToLoad>.Shared.Return(_redrawQueue[i].Data);
            }
        }

        _redrawQueueCount = 0;
    }

    private void DoRedraw(TerrainRendererRedraw r)
    {
        _batcherIdsCount = 0;
        RenderedChunk rendered = r.Chunk.rendered;

        if (rendered?.Ids != null)
        {
            for (int i = 0; i < rendered.IdsCount; i++)
            {
                meshBatcher.Remove(rendered.Ids[i]);
            }
        }

        for (int i = 0; i < r.DataCount; i++)
        {
            VerticesIndicesToLoad submesh = r.Data[i];
            if (submesh.ModelData.IndicesCount == 0)
            {
                ReturnModelArrays(submesh.ModelData);
                continue;
            }

            float cx = submesh.PositionX + (chunksize * 0.5f);
            float cy = submesh.PositionZ + (chunksize * 0.5f);
            float cz = submesh.PositionY + (chunksize * 0.5f);
            float radius = _sqrt3Half * chunksize;

            _batcherIds[_batcherIdsCount++] = meshBatcher.Add(
                submesh.ModelData, submesh.Transparent, submesh.Texture,
                cx, cy, cz, radius);

            ReturnModelArrays(submesh.ModelData);
        }

        if (rendered?.Ids == null || rendered.Ids.Length != _batcherIdsCount)
        {
            rendered?.Ids = new int[_batcherIdsCount];
        }

        for (int i = 0; i < _batcherIdsCount; i++)
        {
            rendered?.Ids[i] = _batcherIds[i];
        }

        rendered?.IdsCount = _batcherIdsCount;
    }

    // ── Tessellation ──────────────────────────────────────────────────────────

    private void RedrawChunk(IGame _game, int x, int y, int z)
    {
        Chunk c = _voxelMap.Chunks[
            VectorIndexUtil.Index3d(x, y, z, MapsizeXChunks(_game), MapsizeYChunks(_game))];
        if (c == null)
        {
            return;
        }

        c.rendered ??= new RenderedChunk();
        _chunkUpdates++;

        GetExtendedChunk(_game, x, y, z);

        int meshCount = 0;
        VerticesIndicesToLoad[] meshData = null;
        bool dataRented = false;

        if (!IsUniformChunk(_currentChunk, BufferedChunkVolume))
        {
            CalculateShadows(_game, x, y, z);
            VerticesIndicesToLoad[] meshes = _game.TerrainChunkTesselator.MakeChunk(
                x, y, z, _currentChunk, _currentChunkShadows,
                _game.LightLevels, out meshCount);

            if (meshCount > 0)
            {
                meshData = ArrayPool<VerticesIndicesToLoad>.Shared.Rent(meshCount);
                dataRented = true;
                for (int i = 0; i < meshCount; i++)
                {
                    meshData[i] = CloneVerticesIndicesToLoad(meshes[i]);
                }
            }
        }

        if (meshData == null)
        {
            meshData = Array.Empty<VerticesIndicesToLoad>();
            dataRented = false;
        }

        _redrawQueue[_redrawQueueCount++] = new(c, meshData, meshCount, dataRented);
    }

    private void GetExtendedChunk(IGame _game, int x, int y, int z)
    {
        _voxelMap.GetMapPortion(
            _currentChunk,
            (x * chunksize) - 1, (y * chunksize) - 1, (z * chunksize) - 1,
            bufferedChunkSize, bufferedChunkSize, bufferedChunkSize);
    }

    /// <summary>
    /// Returns <see langword="true"/> when every entry in <paramref name="chunk"/>
    /// is identical. Uniform chunks produce no visible faces and skip tessellation.
    /// </summary>
    private static bool IsUniformChunk(int[] chunk, int length)
    {
        int first = chunk[0];
        for (int i = 1; i < length; i++)
        {
            if (chunk[i] != first)
            {
                return false;
            }
        }

        return true;
    }

    private void CalculateShadows(IGame _game, int cx, int cy, int cz)
    {
        if (_blockTypeCacheDirty)
        {
            foreach ((int id, BlockType? blockType) in _blockTypeRegistry.BlockTypes)
            {
                _shadowLightRadius[id] = blockType.LightRadius;
                _shadowIsTransparent[id] = IsTransparentForLight(_game, id);
            }

            _blockTypeCacheDirty = false;
        }

        for (int xx = 0; xx < 3; xx++)
        {
            for (int yy = 0; yy < 3; yy++)
            {
                for (int zz = 0; zz < 3; zz++)
                {
                    int cx1 = cx + xx - 1;
                    int cy1 = cy + yy - 1;
                    int cz1 = cz + zz - 1;
                    if (!_voxelMap.IsValidChunkPos(cx1, cy1, cz1))
                    {
                        continue;
                    }

                    int nIdx = VectorIndexUtil.Index3d(
                        cx1, cy1, cz1,
                        _voxelMap.Mapsizexchunks,
                        _voxelMap.Mapsizeychunks);
                    Chunk neighbour = _voxelMap.Chunks[nIdx];
                    if (neighbour == null)
                    {
                        continue;
                    }

                    if (neighbour.baseLightDirty)
                    {
                        _lightBase.CalculateChunkBaseLight(
                            _game, cx1, cy1, cz1,
                            _shadowLightRadius, _shadowIsTransparent);
                        neighbour.baseLightDirty = false;
                    }
                }
            }
        }

        RenderedChunk rendered = _voxelMap
            .GetChunk(cx * chunksize, cy * chunksize, cz * chunksize).rendered;

        if (rendered.Light == null)
        {
            rendered.Light = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
            rendered.LightRented = true;
            rendered.Light.AsSpan(0, BufferedChunkVolume).Fill(15);
        }

        _lightBetweenChunks.CalculateLightBetweenChunks(
            _game, cx, cy, cz, _shadowLightRadius, _shadowIsTransparent);

        rendered.Light.AsSpan(0, BufferedChunkVolume)
                      .CopyTo(_currentChunkShadows.AsSpan(0, BufferedChunkVolume));
    }

    public bool IsTransparentForLight(IGame _game, int blockId)
    {
        BlockType b = _blockTypeRegistry.BlockTypes[blockId];
        return b.DrawType is not DrawType.Solid
            and not DrawType.ClosedDoor;
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    private void DrawTerrain(IGame _game)
    {
        meshBatcher.Draw(
            _game.LocalPositionX,
            _game.LocalPositionY,
            _game.LocalPositionZ);
    }

    internal void Clear(IGame _game) => meshBatcher.Clear();

    // ── Performance info ──────────────────────────────────────────────────────

    /// <summary>Updates chunk-update and triangle-count statistics once per second.</summary>
    internal void UpdatePerformanceInfo(IGame _game)
    {
        const float MsToSeconds = 1f / 1000f;
        float elapsed = (_platform.TimeMillisecondsFromStart - _lastPerfUpdateMs) * MsToSeconds;
        if (elapsed < 1f)
        {
            return;
        }

        _lastPerfUpdateMs = _platform.TimeMillisecondsFromStart;
        int updatesThisPeriod = _chunkUpdates - _lastChunkUpdatesSnapshot;
        _lastChunkUpdatesSnapshot = _chunkUpdates;

        _game.PerformanceInfo["chunk updates"] = string.Format(
            _game.Language.ChunkUpdates(), updatesThisPeriod.ToString());
        _game.PerformanceInfo["triangles"] = string.Format(
            _game.Language.Triangles(), TrianglesCount().ToString());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>View-distance-based side length of the active map area in blocks.</summary>
    private int MapsizeXChunks(IGame _game) => _voxelMap.Mapsizexchunks;
    private int MapsizeYChunks(IGame _game) => _voxelMap.Mapsizeychunks;
    private int MapsizeZChunks(IGame _game) => _voxelMap.Mapsizezchunks;

    private static VerticesIndicesToLoad CloneVerticesIndicesToLoad(VerticesIndicesToLoad source)
        => new()
        {
            ModelData = CloneModelData(source.ModelData),
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            PositionZ = source.PositionZ,
            Texture = source.Texture,
            Transparent = source.Transparent,
        };

    private static GeometryModel CloneModelData(GeometryModel source)
    {
        GeometryModel dest = new();
        unchecked
        {
            dest.Xyz = ArrayPool<float>.Shared.Rent(source.XyzCount);
            source.Xyz.AsSpan(0, source.XyzCount).CopyTo(dest.Xyz);

            dest.Uv = ArrayPool<float>.Shared.Rent(source.UvCount);
            source.Uv.AsSpan(0, source.UvCount).CopyTo(dest.Uv);

            dest.Rgba = ArrayPool<byte>.Shared.Rent(source.RgbaCount);
            source.Rgba.AsSpan(0, source.RgbaCount).CopyTo(dest.Rgba);

            dest.Indices = ArrayPool<int>.Shared.Rent(source.IndicesCount);
            source.Indices.AsSpan(0, source.IndicesCount).CopyTo(dest.Indices);

            dest.VerticesCount = source.VerticesCount;
            dest.IndicesCount = source.IndicesCount;
        }

        return dest;
    }

    private static void ReturnModelArrays(GeometryModel model)
    {
        if (model.Xyz != null)
        {
            ArrayPool<float>.Shared.Return(model.Xyz);
            model.Xyz = null;
        }

        if (model.Uv != null)
        {
            ArrayPool<float>.Shared.Return(model.Uv);
            model.Uv = null;
        }

        if (model.Rgba != null)
        {
            ArrayPool<byte>.Shared.Return(model.Rgba);
            model.Rgba = null;
        }

        if (model.Indices != null)
        {
            ArrayPool<int>.Shared.Return(model.Indices);
            model.Indices = null;
        }
    }
}

/// <summary>
/// Carries tessellated geometry for one chunk from the background thread
/// to the main thread for GPU upload.
/// </summary>
internal readonly record struct TerrainRendererRedraw(
    Chunk Chunk,
    VerticesIndicesToLoad[] Data,
    int DataCount,
    bool DataRented);