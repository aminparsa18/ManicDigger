using OpenTK.Mathematics;

/// <summary>
///This class controls an orbiting camera — like the camera in a strategy game or when you hold right-click and rotate the view in Minecraft.
///The camera always looks at a fixed point (Center) in the world and orbits around it, similar to a ball on a string. You control three things:
///Distance — how far the camera is from the center point (zoom in/out)
///Azimuth — horizontal rotation around the center (spin left/right)
///Angle — how high up the camera is tilted (looking from ground level vs. bird's eye)
///The angle is clamped between 0° and 89° so you can never flip the camera upside down, and the distance has a minimum so you can never zoom inside the target point.
/// </summary>
public class CameraService : ICameraService
{
    public CameraService()
    {
        Distance = 5;
        angle = 45;
        minimumDistance = 2;
        azimuth = 0;
        maximumAngle = 89;
        minimumAngle = 0;
        Center = new Vector3();
    }

    /// <summary>
    /// Computes the world-space position of the camera and writes it into <paramref name="ret"/>.
    /// The camera orbits <see cref="Center"/> at the current azimuth (<see cref="azimuth"/>),
    /// elevation (<see cref="angle"/>), and <see cref="Distance"/>.
    /// </summary>
    public void GetPosition(ref Vector3 ret)
    {
        float flatDist = GetFlatDistance();
        ret.X = MathF.Cos(azimuth) * flatDist + Center.X;
        ret.Y = Center.Y + GetCameraHeightFromCenter();
        ret.Z = MathF.Sin(azimuth) * flatDist + Center.Z;
    }

    /// <summary>Gets or sets the orbit radius, clamped to at least <see cref="minimumDistance"/> on set.</summary>
    private float Distance
    {
        get;
        set => field = MathF.Max(value, minimumDistance);
    }

    /// <summary>Minimum allowed orbit radius.</summary>
    private readonly float minimumDistance;

    /// <summary>Elevation angle of the camera above the horizontal plane, in degrees.</summary>
    private float angle;

    /// <summary>Maximum allowed elevation angle, in degrees.</summary>
    private readonly float maximumAngle;

    /// <summary>Minimum allowed elevation angle, in degrees.</summary>
    private readonly float minimumAngle;

    /// <summary>
    /// Horizontal rotation parameter (azimuth). The actual angle used in position math
    /// is <c>T / 2</c>, so a full orbit requires <c>T</c> to advance by <c>4π</c>.
    /// </summary>
    private float azimuth;

    /// <summary>The point in world space the camera orbits around.</summary>
    public Vector3 Center { get; set; }

    /// <summary>
    /// Returns the vertical offset of the camera above <see cref="Center"/>
    /// based on the current <see cref="angle"/> and <see cref="Distance"/>.
    /// </summary>
    private float GetCameraHeightFromCenter()
    {
        return MathF.Sin(angle * MathF.PI / 180) * Distance;
    }

    /// <summary>
    /// Returns the horizontal (XZ-plane) distance from <see cref="Center"/> to the camera
    /// based on the current <see cref="angle"/> and <see cref="Distance"/>.
    /// </summary>
    private float GetFlatDistance()
    {
        return MathF.Cos(angle * MathF.PI / 180) * Distance;
    }

    /// <summary>Rotates the camera left (counter-clockwise) by <paramref name="p"/> units.</summary>
    public void TurnLeft(float p) { azimuth += p; }

    /// <summary>Rotates the camera right (clockwise) by <paramref name="p"/> units.</summary>
    public void TurnRight(float p) { azimuth -= p; }

    /// <summary>
    /// Applies a <see cref="CameraMoveArgs"/> input to the camera for one frame.
    /// <paramref name="p"/> is scaled before use — turning scales by 4, angle changes by 40.
    /// </summary>
    public void Move(CameraMoveArgs camera_move, float p)
    {
        p *= 4;
        if (camera_move.TurnLeft) { TurnLeft(p); }
        if (camera_move.TurnRight) { TurnRight(p); }
        if (camera_move.DistanceUp) { Distance += p; }
        if (camera_move.DistanceDown) { Distance -= p; }
        if (camera_move.AngleUp) { angle += p * 10; }
        if (camera_move.AngleDown) { angle -= p * 10; }
        Distance = camera_move.Distance;
        SetValidAngle();
    }

    /// <summary>Clamps <see cref="angle"/> to the range [<see cref="minimumAngle"/>, <see cref="maximumAngle"/>].</summary>
    private void SetValidAngle()
    {
        angle = Math.Clamp(angle, minimumAngle, maximumAngle);
    }

    /// <summary>
    /// Increases the elevation angle by <paramref name="p"/> degrees and clamps to valid range.
    /// </summary>
    public void TurnUp(float p)
    {
        angle += p;
        SetValidAngle();
    }
}