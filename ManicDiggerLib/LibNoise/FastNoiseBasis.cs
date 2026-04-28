using System.Runtime.CompilerServices;

namespace LibNoise;

/// <summary>
/// Low-level gradient noise basis used by <see cref="FastNoise"/> and other
/// LibNoise modules. Implements the permutation-table approach from Ken Perlin's
/// original noise algorithm to produce smooth, coherent pseudo-random values.
/// <para>
/// On construction (or whenever <see cref="Seed"/> is set), a gradient lookup
/// table is built from a seeded <see cref="Random"/>. <see cref="GradientCoherentNoise"/>
/// then trilinearly interpolates across the eight corners of the unit cube
/// surrounding the sample point using those gradients.
/// </para>
/// </summary>
public class FastNoiseBasis : Math
{
    // ── Permutation / gradient tables ─────────────────────────────────────────

    /// <summary>
    /// Doubled permutation table (indices 0–255 mirrored to 256–511) so that
    /// index arithmetic never wraps — avoids a modulo on every lookup.
    /// </summary>
    private readonly int[] _permutations = new int[512];

    /// <summary>
    /// Pre-computed gradient values mapped through <see cref="_permutations"/>.
    /// Doubled to 512 entries for the same wrap-avoidance reason as above.
    /// Stored as <c>float</c> — the noise output is single-precision so there
    /// is no benefit in keeping these as <c>double</c>.
    /// </summary>
    private readonly float[] _gradients = new float[512];

    private int _seed;

    // ── Seed ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Random seed that determines the noise pattern.
    /// Setting this rebuilds the permutation and gradient tables immediately.
    /// Must be non-negative.
    /// </summary>
    public int Seed
    {
        get => _seed;
        set
        {
            _seed = value;

            // Build permutation table from the seed.
            // _randomValues has been eliminated — we write directly into
            // _permutations[0..255] then mirror, saving one 512-int allocation
            // and a copy loop.
            var rng = new Random(_seed);
            for (int i = 0; i < 256; i++)
                _permutations[i] = rng.Next(255);

            // Mirror [0..255] into [256..511] so index+1 never wraps.
            for (int i = 0; i < 256; i++)
                _permutations[256 + i] = _permutations[i];

            // Map each permutation entry to a gradient in [-1, 1] and store
            // directly — gradientSource[] temp array eliminated.
            const float inv255 = 1f / 255f;
            for (int i = 0; i < 256; i++)
                _gradients[i] = -1f + 2f * (_permutations[i] * inv255);

            // Mirror gradient table to [256..511].
            for (int i = 256; i < 512; i++)
                _gradients[i] = _gradients[i & 0xFF];
        }
    }

    // ── Construction ──────────────────────────────────────────────────────────

    /// <summary>Initialises a <see cref="FastNoiseBasis"/> with the given seed.</summary>
    /// <param name="seed">Non-negative random seed.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="seed"/> is negative.</exception>
    public FastNoiseBasis(int seed)
    {
        if (seed < 0)
            throw new ArgumentException("Seed must be non-negative.", nameof(seed));
        Seed = seed;
    }

    // ── Core noise ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a coherent gradient noise value in approximately [-1, 1] at the
    /// given world position, using the permutation table built from <see cref="Seed"/>.
    /// <para>
    /// The algorithm:
    /// <list type="number">
    ///   <item>Floor the input to find the unit cube containing the point.</item>
    ///   <item>Compute fractional offsets inside the cube.</item>
    ///   <item>Smooth the offsets with an S-curve (quality-dependent).</item>
    ///   <item>Look up gradient values at the cube's eight corners via
    ///         the permutation table.</item>
    ///   <item>Trilinearly interpolate the eight corner gradients.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="x">World X coordinate.</param>
    /// <param name="y">World Y coordinate.</param>
    /// <param name="z">World Z coordinate.</param>
    /// <param name="seed">Per-octave seed offset (added to the instance seed externally by callers).</param>
    /// <param name="noiseQuality">
    /// Interpolation smoothing level:
    /// <see cref="NoiseQuality.Low"/> = linear (fast, blocky),
    /// <see cref="NoiseQuality.Standard"/> = cubic S-curve,
    /// <see cref="NoiseQuality.High"/> = quintic S-curve (smoothest).
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GradientCoherentNoise(float x, float y, float z,
        int seed, NoiseQuality noiseQuality)
    {
        // Floor to integer cube origin (handles negative coordinates correctly).
        int ix = x > 0f ? (int)x : (int)x - 1;
        int iy = y > 0f ? (int)y : (int)y - 1;
        int iz = z > 0f ? (int)z : (int)z - 1;

        // Fractional position inside the unit cube.
        float fx = x - ix;
        float fy = y - iy;
        float fz = z - iz;

        // Wrap cube coordinates to [0, 255] for table lookup.
        int cx = ix & 0xFF;
        int cy = iy & 0xFF;
        int cz = iz & 0xFF;

        // Smooth the fractional offsets based on quality (inlined, no virtual call).
        float sx, sy, sz;
        switch (noiseQuality)
        {
            case NoiseQuality.Low:
                sx = fx; sy = fy; sz = fz;
                break;
            case NoiseQuality.Standard:
                sx = fx * fx * (3f - 2f * fx);
                sy = fy * fy * (3f - 2f * fy);
                sz = fz * fz * (3f - 2f * fz);
                break;
            default: // High
                sx = fx * fx * fx * (fx * (6f * fx - 15f) + 10f);
                sy = fy * fy * fy * (fy * (6f * fy - 15f) + 10f);
                sz = fz * fz * fz * (fz * (6f * fz - 15f) + 10f);
                break;
        }

        // Pin spans once — suppresses repeated bounds checks across all 8 lookups.
        ReadOnlySpan<int> perm = _permutations;
        ReadOnlySpan<float> grad = _gradients;

        // Look up gradient table indices for the eight cube corners.
        int a = perm[cx] + cy;
        int aa = perm[a] + cz;
        int ab = perm[a + 1] + cz;
        int b = perm[cx + 1] + cy;
        int ba = perm[b] + cz;
        int bb = perm[b + 1] + cz;

        // Trilinear interpolation across the eight corners (Lerp inlined as FMA).
        float x1 = Lerp(grad[aa], grad[ba], sx);
        float x2 = Lerp(grad[ab], grad[bb], sx);
        float y1 = Lerp(x1, x2, sy);

        float x3 = Lerp(grad[aa + 1], grad[ba + 1], sx);
        float x4 = Lerp(grad[ab + 1], grad[bb + 1], sx);
        float y2 = Lerp(x3, x4, sy);

        return Lerp(y1, y2, sz);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Inline linear interpolation — avoids virtual dispatch to Math.Lerp.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) => a + t * (b - a);
}