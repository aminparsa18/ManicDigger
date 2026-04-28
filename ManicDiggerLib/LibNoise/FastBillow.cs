namespace LibNoise;

/// <summary>
/// Billowing fBm noise generator backed by <see cref="FastNoiseBasis"/>.
/// Produces the same cloud-like, folded appearance as <see cref="Billow"/> but
/// uses the cheaper permutation-table gradient lookup instead of full 3-D vector
/// dot products, making it faster at the cost of slightly lower noise quality.
/// <para>
/// Each octave folds the gradient signal with <c>2 * |signal| - 1</c>, weights
/// by the running amplitude, then advances frequency and decays amplitude by
/// <see cref="Persistence"/>. A final +0.5 bias lifts the output to approximately
/// [0, 1].
/// </para>
/// </summary>
public class FastBillow : FastNoiseBasis, IModule
{
    private const int MaxOctaves = 30;
    private int _octaveCount;

    public float Frequency { get; set; }
    public float Persistence { get; set; }
    public float Lacunarity { get; set; }
    public NoiseQuality NoiseQuality { get; set; }

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

    public FastBillow() : this(0) { }

    public FastBillow(int seed) : base(seed)
    {
        Frequency = 1f;
        Lacunarity = 2f;
        OctaveCount = 6;
        Persistence = 0.5f;
        NoiseQuality = NoiseQuality.Standard;
    }

    public float GetValue(float x, float y, float z)
    {
        float sum = 0f;
        float amplitude = 1f;

        // Cache fields in locals — prevents repeated this-pointer dereferences
        // and keeps values in registers across all octave iterations.
        int octaveCount = _octaveCount;
        int seed = Seed;
        float lacunarity = Lacunarity;
        float persistence = Persistence;
        NoiseQuality quality = NoiseQuality;

        x *= Frequency;
        y *= Frequency;
        z *= Frequency;

        for (int i = 0; i < octaveCount; i++)
        {
            int octaveSeed = (seed + i) & 0x7FFFFFFF;

            // Fold signal into [0, 1] with abs, then bias to [-1, 1] to produce
            // the rounded, cloud-like billow shape.
            float signal = GradientCoherentNoise(x, y, z, octaveSeed, quality);
            signal = 2f * MathF.Abs(signal) - 1f;

            sum += signal * amplitude;
            x *= lacunarity;
            y *= lacunarity;
            z *= lacunarity;
            amplitude *= persistence;
        }

        // Bias output to approximately [0, 1].
        return sum + 0.5f;
    }
}