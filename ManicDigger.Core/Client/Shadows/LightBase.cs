using ManicDigger;

/// <summary>
/// Calculates base lighting for a single 16×16×16 chunk.
/// Handles sunlight seeding from the heightmap and light-emitting blocks.
/// Does not spread light across chunk boundaries (see LightBetweenChunks).
///
/// Key change vs original: SunlightFlood no longer calls FloodLight once per
/// pair of adjacent transparent blocks (O(n²)).  Instead it calls
/// FloodLightFromAllSeeds once after Sunlight() seeds the top rows (O(n)).
/// </summary>
public class LightBase
{
    private const int ChunkSize = 16;
    private const int ChunkVolume = ChunkSize * ChunkSize * ChunkSize;

    private readonly LightFlood _flood;
    private readonly IVoxelMap _voxelMap;
    private readonly ILightManager _lightManager;
    private readonly int[] _workData = new int[ChunkVolume];

    public LightBase(IVoxelMap voxelMap, ILightManager lightManager)
    {
        _flood = new LightFlood();
        _voxelMap = voxelMap;
        _lightManager = lightManager;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void CalculateChunkBaseLight(
        int cx, int cy, int cz,
        int[] dataLightRadius,
        bool[] transparentForLight)
    {
        Chunk chunk = _voxelMap.GetChunkAt(cx, cy, cz);

        for (int i = 0; i < ChunkVolume; i++)
            _workData[i] = chunk.GetBlock(i);

        byte[] workLight = chunk.BaseLight;
        Array.Clear(workLight, 0, workLight.Length);

        // 1. Seed sunlight from the heightmap — fills columns from entry point upward.
        SeedSunlight(cx, cy, cz, workLight, _lightManager.Sunlight);

        // 2. Propagate sunlight horizontally through transparent blocks.
        //    One multi-source BFS from every already-lit cell — O(n) not O(n²).
        _flood.FloodLightFromAllSeeds(_workData, workLight, dataLightRadius, transparentForLight);

        // 3. Seed and flood emissive blocks.
        FloodEmissive(_workData, workLight, dataLightRadius, transparentForLight);
    }

    // ── Sunlight seeding ──────────────────────────────────────────────────────

    private void SeedSunlight(int cx, int cy, int cz, byte[] workLight, int sunlight)
    {
        int baseHeight = cz * GameConstants.CHUNK_SIZE;

        for (int xx = 0; xx < ChunkSize; xx++)
        {
            for (int yy = 0; yy < ChunkSize; yy++)
            {
                int height = GetLightHeight(cx, cy, xx, yy);

                int z = height - baseHeight;
                if (z < 0) z = 0;
                if (z > ChunkSize) continue; // sunlight enters above this chunk

                int pos = Index3d(xx, yy, z, ChunkSize, ChunkSize);
                for (int zz = z; zz < ChunkSize; zz++, pos += LightFlood.ZPlus)
                    workLight[pos] = (byte)sunlight;
            }
        }
    }

    private int GetLightHeight(int cx, int cy, int xx, int yy)
    {
        int[] heightmapChunk = _voxelMap.Heightmap.GetOrAllocChunk(
            cx * GameConstants.CHUNK_SIZE,
            cy * GameConstants.CHUNK_SIZE);

        return heightmapChunk?[
            VectorIndexUtil.Index2d(
                xx % GameConstants.CHUNK_SIZE,
                yy % GameConstants.CHUNK_SIZE,
                GameConstants.CHUNK_SIZE)] ?? 0;
    }

    // ── Emissive blocks ───────────────────────────────────────────────────────

    /// <summary>
    /// Finds every emissive block, raises its light to its emission radius if
    /// not already higher, then floods. One FloodLight call per emissive block
    /// (typically very few per chunk).
    /// </summary>
    private void FloodEmissive(
        int[] workData, byte[] workLight,
        int[] dataLightRadius, bool[] dataTransparent)
    {
        for (int pos = 0; pos < ChunkVolume; pos++)
        {
            int blockId = workData[pos];
            if (blockId < 10) continue; // no block below ID 10 emits light

            int radius = dataLightRadius[blockId];
            if (radius == 0) continue;
            if (radius <= workLight[pos]) continue;

            workLight[pos] = (byte)radius;

            int x = pos & 15;
            int y = (pos >> 4) & 15;
            int z = pos >> 8;
            _flood.FloodLight(workData, workLight, x, y, z, dataLightRadius, dataTransparent);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int Index3d(int x, int y, int z, int sx, int sy)
        => ((z * sy) + y) * sx + x;
}