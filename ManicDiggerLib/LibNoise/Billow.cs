namespace LibNoise;

/// <summary>
/// Billowing fBm noise generator. Similar to <see cref="FastNoise"/> but folds
/// each octave's signal with an absolute-value and bias, producing a "billowing
/// clouds" appearance rather than smooth gradients.
/// <para>
/// Each octave: takes the absolute value of the gradient noise, scales it to
/// [-1, 1], weights by the running amplitude, then advances frequency and decays
/// amplitude by <see cref="Persistence"/>. A final +0.5 bias lifts the output
/// into approximately [0, 1].
/// </para>
/// </summary>
public class Billow : GradientNoiseBasis, IModule
{
    private const int MaxOctaves = 30;
    private int _octaveCount;

    public float Frequency { get; set; }
    public float Persistence { get; set; }
    public float Lacunarity { get; set; }
    public NoiseQuality NoiseQuality { get; set; }
    public int Seed { get; set; }

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

    public Billow()
    {
        Frequency = 1f;
        Lacunarity = 2f;
        OctaveCount = 6;
        Persistence = 0.5f;
        NoiseQuality = NoiseQuality.Standard;
        Seed = 0;
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