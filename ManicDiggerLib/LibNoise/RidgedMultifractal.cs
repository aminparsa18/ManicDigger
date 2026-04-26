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
    private double _lacunarity;

    /// <summary>
    /// Pre-computed per-octave amplitude weights based on <see cref="Lacunarity"/>.
    /// Recalculated whenever <see cref="Lacunarity"/> changes.
    /// </summary>
    private readonly double[] _spectralWeights = new double[MaxOctaves];

    // ── Parameters ────────────────────────────────────────────────────────────

    /// <summary>Base frequency of the lowest octave. Default is <c>1.0</c>.</summary>
    public double Frequency { get; set; }

    /// <summary>Noise interpolation quality. Default is <see cref="NoiseQuality.Standard"/>.</summary>
    public NoiseQuality NoiseQuality { get; set; }

    /// <summary>Random seed that determines the noise pattern. Default is <c>0</c>.</summary>
    public int Seed { get; set; }

    /// <summary>
    /// Frequency multiplier between successive octaves.
    /// Setting this recalculates <see cref="_spectralWeights"/> immediately.
    /// Default is <c>2.0</c>.
    /// </summary>
    public double Lacunarity
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
        Frequency = 1.0;
        Lacunarity = 2.0; // also calls CalculateSpectralWeights()
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
    public double GetValue(double x, double y, double z)
    {
        x *= Frequency;
        y *= Frequency;
        z *= Frequency;

        double sum = 0.0;
        double weight = 1.0;

        // Offset and gain control ridge sharpness — these are standard constants.
        const double offset = 1.0;
        const double gain = 2.0;

        for (int i = 0; i < OctaveCount; i++)
        {
            int octaveSeed = (int)((Seed + i) & 0x7FFFFFFF);

            // Sample noise, fold to [0,1], invert to make ridges at zero-crossings.
            double signal = GradientCoherentNoise(x, y, z, octaveSeed, NoiseQuality);
            signal = System.Math.Abs(signal);
            signal = offset - signal;
            signal *= signal;               // square to sharpen ridges

            // Weight this octave by the previous signal (feedback amplifies ridges).
            signal *= weight;
            weight = System.Math.Clamp(signal * gain, 0.0, 1.0);

            sum += signal * _spectralWeights[i];

            x *= Lacunarity;
            y *= Lacunarity;
            z *= Lacunarity;
        }

        // Scale output to approximately [-1, 1].
        return sum * 1.25 - 1.0;
    }

    // ── Spectral weights ──────────────────────────────────────────────────────

    /// <summary>
    /// Pre-computes the amplitude weight for each octave as
    /// <c>lacunarity^(-H * i)</c> where H = 1 (standard for ridged noise).
    /// Called automatically whenever <see cref="Lacunarity"/> is set.
    /// </summary>
    private void CalculateSpectralWeights()
    {
        const double H = 1.0;
        double frequency = 1.0;
        for (int i = 0; i < MaxOctaves; i++)
        {
            _spectralWeights[i] = System.Math.Pow(frequency, -H);
            frequency *= _lacunarity;
        }
    }
}