using static ManicDigger.Mods.ModNetworkProcess;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// ModFly – client-side flight mod.
///   F      → toggle flight mode on / off
///   Space  → rise  (while in flight mode)
///   Shift  → sink  (while in flight mode)
///
/// Works by driving Controls.freemove / moveup / movedown directly,
/// which ScriptCharacterPhysics already understands:
///   - freemove=true  → gravity disabled, full 3-axis movement
///   - moveup=true    → ascend (same as swim-up)
///   - movedown=true  → descend
/// </summary>
public class ModFly : ModBase
{
    private readonly IGameClient game;

    private bool flyActive = false;

    public ModFly(IGameClient game)
    {
        this.game = game;
    }

    // ── Toggle on F ───────────────────────────────────────────────────────────
    public override void OnKeyDown(KeyEventArgs args)
    {
        if (game.GuiState != GuiState.Normal || game.GuiTyping != TypingState.None)
            return;

        if (args.KeyChar != game.GetKey(Keys.F))
            return;

        flyActive = !flyActive;
        DiagLog.Write($"Flight mode {(flyActive ? "On" : "Off")}");
        if (flyActive)
        {
            game.AddChatLine("&aFlight ON  –  Space: rise  |  Shift: sink  |  F: exit");
        }
        else
        {
            // Clear fly controls so physics resumes normally on the very next tick
            game.Controls.FreeMove = false;
            game.Controls.MoveUp = false;
            game.Controls.MoveDown = false;
            game.playervelocity = new OpenTK.Mathematics.Vector3(
                game.playervelocity.X, 0f, game.playervelocity.Z);

            game.AddChatLine("&cFlight OFF");
        }
    }

    // ── Feed freemove + vertical intent into Controls every physics tick ──────
    public override void OnNewFrameFixed(float dt)
    {
        if (!flyActive) return;

        game.Controls.FreeMove = true;

        bool space = game.KeyboardStateRaw[game.GetKey(Keys.Space)];
        bool shift = game.KeyboardStateRaw[game.GetKey(Keys.LeftControl)];

        // Both or neither → hover (moveup and movedown both false keeps Y still)
        game.Controls.MoveUp = space && !shift;
        game.Controls.MoveDown = shift && !space;
    }

    // ── Cleanup on mod unload ─────────────────────────────────────────────────
    public override void Dispose()
    {
        if (!flyActive) return;

        game.Controls.FreeMove = false;
        game.Controls.MoveUp = false;
        game.Controls.MoveDown = false;
    }
}