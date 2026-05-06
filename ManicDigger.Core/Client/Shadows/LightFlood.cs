/// <summary>
/// Floods light values outward from one or more seed positions inside a
/// 16×16×16 chunk using a breadth-first approach.
///
/// Changes from original:
///   • Queue&lt;int&gt; replaced with a power-of-two ring buffer — no heap
///     allocation per flood, better cache locality.
///   • Per-dequeue x/y/z decode replaced with a static per-position
///     neighbour-mask table — eliminates three integer divisions per step.
///   • FloodLightAll() enables a single multi-source BFS seeded from every
///     lit position, replacing the O(n × BFS) pair-flood loop in LightBase.
/// </summary>
public sealed class LightFlood
{
    // ── Flat index offsets for the 6 face-connected neighbours ────────────────
    // Layout: index = z*256 + y*16 + x
    public const int XPlus = 1;
    public const int XMinus = -1;
    public const int YPlus = 16;
    public const int YMinus = -16;
    public const int ZPlus = 256;
    public const int ZMinus = -256;

    // ── Neighbour-mask lookup table (built once, shared across all instances) ─
    // One byte per flat position.  Bits flag which of the 6 neighbours are in
    // bounds so RunFlood never decodes x/y/z at runtime:
    //   bit 0 → X+   bit 1 → X−
    //   bit 2 → Y+   bit 3 → Y−
    //   bit 4 → Z+   bit 5 → Z−
    private static readonly byte[] s_mask = BuildMask();

    private static byte[] BuildMask()
    {
        byte[] m = new byte[4096];
        for (int z = 0; z < 16; z++)
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    byte b = 0;
                    if (x < 15) b |= 1 << 0;
                    if (x > 0) b |= 1 << 1;
                    if (y < 15) b |= 1 << 2;
                    if (y > 0) b |= 1 << 3;
                    if (z < 15) b |= 1 << 4;
                    if (z > 0) b |= 1 << 5;
                    m[z * 256 + y * 16 + x] = b;
                }
        return m;
    }

    // ── Power-of-two ring buffer ───────────────────────────────────────────────
    // (index & _mask) replaces expensive modulo.
    // 4096 initial slots = one full chunk worth; rarely grows.
    private int[] _buf;
    private int _head, _tail, _count, _mask;

    public LightFlood(int initialCapacity = 4096)
    {
        _buf = new int[initialCapacity];
        _mask = initialCapacity - 1;
    }

    private void Enqueue(int v)
    {
        if (_count == _buf.Length) Grow();
        _buf[_tail] = v;
        _tail = (_tail + 1) & _mask;
        _count++;
    }

    private int Dequeue()
    {
        int v = _buf[_head];
        _head = (_head + 1) & _mask;
        _count--;
        return v;
    }

    private void Grow()
    {
        int newLen = _buf.Length * 2;
        int[] next = new int[newLen];
        for (int i = 0; i < _count; i++)
            next[i] = _buf[(_head + i) & _mask];
        _buf = next;
        _head = 0;
        _tail = _count;
        _mask = newLen - 1;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Single-source BFS flood from one seed position.
    /// Skips immediately if the seed cell has no light.
    /// </summary>
    public void FloodLight(
        int[] chunk, byte[] light,
        int startX, int startY, int startZ,
        int[] dataLightRadius, bool[] dataTransparent)
    {
        int start = startZ * 256 + startY * 16 + startX;
        if (light[start] == 0) return;

        _head = _tail = _count = 0;
        Enqueue(start);
        RunFlood(chunk, light, dataLightRadius, dataTransparent);
    }

    /// <summary>
    /// Multi-source BFS seeded simultaneously from every lit position in the
    /// chunk.  One call replaces N individual <see cref="FloodLight"/> calls and
    /// visits each position at most once regardless of how many seeds there are.
    /// Used by LightBase after sunlight seeding and by LightBetweenChunks after
    /// boundary propagation.
    /// </summary>
    public void FloodLightAll(
        int[] chunk, byte[] light,
        int[] dataLightRadius, bool[] dataTransparent)
    {
        _head = _tail = _count = 0;
        for (int i = 0; i < 4096; i++)
            if (light[i] > 0)
                Enqueue(i);

        RunFlood(chunk, light, dataLightRadius, dataTransparent);
    }

    // ── Core BFS (shared by both entry points) ────────────────────────────────

    private void RunFlood(
        int[] chunk, byte[] light,
        int[] dataLightRadius, bool[] dataTransparent)
    {
        while (_count > 0)
        {
            int pos = Dequeue();
            int vLight = light[pos];
            if (vLight == 0) continue;

            int block = chunk[pos];
            if (!dataTransparent[block] && dataLightRadius[block] == 0) continue;

            int next = vLight - 1;
            byte nb = s_mask[pos];

            if ((nb & (1 << 0)) != 0) TryPush(light, next, pos + XPlus);
            if ((nb & (1 << 1)) != 0) TryPush(light, next, pos + XMinus);
            if ((nb & (1 << 2)) != 0) TryPush(light, next, pos + YPlus);
            if ((nb & (1 << 3)) != 0) TryPush(light, next, pos + YMinus);
            if ((nb & (1 << 4)) != 0) TryPush(light, next, pos + ZPlus);
            if ((nb & (1 << 5)) != 0) TryPush(light, next, pos + ZMinus);
        }
    }

    private void TryPush(byte[] light, int next, int pos)
    {
        if (light[pos] < next)
        {
            light[pos] = (byte)next;
            Enqueue(pos);
        }
    }
}