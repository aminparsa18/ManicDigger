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
    /// Raw random values in [0, 254] generated from <see cref="Seed"/>.
    /// Used to shuffle <see cref="_permutations"/>.
    /// </summary>
    private readonly int[] _randomValues = new int[512];

    /// <summary>
    /// Doubled permutation table (indices 0–255 mirrored to 256–511) so that
    /// index arithmetic never wraps — avoids a modulo on every lookup.
    /// </summary>
    private readonly int[] _permutations = new int[512];

    /// <summary>
    /// Pre-computed gradient values mapped through <see cref="_permutations"/>.
    /// Also doubled to 512 entries for the same wrap-avoidance reason.
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

            // Build raw random permutation values from the seed.
            var rng = new Random(_seed);
            for (int i = 0; i < 512; i++)
                _randomValues[i] = rng.Next(255);

            // Mirror the first 256 entries into [256..511] so index+1 never wraps.
            for (int i = 0; i < 256; i++)
                _permutations[256 + i] = _permutations[i] = _randomValues[i];

            // Map uniform [0..255] integers to gradients in [-1, 1].
            var gradientSource = new float[256];
            for (int i = 0; i < 256; i++)
                gradientSource[i] = -1f + 2f * (i / 255f);

            // Shuffle gradients through the permutation table.
            for (int i = 0; i < 256; i++)
                _gradients[i] = gradientSource[_permutations[i]];

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
    public double GradientCoherentNoise(double x, double y, double z,
        int seed, NoiseQuality noiseQuality)
    {
        // Floor to integer cube origin (handles negative coordinates correctly).
        int ix = x > 0.0 ? (int)x : (int)x - 1;
        int iy = y > 0.0 ? (int)y : (int)y - 1;
        int iz = z > 0.0 ? (int)z : (int)z - 1;

        // Fractional position inside the unit cube.
        double fx = x - ix;
        double fy = y - iy;
        double fz = z - iz;

        // Wrap cube coordinates to [0, 255] for table lookup.
        int cx = ix & 0xFF;
        int cy = iy & 0xFF;
        int cz = iz & 0xFF;

        // Smooth the fractional offsets based on quality.
        double sx, sy, sz;
        switch (noiseQuality)
        {
            case NoiseQuality.Low:
                sx = fx; sy = fy; sz = fz;
                break;
            case NoiseQuality.Standard:
                sx = SCurve3(fx); sy = SCurve3(fy); sz = SCurve3(fz);
                break;
            default: // High
                sx = SCurve5(fx); sy = SCurve5(fy); sz = SCurve5(fz);
                break;
        }

        // Look up gradient table indices for the eight cube corners.
        int a = _permutations[cx] + cy;
        int aa = _permutations[a] + cz;
        int ab = _permutations[a + 1] + cz;
        int b = _permutations[cx + 1] + cy;
        int ba = _permutations[b] + cz;
        int bb = _permutations[b + 1] + cz;

        // Trilinear interpolation across the eight corners.
        double x1 = double.Lerp(_gradients[aa], _gradients[ba], sx);
        double x2 = double.Lerp(_gradients[ab], _gradients[bb], sx);
        double y1 = double.Lerp(x1, x2, sy);

        double x3 = double.Lerp(_gradients[aa + 1], _gradients[ba + 1], sx);
        double x4 = double.Lerp(_gradients[ab + 1], _gradients[bb + 1], sx);
        double y2 = double.Lerp(x3, x4, sy);

        return double.Lerp(y1, y2, sz);
    }
}