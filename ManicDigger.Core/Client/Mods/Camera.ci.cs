using OpenTK.Mathematics;

/// <summary>
/// Handles first-person, third-person, and overhead camera positioning each frame.
/// </summary>
public class ModCamera : ModBase
{
    private static readonly Vector3 Up = Vector3.UnitY;
    private Vector3 overheadCameraEye;

    public ModCamera()
    {
    }

    public override void OnBeforeNewFrameDraw3d(IGame game, float deltaTime)
    {
        game.Camera = game.OverheadCamera ? OverheadCamera(game) : FppCamera(game);
    }

    private Matrix4 OverheadCamera(IGame game)
    {
        game.OverheadCameraK.GetPosition(ref overheadCameraEye);
        Vector3 eye = overheadCameraEye;
        Vector3 target = new(
            game.OverheadCameraK.Center.X,
            game.OverheadCameraK.Center.Y + game.GetCharacterEyesHeight(),
            game.OverheadCameraK.Center.Z);

        game.OverHeadCameraDistance = LimitThirdPersonCameraToWalls(game, ref eye, ref target, game.OverHeadCameraDistance);
        SetCameraEye(game, eye);
        return Matrix4.LookAt(eye, target, Up);
    }

    private Matrix4 FppCamera(IGame game)
    {
        Vector3 forward = new();
        VectorUtils.ToVectorInFixedSystem(0, 0, 1, game.Player.position.rotx, game.Player.position.roty, ref forward);

        float eyeX = game.Player.position.x;
        float eyeY = game.Player.position.y + game.GetCharacterEyesHeight();
        float eyeZ = game.Player.position.z;

        Vector3 eye, target;

        if (!game.EnableTppView)
        {
            eye = new Vector3(eyeX, eyeY, eyeZ);
            target = new Vector3(eyeX + forward.X, eyeY + forward.Y, eyeZ + forward.Z);
        }
        else
        {
            eye = new Vector3(eyeX + forward.X * -game.TppCameraDistance,
                                 eyeY + forward.Y * -game.TppCameraDistance,
                                 eyeZ + forward.Z * -game.TppCameraDistance);
            target = new Vector3(eyeX, eyeY, eyeZ);
            game.TppCameraDistance = LimitThirdPersonCameraToWalls(game, ref eye, ref target, game.TppCameraDistance);
        }

        SetCameraEye(game, eye);
        return Matrix4.LookAt(eye, target, Up);
    }

    /// <summary>
    /// Casts a ray from the camera target toward the eye and pulls the camera
    /// in if terrain blocks the view, with a minimum distance of 0.3 units.
    /// </summary>
    private float LimitThirdPersonCameraToWalls(IGame game, ref Vector3 eye, ref Vector3 target, float distance)
    {
        const float MinDistance = 0.3f;

        Vector3 dir = eye - target;
        float dirLength = dir.Length;
        dir /= dirLength;

        Vector3 rayEnd = target + dir * (game.TppCameraDistance + 1);
        Line3D pick = new()
        {
            Start = target,
            End = rayEnd
        };

        ArraySegment<BlockPosSide> hits = game.Pick(game.BlockOctreeSearcher, pick, out int hitCount);
        if (hitCount > 0)
        {
            BlockPosSide nearest = game.Nearest(hits, hitCount, target);
            float pickDistance = new Vector3(nearest.blockPos[0] - target.X, nearest.blockPos[1] - target.Y, nearest.blockPos[2] - target.Z).Length;
            distance = Math.Max(MinDistance, Math.Min(pickDistance - 1, distance));
        }

        eye = target + dir * distance;
        return distance;
    }

    /// <summary>Writes the eye position back to the game for other systems to use.</summary>
    private void SetCameraEye(IGame game, Vector3 eye)
    {
        game.CameraEye = eye;
    }
}