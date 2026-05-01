using OpenTK.Mathematics;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles keyboard input for player movement and camera control each fixed frame.
/// </summary>
public class ModCameraKeys : ModBase
{
    private const float OverheadCameraSpeed = 3f;
    private readonly IGameService platform;
    private readonly ICameraService cameraService;

    public ModCameraKeys(IGameService platform, ICameraService cameraService)
    {
        this.platform = platform;
        this.cameraService = cameraService;
    }

    public override void OnNewFrameFixed(IGame game, float args)
    {
        if (game.GuiState == GuiState.MapLoading) return;

        float dt = args;
        bool isNormal = game.GuiState == GuiState.Normal;
        bool isTyping = game.GuiTyping != TypingState.None;

        UpdateJumpAndShift(game, isNormal, isTyping);

        game.Controls.MovedX = 0;
        game.Controls.MovedY = 0;
        game.Controls.MoveUp = false;
        game.Controls.MoveDown = false;

        if (isNormal)
        {
            if (!isTyping)
            {
                UpdateAutoJump(game);

                if (game.OverheadCamera)
                    UpdateOverheadCamera(game, dt);
                else if (game.EnableMove)
                    UpdateMovementKeys(game);
            }

            UpdateVerticalFreemove(game, isTyping);
        }
    }

    /// <summary>Updates jump and shift control flags based on keyboard state.</summary>
    private static void UpdateJumpAndShift(IGame game, bool isNormal, bool isTyping)
    {
        bool canAct = isNormal && !isTyping;
        game.Controls.WantsJump = canAct && game.KeyboardState[game.GetKey(Keys.Space)];
        game.Controls.WantsJumpHalf = false;
        game.Controls.ShiftKeyDown = canAct && game.KeyboardState[game.GetKey(Keys.LeftShift)];
    }

    /// <summary>Triggers auto-jump when the player walks into a climbable wall or half-block.</summary>
    private void UpdateAutoJump(IGame game)
    {
        if (game.ReachedWall1BlockHigh && (game.AutoJumpEnabled || !platform.IsMousePointerLocked()))
            game.Controls.WantsJump = true;

        if (game.ReachedHalfBlock)
            game.Controls.WantsJumpHalf = true;
    }

    /// <summary>Handles overhead (RTS-style) camera rotation, angle, and click-to-move.</summary>
    private void UpdateOverheadCamera(IGame game, float dt)
    {
        if (game.KeyboardState[game.GetKey(Keys.A)]) cameraService.TurnRight(dt * OverheadCameraSpeed);
        if (game.KeyboardState[game.GetKey(Keys.D)]) cameraService.TurnLeft(dt * OverheadCameraSpeed);

        cameraService.Center = new Vector3(game.Player.position.x,
            game.Player.position.y, game.Player.position.z);

        CameraMoveArgs m = new()
        {
            Distance = cameraService.OverHeadCameraDistance,
            AngleUp = game.KeyboardState[game.GetKey(Keys.W)],
            AngleDown = game.KeyboardState[game.GetKey(Keys.S)],
        };
        cameraService.Move(m, dt);

        // Click-to-move: steer player toward destination
        float toDest = Vector3.Distance(
            new Vector3(game.Player.position.x, game.Player.position.y, game.Player.position.z),
            new Vector3(game.PlayerDestination.X + 0.5f, game.PlayerDestination.Y - 0.5f, game.PlayerDestination.Z + 0.5f));

        if (toDest >= 1)
        {
            game.Controls.MovedY += 1;
            if (game.ReachedWall) game.Controls.WantsJump = true;

            float qX = game.PlayerDestination.X - game.Player.position.x;
            float qZ = game.PlayerDestination.Z - game.Player.position.z;
            game.Player.position.roty = MathF.PI / 2 + MathF.Atan2(qX, qZ);
            game.Player.position.rotx = MathF.PI;
        }
    }

    /// <summary>Handles WASD movement and leaning animation hints in first/third person.</summary>
    private static void UpdateMovementKeys(IGame game)
    {
        if (game.KeyboardState[game.GetKey(Keys.W)]) game.Controls.MovedY += 1;
        if (game.KeyboardState[game.GetKey(Keys.S)]) game.Controls.MovedY -= 1;

        bool leanLeft = game.KeyboardState[game.GetKey(Keys.A)];
        bool leanRight = game.KeyboardState[game.GetKey(Keys.D)];

        if (leanLeft) { game.Controls.MovedX -= 1; game.LocalStance = 1; }
        if (leanRight) { game.Controls.MovedX += 1; game.LocalStance = 2; }
        if (!leanLeft && !leanRight) game.LocalStance = 0;

        game.LocalPlayerAnimationHint.LeanLeft = leanLeft;
        game.LocalPlayerAnimationHint.LeanRight = leanRight;

        game.Controls.MovedX += game.TouchMoveDx;
        game.Controls.MovedY += game.TouchMoveDy;
    }

    /// <summary>Handles vertical movement in freemove mode or while swimming.</summary>
    private void UpdateVerticalFreemove(IGame game, bool isTyping)
    {
        if (!game.Controls.FreeMove && !game.SwimmingEyes()) return;
        if (isTyping) return;

        if (game.KeyboardState[game.GetKey(Keys.Space)]) game.Controls.MoveUp = true;
        if (game.KeyboardState[game.GetKey(Keys.LeftControl)]) game.Controls.MoveDown = true;
    }
}