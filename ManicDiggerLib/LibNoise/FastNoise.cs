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

    public double Frequency { get; set; }
    public double Persistence { get; set; }
    public NoiseQuality NoiseQuality { get; set; }
    public double Lacunarity { get; set; }

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
        Frequency = 1.0;
        Lacunarity = 2.0;
        OctaveCount = 6;
        Persistence = 0.5;
        NoiseQuality = NoiseQuality.Standard;
    }

    public double GetValue(double x, double y, double z)
    {
        double sum = 0.0;
        double amplitude = 1.0;

        x *= Frequency;
        y *= Frequency;
        z *= Frequency;

        for (int i = 0; i < OctaveCount; i++)
        {
            int octaveSeed = (int)((Seed + i) & 0xFFFFFFFFu);
            double signal = GradientCoherentNoise(x, y, z, octaveSeed, NoiseQuality);
            sum += signal * amplitude;
            x *= Lacunarity;
            y *= Lacunarity;
            z *= Lacunarity;
            amplitude *= Persistence;
        }

        return sum;
    }
}