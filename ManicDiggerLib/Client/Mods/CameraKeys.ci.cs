using OpenTK.Mathematics;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles keyboard input for player movement and camera control each fixed frame.
/// </summary>
public class ModCameraKeys : ModBase
{
    private const float OverheadCameraSpeed = 3f;

    public override void OnNewFrameFixed(Game game, float args)
    {
        if (game.GuiState == GuiState.MapLoading) return;

        float dt = args;
        bool isNormal = game.GuiState == GuiState.Normal;
        bool isTyping = game.GuiTyping != TypingState.None;

        UpdateJumpAndShift(game, isNormal, isTyping);

        game.Controls.movedx = 0;
        game.Controls.movedy = 0;
        game.Controls.moveup = false;
        game.Controls.movedown = false;

        if (isNormal)
        {
            if (!isTyping)
            {
                UpdateAutoJump(game);

                if (game.overheadcamera)
                    UpdateOverheadCamera(game, dt);
                else if (game.enable_move)
                    UpdateMovementKeys(game);
            }

            UpdateVerticalFreemove(game, isTyping);
        }
    }

    /// <summary>Updates jump and shift control flags based on keyboard state.</summary>
    private static void UpdateJumpAndShift(Game game, bool isNormal, bool isTyping)
    {
        bool canAct = isNormal && !isTyping;
        game.Controls.wantsjump = canAct && game.keyboardState[game.GetKey(Keys.Space)];
        game.Controls.wantsjumphalf = false;
        game.Controls.shiftkeydown = canAct && game.keyboardState[game.GetKey(Keys.LeftShift)];
    }

    /// <summary>Triggers auto-jump when the player walks into a climbable wall or half-block.</summary>
    private static void UpdateAutoJump(Game game)
    {
        if (game.reachedwall_1blockhigh && (game.AutoJumpEnabled || !game.Platform.IsMousePointerLocked()))
            game.Controls.wantsjump = true;

        if (game.reachedHalfBlock)
            game.Controls.wantsjumphalf = true;
    }

    /// <summary>Handles overhead (RTS-style) camera rotation, angle, and click-to-move.</summary>
    private static void UpdateOverheadCamera(Game game, float dt)
    {
        if (game.keyboardState[game.GetKey(Keys.A)]) game.overheadcameraK.TurnRight(dt * OverheadCameraSpeed);
        if (game.keyboardState[game.GetKey(Keys.D)]) game.overheadcameraK.TurnLeft(dt * OverheadCameraSpeed);

        game.overheadcameraK.Center = new Vector3(game.Player.position.x,
            game.Player.position.y, game.Player.position.z);

        CameraMove m = new()
        {
            Distance = game.overheadcameradistance,
            AngleUp = game.keyboardState[game.GetKey(Keys.W)],
            AngleDown = game.keyboardState[game.GetKey(Keys.S)],
        };
        game.overheadcameraK.Move(m, dt);

        // Click-to-move: steer player toward destination
        float toDest = Vector3.Distance(
            new Vector3(game.Player.position.x, game.Player.position.y, game.Player.position.z),
            new Vector3(game.playerdestination.X + 0.5f, game.playerdestination.Y - 0.5f, game.playerdestination.Z + 0.5f));

        if (toDest >= 1)
        {
            game.Controls.movedy += 1;
            if (game.reachedwall) game.Controls.wantsjump = true;

            float qX = game.playerdestination.X - game.Player.position.x;
            float qZ = game.playerdestination.Z - game.Player.position.z;
            game.Player.position.roty = MathF.PI / 2 + MathF.Atan2(qX, qZ);
            game.Player.position.rotx = MathF.PI;
        }
    }

    /// <summary>Handles WASD movement and leaning animation hints in first/third person.</summary>
    private static void UpdateMovementKeys(Game game)
    {
        if (game.keyboardState[game.GetKey(Keys.W)]) game.Controls.movedy += 1;
        if (game.keyboardState[game.GetKey(Keys.S)]) game.Controls.movedy -= 1;

        bool leanLeft = game.keyboardState[game.GetKey(Keys.A)];
        bool leanRight = game.keyboardState[game.GetKey(Keys.D)];

        if (leanLeft) { game.Controls.movedx -= 1; game.localstance = 1; }
        if (leanRight) { game.Controls.movedx += 1; game.localstance = 2; }
        if (!leanLeft && !leanRight) game.localstance = 0;

        game.localplayeranimationhint.LeanLeft = leanLeft;
        game.localplayeranimationhint.LeanRight = leanRight;

        game.Controls.movedx += game.touchMoveDx;
        game.Controls.movedy += game.touchMoveDy;
    }

    /// <summary>Handles vertical movement in freemove mode or while swimming.</summary>
    private static void UpdateVerticalFreemove(Game game, bool isTyping)
    {
        if (!game.Controls.freemove && !game.SwimmingEyes()) return;
        if (isTyping) return;

        if (game.keyboardState[game.GetKey(Keys.Space)]) game.Controls.moveup = true;
        if (game.keyboardState[game.GetKey(Keys.LeftControl)]) game.Controls.movedown = true;
    }
}