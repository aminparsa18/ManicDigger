/// <summary>
/// Floods light values outward from one or more seed positions inside a
/// 16×16×16 chunk using breadth-first propagation.
///
/// Hot-path optimisations vs the original:
///   • Pre-allocated int[] ring buffer replaces Queue&lt;int&gt; — no GC, no
///     internal resize, sequential memory access.
///   • Coordinate decode uses bit-ops (16 is a power of 2) instead of
///     integer division inside the inner loop.
///   • FloodLightFromAllSeeds seeds the BFS from every lit cell in one call
///     so callers never need to issue one flood per lit block.
/// </summary>
public class LightFlood
{
    private const int ChunkSize = 16;
    private const int ChunkVolume = ChunkSize * ChunkSize * ChunkSize; // 4 096

    // Ring buffer.  Each of the 4 096 cells can be re-enqueued at most 15 times
    // (once per light level), so 4 096 × 15 = 61 440 worst-case entries.
    // Next power of 2: 65 536.  Masking replaces modulo.
    private const int QueueCapacity = 1 << 16; // 65 536
    private const int QueueMask = QueueCapacity - 1;

    private readonly int[] _queue = new int[QueueCapacity];
    private int _head, _tail;

    // Flat-index offsets for the six face-connected neighbours.
    // Layout: index = (z * 16 + y) * 16 + x
    public const int XPlus = 1;
    public const int XMinus = -1;
    public const int YPlus = 16;
    public const int YMinus = -16;
    public const int ZPlus = 256;
    public const int ZMinus = -256;

    private static int Index3d(int x, int y, int z, int sx, int sy)
        => ((z * sy) + y) * sx + x;

    // ── Public entry points ───────────────────────────────────────────────────

    /// <summary>
    /// Floods light outward from a single seed position.
    /// No-op if the seed has no light.
    /// </summary>
    public void FloodLight(
        int[] chunk, byte[] light,
        int startX, int startY, int startZ,
        int[] dataLightRadius, bool[] dataTransparent)
    {
        int start = Index3d(startX, startY, startZ, ChunkSize, ChunkSize);
        if (light[start] == 0) return;

        _head = _tail = 0;
        Enqueue(start);
        RunFlood(chunk, light, dataLightRadius, dataTransparent);
    }

    /// <summary>
    /// Seeds the BFS from every position in the chunk that already has light
    /// and runs one combined flood pass.  Replaces calling FloodLight once per
    /// lit block — O(n) total instead of O(n²).
    /// </summary>
    public void FloodLightFromAllSeeds(
        int[] chunk, byte[] light,
        int[] dataLightRadius, bool[] dataTransparent)
    {
        _head = _tail = 0;
        for (int pos = 0; pos < ChunkVolume; pos++)
        {
            if (light[pos] > 0)
                Enqueue(pos);
        }
        RunFlood(chunk, light, dataLightRadius, dataTransparent);
    }

    // ── Core BFS ──────────────────────────────────────────────────────────────

    private void RunFlood(int[] chunk, byte[] light, int[] dataLightRadius, bool[] dataTransparent)
    {
        while (_head != _tail)
        {
            int pos = _queue[_head & QueueMask];
            _head++;

            int vLight = light[pos];
            if (vLight == 0) continue;

            int blockId = chunk[pos];
            if (!dataTransparent[blockId] && dataLightRadius[blockId] == 0)
                continue;

            // Bit-ops instead of integer division — 16 = 2⁴.
            int x = pos & 15;
            int y = (pos >> 4) & 15;
            int z = pos >> 8;

            int next = vLight - 1;

            if (x < 15) TryPush(light, next, pos + XPlus);
            if (x > 0) TryPush(light, next, pos + XMinus);
            if (y < 15) TryPush(light, next, pos + YPlus);
            if (y > 0) TryPush(light, next, pos + YMinus);
            if (z < 15) TryPush(light, next, pos + ZPlus);
            if (z > 0) TryPush(light, next, pos + ZMinus);
        }
    }

    private void TryPush(byte[] light, int next, int newPos)
    {
        if (light[newPos] < next)
        {
            light[newPos] = (byte)next;
            Enqueue(newPos);
        }
    }

    private void Enqueue(int pos)
    {
        _queue[_tail & QueueMask] = pos;
        _tail++;
    }
}