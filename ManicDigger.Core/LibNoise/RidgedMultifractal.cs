using System.Runtime.CompilerServices;

namespace LibNoise;

/// <summary>
/// Ridged multifractal noise generator. Produces sharp ridges and smooth valleys,
/// suitable for mountain ranges, cliff edges, and canyon walls.
/// <para>
/// Unlike standard fBm (<see cref="FastNoise"/>), each octave's signal is
/// folded (absolute value), inverted, and squared before being weighted by
/// the previous octave's output. This feedback loop creates sharp discontinuities
/// at ridge lines while keeping valleys smooth.
/// </para>
/// <para>
/// Used in <c>DefaultWorldGenerator</c> for the mountain terrain layer.
/// </para>
/// </summary>
public class RidgedMultifractal : GradientNoiseBasis, IModule
{
    private const int MaxOctaves = 30;

    private int _octaveCount;
    private float _lacunarity;

    /// <summary>
    /// Pre-computed per-octave amplitude weights based on <see cref="Lacunarity"/>.
    /// Stored as <c>float</c> — precision beyond single is wasted here since
    /// the noise values themselves are <c>float</c>.
    /// Recalculated whenever <see cref="Lacunarity"/> changes.
    /// </summary>
    private readonly float[] _spectralWeights = new float[MaxOctaves];

    // ── Parameters ────────────────────────────────────────────────────────────

    /// <summary>Base frequency of the lowest octave. Default is <c>1.0</c>.</summary>
    public float Frequency { get; set; }

    /// <summary>Noise interpolation quality. Default is <see cref="NoiseQuality.Standard"/>.</summary>
    public NoiseQuality NoiseQuality { get; set; }

    /// <summary>Random seed that determines the noise pattern. Default is <c>0</c>.</summary>
    public int Seed { get; set; }

    /// <summary>
    /// Frequency multiplier between successive octaves.
    /// Setting this recalculates <see cref="_spectralWeights"/> immediately.
    /// Default is <c>2.0</c>.
    /// </summary>
    public float Lacunarity
    {
        get => _lacunarity;
        set
        {
            _lacunarity = value;
            CalculateSpectralWeights();
        }
    }

    /// <summary>
    /// Number of octaves to sum. More octaves add finer ridge detail.
    /// Must be in the range [1, 30]. Default is <c>6</c>.
    /// </summary>
    public int OctaveCount
    {
        get => _octaveCount;
        set
        {
            if (value < 1 || value > MaxOctaves)
                throw new ArgumentException(
                    $"OctaveCount must be between 1 and {MaxOctaves}, got {value}.");
            _octaveCount = value;
        }
    }

    // ── Construction ──────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises a <see cref="RidgedMultifractal"/> with default parameters.
    /// Setting <see cref="Lacunarity"/> triggers the initial spectral weight calculation.
    /// </summary>
    public RidgedMultifractal()
    {
        Frequency = 1f;
        Lacunarity = 2f;  // also calls CalculateSpectralWeights()
        OctaveCount = 6;
        NoiseQuality = NoiseQuality.Standard;
        Seed = 0;
    }

    // ── IModule ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the ridged multifractal noise value at world position
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).
    /// <para>
    /// Each octave:
    /// <list type="number">
    ///   <item>Samples gradient noise and takes the absolute value (fold).</item>
    ///   <item>Inverts and squares the result to sharpen ridges.</item>
    ///   <item>Weights by the previous octave's output (feedback).</item>
    ///   <item>Accumulates into the running sum weighted by the spectral weight.</item>
    /// </list>
    /// The output is scaled and biased to approximately [-1, 1].
    /// </para>
    /// </summary>
    public float GetValue(float x, float y, float z)
    {
        x *= Frequency;
        y *= Frequency;
        z *= Frequency;

        float sum = 0f;
        float weight = 1f;

        // Offset and gain control ridge sharpness — standard constants.
        const float offset = 1f;
        const float gain = 2f;

        // Cache fields in locals so the JIT doesn't re-read them each iteration.
        int octaveCount = _octaveCount;
        float lacunarity = _lacunarity;
        int seed = Seed;
        NoiseQuality quality = NoiseQuality;
        ReadOnlySpan<float> sw = _spectralWeights;

        for (int i = 0; i < octaveCount; i++)
        {
            int octaveSeed = (seed + i) & 0x7FFFFFFF;

            // Sample gradient noise, fold to [0,1], invert so ridge peaks sit at
            // zero-crossings of the raw noise field, then square to sharpen them.
            float signal = GradientCoherentNoise(x, y, z, octaveSeed, quality);
            signal = offset - MathF.Abs(signal);
            signal *= signal;

            // Feedback: weight this octave by the previous signal so ridges
            // compound across octaves rather than blending uniformly.
            signal *= weight;
            weight = Clamp01(signal * gain);

            sum += signal * sw[i];

            x *= lacunarity;
            y *= lacunarity;
            z *= lacunarity;
        }

        // Scale output to approximately [-1, 1].
        return sum * 1.25f - 1f;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Branchless clamp to [0, 1] — avoids a Math.Clamp call in the hot loop.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    // ── Spectral weights ──────────────────────────────────────────────────────

    /// <summary>
    /// Pre-computes the amplitude weight for each octave as
    /// <c>lacunarity^(-H * i)</c> where H = 1 (standard for ridged noise).
    /// Called automatically whenever <see cref="Lacunarity"/> is set.
    /// </summary>
    private void CalculateSpectralWeights()
    {
        const float H = 1f;
        float frequency = 1f;
        for (int i = 0; i < MaxOctaves; i++)
        {
            _spectralWeights[i] = MathF.Pow(frequency, -H);
            frequency *= _lacunarity;
        }
    }
}