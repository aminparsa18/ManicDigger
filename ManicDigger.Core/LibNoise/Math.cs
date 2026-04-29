namespace LibNoise;

/// <summary>
/// Shared mathematical utilities for LibNoise modules.
/// Provides smoothstep functions and constants used by noise generators.
/// </summary>
public class Math
{
    public static readonly double Sqrt3 = 1.7320508075688772;

    /// <summary>
    /// Cubic smoothstep — maps <paramref name="a"/> from [0,1] to [0,1]
    /// with zero first-derivative at both endpoints.
    /// Used for <see cref="NoiseQuality.Standard"/> interpolation.
    /// </summary>
    protected static double SCurve3(double a) => a * a * (3.0 - 2.0 * a);

    /// <summary>
    /// Quintic smootherstep — maps <paramref name="a"/> from [0,1] to [0,1]
    /// with zero first and second derivatives at both endpoints (smoother than
    /// <see cref="SCurve3"/>). Used for <see cref="NoiseQuality.High"/> interpolation.
    /// </summary>
    protected static double SCurve5(double a)
    {
        double a3 = a * a * a;
        double a4 = a3 * a;
        double a5 = a4 * a;
        return 6.0 * a5 - 15.0 * a4 + 10.0 * a3;
    }
}