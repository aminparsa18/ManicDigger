using OpenTK.Mathematics;

/// <summary>
/// Handles first-person, third-person, and overhead camera positioning each frame.
/// </summary>
public class ModCamera : ModBase
{
    private static readonly Vector3 Up = Vector3.UnitY;
    private Vector3 overheadCameraEye;
    private readonly ICameraService cameraService;

    public ModCamera(ICameraService cameraService, IGame game) : base(game)
    {
        this.cameraService = cameraService;
    }

    public override void OnBeforeNewFrameDraw3d(float deltaTime)
    {
        Game.Camera = Game.OverheadCamera ? OverheadCamera() : FppCamera();
    }

    private Matrix4 OverheadCamera()
    {
        cameraService.GetPosition(ref overheadCameraEye);
        Vector3 eye = overheadCameraEye;
        Vector3 target = new(
            cameraService.Center.X,
            cameraService.Center.Y + Game.GetCharacterEyesHeight(),
            cameraService.Center.Z);

        cameraService.OverHeadCameraDistance = LimitThirdPersonCameraToWalls(ref eye, ref target, cameraService.OverHeadCameraDistance);
        SetCameraEye(eye);
        return Matrix4.LookAt(eye, target, Up);
    }

    private Matrix4 FppCamera()
    {
        Vector3 forward = new();
        VectorUtils.ToVectorInFixedSystem(0, 0, 1, Game.Player.position.rotx, Game.Player.position.roty, ref forward);

        float eyeX = Game.Player.position.x;
        float eyeY = Game.Player.position.y + Game.GetCharacterEyesHeight();
        float eyeZ = Game.Player.position.z;
        Vector3 eye, target;

        if (!Game.EnableTppView)
        {
            eye = new Vector3(eyeX, eyeY, eyeZ);
            target = new Vector3(eyeX + forward.X, eyeY + forward.Y, eyeZ + forward.Z);
        }
        else
        {
            eye = new Vector3(eyeX + forward.X * -Game.TppCameraDistance,
                                 eyeY + forward.Y * -Game.TppCameraDistance,
                                 eyeZ + forward.Z * -Game.TppCameraDistance);
            target = new Vector3(eyeX, eyeY, eyeZ);
            Game.TppCameraDistance = LimitThirdPersonCameraToWalls(ref eye, ref target, Game.TppCameraDistance);
        }

        SetCameraEye(eye);
        return Matrix4.LookAt(eye, target, Up);
    }

    /// <summary>
    /// Casts a ray from the camera target toward the eye and pulls the camera
    /// in if terrain blocks the view, with a minimum distance of 0.3 units.
    /// </summary>
    private float LimitThirdPersonCameraToWalls(ref Vector3 eye, ref Vector3 target, float distance)
    {
        const float MinDistance = 0.3f;

        Vector3 dir = eye - target;
        float dirLength = dir.Length;
        dir /= dirLength;

        Vector3 rayEnd = target + dir * (Game.TppCameraDistance + 1);
        Line3D pick = new()
        {
            Start = target,
            End = rayEnd
        };

        ArraySegment<BlockPosSide> hits = Game.Pick(cameraService.BlockOctreeSearcher, pick, out int hitCount);
        if (hitCount > 0)
        {
            BlockPosSide nearest = Game.Nearest(hits, hitCount, target);
            float pickDistance = new Vector3(nearest.blockPos[0] - target.X, nearest.blockPos[1] - target.Y, nearest.blockPos[2] - target.Z).Length;
            distance = Math.Max(MinDistance, Math.Min(pickDistance - 1, distance));
        }

        eye = target + dir * distance;
        return distance;
    }

    /// <summary>Writes the eye position back to the game for other systems to use.</summary>
    private void SetCameraEye(Vector3 eye)
    {
        Game.CameraEye = eye;
    }
}