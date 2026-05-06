/// <summary>
/// Calculates final lighting for a chunk by sampling base light from the
/// 3×3×3 neighbourhood of chunks surrounding it, flooding light across
/// chunk boundaries, then writing the result into the chunk's render buffer.
///
/// Change from original:
///   FloodBetweenChunks no longer calls FloodLight per boundary cell.
///   Old approach: 16×16 cells × 6 faces × 27 chunks × 2 passes
///                 = up to ~83 000 individual FloodLight invocations.
///   New approach: copy improved light values across all boundaries first,
///                 then one FloodLightAll per chunk (27 × 2 = 54 BFS calls).
/// </summary>
public class LightBetweenChunks
{
    private const int NS = 3;           // neighbourhood size per axis
    private const int NVol = NS * NS * NS; // 27 chunks
    private const int CS = 16;          // chunk size per axis
    private const int CVol = CS * CS * CS; // 4096 blocks per chunk
    private const int Out = 18;          // output buffer side (CS + 1-block border)

    private readonly LightFlood _flood;
    private readonly byte[][] _chunksLight = new byte[NVol][];
    private readonly int[][] _chunksData = new int[NVol][];
    private readonly IVoxelMap _voxelMap;

    public LightBetweenChunks(IVoxelMap voxelMap)
    {
        _voxelMap = voxelMap;
        _flood = new LightFlood();
        for (int i = 0; i < NVol; i++)
        {
            _chunksLight[i] = new byte[CVol];
            _chunksData[i] = new int[CVol];
        }
    }

    public void CalculateLightBetweenChunks(
        int cx, int cy, int cz,
        int[] dataLightRadius,
        bool[] dataTransparent)
    {
        Input(cx, cy, cz);
        FloodBetweenChunks(dataLightRadius, dataTransparent);
        Output(cx, cy, cz);
    }

    private static int Idx(int x, int y, int z) => (z * NS + y) * NS + x;
    private static int BlockIdx(int x, int y, int z) => z * 256 + y * 16 + x;

    // ── Input ─────────────────────────────────────────────────────────────────

    private void Input(int cx, int cy, int cz)
    {
        for (int x = 0; x < NS; x++)
            for (int y = 0; y < NS; y++)
                for (int z = 0; z < NS; z++)
                {
                    int slot = Idx(x, y, z);
                    int pcx = cx + x - 1, pcy = cy + y - 1, pcz = cz + z - 1;

                    if (!_voxelMap.IsValidChunkPos(pcx, pcy, pcz))
                    {
                        Array.Clear(_chunksData[slot], 0, CVol);
                        Array.Clear(_chunksLight[slot], 0, CVol);
                        continue;
                    }

                    Chunk chunk = _voxelMap.GetChunkAt(pcx, pcy, pcz);
                    int[] data = _chunksData[slot];
                    byte[] light = _chunksLight[slot];

                    for (int i = 0; i < CVol; i++)
                        data[i] = chunk.GetBlock(i);

                    Array.Copy(chunk.BaseLight, light, CVol);
                }
    }

    // ── Flood ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Two passes, each consisting of:
    ///   1. Copy improved light values across every chunk-face boundary.
    ///   2. One FloodLightAll per chunk to propagate them inward.
    ///
    /// This replaces the old per-cell FloodLight calls that fired up to
    /// 16 × 16 = 256 times per face — now each chunk floods exactly once
    /// per pass regardless of how many boundary cells received new light.
    /// </summary>
    private void FloodBetweenChunks(int[] dataLightRadius, bool[] dataTransparent)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            // Step 1: propagate values across all six face pairs.
            for (int x = 0; x < NS; x++)
                for (int y = 0; y < NS; y++)
                    for (int z = 0; z < NS; z++)
                    {
                        byte[] src = _chunksLight[Idx(x, y, z)];

                        if (z < NS - 1) CopyFaceZPlus(src, _chunksLight[Idx(x, y, z + 1)]);
                        if (z > 0) CopyFaceZMinus(src, _chunksLight[Idx(x, y, z - 1)]);
                        if (x < NS - 1) CopyFaceXPlus(src, _chunksLight[Idx(x + 1, y, z)]);
                        if (x > 0) CopyFaceXMinus(src, _chunksLight[Idx(x - 1, y, z)]);
                        if (y < NS - 1) CopyFaceYPlus(src, _chunksLight[Idx(x, y + 1, z)]);
                        if (y > 0) CopyFaceYMinus(src, _chunksLight[Idx(x, y - 1, z)]);
                    }

