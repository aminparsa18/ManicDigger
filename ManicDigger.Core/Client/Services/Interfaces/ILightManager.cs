using OpenTK.Mathematics;

namespace ManicDigger;

public interface ILightManager
{
    /// <summary>Maps light level (0–15) to a GL colour multiplier.</summary>
    float[] LightLevels { get; set; }

    /// <summary>Current sun light level (0–15).</summary>
    int Sunlight { get; set; }

    /// <summary>Per-level night light multipliers.</summary>
    int[] NightLevels { get; set; }
    /// <summary>World-space position of the sun billboard.</summary>
    Vector3 sunPosition { get; set; }

    /// <summary>World-space position of the moon billboard.</summary>
    Vector3 moonPosition { get; set; }

    /// <summary>Whether it is currently night-time.</summary>
    bool isNight { get; set; }
    /// <summary>Whether the fancy sky-sphere shader is enabled.</summary>
    bool fancySkysphere { get; set; }

    /// <summary>Whether the night sky-sphere variant is active.</summary>
    bool SkySphereNight { get; set; }

    /// <summary>Whether simple (non-smooth) shadows are used.</summary>
    bool ShadowsSimple { get; set; }

    /// <summary>Returns the light level at the given world position.</summary>
    int GetLight(int x, int y, int z);
}

public class LightManager : ILightManager
{
    public float[] LightLevels { get; set; }
    public int Sunlight { get; set; }
    public int[] NightLevels { get; set; }
    public Vector3 sunPosition { get; set; }
    public Vector3 moonPosition { get; set; }
    public bool isNight { get; set; }
    public bool fancySkysphere { get; set; }
    public bool SkySphereNight { get; set; }
    public bool ShadowsSimple { get; set; }

    private readonly IVoxelMap _voxelMap;

    public LightManager(IVoxelMap voxelMap)
    {
        _voxelMap = voxelMap;
        Sunlight = 15;
        LightLevels = new float[16];
        for (int i = 0; i < 16; i++)
        {
            LightLevels[i] = 0.15f;
        }
    }

    /// <summary>
    /// Returns the baked light level at a block position, falling back to
    /// sunlight when above the heightmap or to <see cref="minlight"/> when
    /// lighting data is unavailable.
    /// </summary>
    public int GetLight(int x, int y, int z)
    {
        int light = _voxelMap.MaybeGetLight(x, y, z);
        if (light != -1)
        {
            return light;
        }

        if (x >= 0 && x < _voxelMap.MapSizeX
         && y >= 0 && y < _voxelMap.MapSizeY
         && z >= _voxelMap.Heightmap.GetBlock(x, y))
        {
            return Sunlight;
        }

        return GameConstants.minlight;
    }
}
