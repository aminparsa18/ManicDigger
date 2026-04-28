namespace LibNoise;

/// <summary>
/// Fractal Brownian motion (fBm) noise generator.
/// Produces natural-looking noise by stacking multiple octaves of coherent
/// gradient noise, each at progressively higher frequency and lower amplitude.
/// </summary>
public class FastNoise : FastNoiseBasis, IModule
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

    public FastNoise() : this(0) { }

    public FastNoise(int seed) : base(seed)
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
        // inside the hot loop and lets the JIT keep everything in registers.
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