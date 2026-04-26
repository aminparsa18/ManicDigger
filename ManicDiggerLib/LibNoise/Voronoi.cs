namespace LibNoise;

/// <summary>
/// Voronoi (cellular/Worley) noise generator.
/// Divides space into irregular cells, each owned by the nearest randomly
/// jittered feature point, and returns a value based on that nearest point.
/// <para>
/// For each sample the algorithm searches a 5×5×5 neighbourhood of integer
/// grid cells. Each cell's centre is jittered by <see cref="ValueNoise"/> to
/// produce a pseudo-random feature point. The nearest feature point is found,
/// and the output is:
/// <list type="bullet">
///   <item>The noise value at the nearest feature point's grid cell (always).</item>
///   <item>Plus the Euclidean distance to that point scaled by
///         <see cref="Math.Sqrt3"/> (only when <see cref="DistanceEnabled"/>).</item>
/// </list>
/// Used in <c>DefaultWorldGenerator</c> for flower and decoration placement.
/// </para>
/// </summary>
public class Voronoi : ValueNoiseBasis, IModule
{
    // ── Parameters ────────────────────────────────────────────────────────────

    /// <summary>
    /// Scales the input coordinates before cell lookup.
    /// Higher values produce smaller, more densely packed cells. Default is <c>1.0</c>.
    /// </summary>
    public double Frequency { get; set; }

    /// <summary>
    /// Scales the cell colour value added to the output.
    /// Higher values produce stronger contrast between adjacent cells. Default is <c>1.0</c>.
    /// </summary>
    public double Displacement { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the Euclidean distance to the nearest feature
    /// point is added to the output, producing raised ridges at cell boundaries.
    /// Default is <see langword="false"/> (flat cell interiors).
    /// </summary>
    public bool DistanceEnabled { get; set; }

    /// <summary>Random seed used to jitter feature points. Default is <c>0</c>.</summary>
    public int Seed { get; set; }

    // ── Construction ──────────────────────────────────────────────────────────

    // ── IModule ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the Voronoi noise value at world position
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).
    /// </summary>
    public double GetValue(double x, double y, double z)
    {
        x *= Frequency;
        y *= Frequency;
        z *= Frequency;

        // Integer cell containing the sample point (floor, handles negatives).
        int cellX = x > 0.0 ? (int)x : (int)x - 1;
        int cellY = y > 0.0 ? (int)y : (int)y - 1;
        int cellZ = z > 0.0 ? (int)z : (int)z - 1;

        // Find the nearest jittered feature point in the 5×5×5 neighbourhood.
        double minDistSq = double.MaxValue;
        double nearestX = 0.0;
        double nearestY = 0.0;
        double nearestZ = 0.0;

        for (int iz = cellZ - 2; iz <= cellZ + 2; iz++)
            for (int iy = cellY - 2; iy <= cellY + 2; iy++)
                for (int ix = cellX - 2; ix <= cellX + 2; ix++)
                {
                    // Jitter the cell centre with three independent noise values.
                    double fpX = ix + ValueNoise(ix, iy, iz, Seed);
                    double fpY = iy + ValueNoise(ix, iy, iz, Seed + 1);
                    double fpZ = iz + ValueNoise(ix, iy, iz, Seed + 2);

                    double dx = fpX - x;
                    double dy = fpY - y;
                    double dz = fpZ - z;
                    double distSq = dx * dx + dy * dy + dz * dz;

                    if (distSq < minDistSq)
                    {
                        minDistSq = distSq;
                        nearestX = fpX;
                        nearestY = fpY;
                        nearestZ = fpZ;
                    }
                }

        // Optionally add the distance to the nearest feature point.
        double distanceContribution = 0.0;
        if (DistanceEnabled)
        {
            double dx = nearestX - x;
            double dy = nearestY - y;
            double dz = nearestZ - z;
            // Normalised so the maximum possible distance maps to ~1.
            distanceContribution = System.Math.Sqrt(dx * dx + dy * dy + dz * dz)
                                   * Math.Sqrt3 - 1.0;
        }

        // Cell colour: noise value at the nearest feature point's integer cell.
        int nx = nearestX > 0.0 ? (int)nearestX : (int)nearestX - 1;
        int ny = nearestY > 0.0 ? (int)nearestY : (int)nearestY - 1;
        int nz = nearestZ > 0.0 ? (int)nearestZ : (int)nearestZ - 1;

        return distanceContribution + Displacement * ValueNoise(nx, ny, nz);
    }
}