            // Step 2: one multi-source BFS per chunk picks up all improved cells.
            for (int i = 0; i < NVol; i++)
                _flood.FloodLightAll(_chunksData[i], _chunksLight[i], dataLightRadius, dataTransparent);
        }
    }

    // ── Face copy helpers ─────────────────────────────────────────────────────
    // Each helper copies the light value from the edge face of src into the
    // opposite edge face of dst, only if it would improve the destination.
    // The value decays by 1 crossing the boundary (same as intra-chunk decay).

    private static void CopyFaceZPlus(byte[] src, byte[] dst)
    {
        // src z=15 → dst z=0
        for (int y = 0; y < CS; y++)
            for (int x = 0; x < CS; x++)
            {
                int sv = src[BlockIdx(x, y, 15)] - 1;
                int di = BlockIdx(x, y, 0);
                if (sv > 0 && dst[di] < sv) dst[di] = (byte)sv;
            }
    }

    private static void CopyFaceZMinus(byte[] src, byte[] dst)
    {
        // src z=0 → dst z=15
        for (int y = 0; y < CS; y++)
            for (int x = 0; x < CS; x++)
            {
                int sv = src[BlockIdx(x, y, 0)] - 1;
                int di = BlockIdx(x, y, 15);
                if (sv > 0 && dst[di] < sv) dst[di] = (byte)sv;
            }
    }

    private static void CopyFaceXPlus(byte[] src, byte[] dst)
    {
        // src x=15 → dst x=0
        for (int z = 0; z < CS; z++)
            for (int y = 0; y < CS; y++)
            {
                int sv = src[BlockIdx(15, y, z)] - 1;
                int di = BlockIdx(0, y, z);
                if (sv > 0 && dst[di] < sv) dst[di] = (byte)sv;
            }
    }

    private static void CopyFaceXMinus(byte[] src, byte[] dst)
    {
        // src x=0 → dst x=15
        for (int z = 0; z < CS; z++)
            for (int y = 0; y < CS; y++)
            {
                int sv = src[BlockIdx(0, y, z)] - 1;
                int di = BlockIdx(15, y, z);
                if (sv > 0 && dst[di] < sv) dst[di] = (byte)sv;
            }
    }

    private static void CopyFaceYPlus(byte[] src, byte[] dst)
    {
        // src y=15 → dst y=0
        for (int z = 0; z < CS; z++)
            for (int x = 0; x < CS; x++)
            {
                int sv = src[BlockIdx(x, 15, z)] - 1;
                int di = BlockIdx(x, 0, z);
                if (sv > 0 && dst[di] < sv) dst[di] = (byte)sv;
            }
    }

    private static void CopyFaceYMinus(byte[] src, byte[] dst)
    {
        // src y=0 → dst y=15
        for (int z = 0; z < CS; z++)
            for (int x = 0; x < CS; x++)
            {
                int sv = src[BlockIdx(x, 0, z)] - 1;
                int di = BlockIdx(x, 15, z);
                if (sv > 0 && dst[di] < sv) dst[di] = (byte)sv;
            }
    }

    // ── Output ────────────────────────────────────────────────────────────────

    private void Output(int cx, int cy, int cz)
    {
        Chunk chunk = _voxelMap.GetChunkAt(cx, cy, cz);
        byte[] renderLight = chunk.Rendered.Light;

        for (int x = 0; x < Out; x++)
            for (int y = 0; y < Out; y++)
                for (int z = 0; z < Out; z++)
                {
                    int globalX = CS - 1 + x;
                    int globalY = CS - 1 + y;
                    int globalZ = CS - 1 + z;

                    int ncx = globalX / CS;
                    int ncy = globalY / CS;
                    int ncz = globalZ / CS;
                    int localX = globalX % CS;
                    int localY = globalY % CS;
                    int localZ = globalZ % CS;

                    renderLight[(z * Out + y) * Out + x] =
                        _chunksLight[Idx(ncx, ncy, ncz)][BlockIdx(localX, localY, localZ)];
                }
    }
}