namespace LibNoise;

/// <summary>
/// Classic Perlin fBm noise generator. Stacks multiple octaves of coherent
/// gradient noise at progressively higher frequency and lower amplitude,
/// producing smooth, natural-looking noise suitable for terrain heightmaps,
/// cloud layers, and general-purpose procedural content.
/// <para>
/// Identical in structure to <see cref="FastNoise"/> but operates on
/// <see cref="GradientNoiseBasis"/> rather than <see cref="FastNoiseBasis"/>,
/// giving a different visual character from the same parameters.
/// </para>
/// </summary>
public class Perlin : GradientNoiseBasis, IModule
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

    public Perlin()
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
            sum += GradientCoherentNoise(x, y, z, octaveSeed, quality) * amplitude;
            x *= lacunarity;
            y *= lacunarity;
            z *= lacunarity;
            amplitude *= persistence;
        }

        return sum;
    }
}