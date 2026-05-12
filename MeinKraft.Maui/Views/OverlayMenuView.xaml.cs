// OverlayMenuView.xaml.cs
// ════════════════════════
// Code-behind for the pause / options overlay ContentView.
//
// Owns:
//   • Internal panel navigation  (Pause ↔ Options)
//   • All option toggle / stepper logic, wired to IGame + TerrainChunkTesselator
//   • Two exit events that GameView handles (cursor, GuiState, Shell nav)
//
// GameView contract:
//   1. Call Initialize(game) right after InitializeComponent()
//   2. Subscribe to ReturnToGameRequested / ExitToMenuRequested / FullscreenChanged
//   3. Call ShowPauseMenu() before setting IsVisible = true

namespace MeinKraft.Maui.Views;

public partial class OverlayMenuView : ContentView
{
    // ── Events for GameView ───────────────────────────────────────────────────

    /// <summary>Player clicked "Return to Game". GameView hides overlay + recaptures cursor.</summary>
    public event EventHandler? ReturnToGameRequested;

    /// <summary>Player clicked "Exit to Menu". GameView hides overlay + navigates.</summary>
    public event EventHandler? ExitToMenuRequested;

    /// <summary>
    /// Player toggled Fullscreen. GameView calls platform.SetWindowState accordingly.
    /// Payload is the new desired state (true = fullscreen).
    /// </summary>
    public event EventHandler<bool>? FullscreenChanged;

    // ── Injected services ─────────────────────────────────────────────────────
    private IGame? _game;
    private ITerrainChunkTesselator? _tesselator;

    // ── Resolution stepper ────────────────────────────────────────────────────
    private static readonly string[] Resolutions =
    {
        "1280×720",
        "1366×768",
        "1600×900",
        "1920×1080",
        "2560×1440",
        "3840×2160",
    };

    private int _resolutionIndex = 3; // default: 1920×1080

    // ── Constructor ───────────────────────────────────────────────────────────

    public OverlayMenuView()
    {
        InitializeComponent();
        // ApplyAllToggleStates() is deferred until Initialize() is called so the
        // UI reflects real game state rather than hard-coded defaults.
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Injects game services and seeds all toggle states from the live options.
    /// Call once from GameView's constructor, after InitializeComponent().
    /// </summary>
    public void Initialize(IGame game, ITerrainChunkTesselator terrainChunkTesselator)
    {
        _game = game;
        _tesselator = terrainChunkTesselator;

        GameOption o = game.options;

        // Seed backing state from persisted options so the UI opens in the
        // correct state instead of hard-coded defaults.
        _resolutionIndex = Math.Clamp(o.Resolution, 0, Resolutions.Length - 1);

        ApplyAllToggleStates();
    }

    /// <summary>
    /// Resets the overlay to the Pause panel (not Options).
    /// GameView calls this before setting IsVisible = true.
    /// </summary>
    public void ShowPauseMenu()
    {
        PausePanel.IsVisible = true;
        OptionsPanel.IsVisible = false;
    }

    // ── Internal panel navigation ─────────────────────────────────────────────

    private void OnReturnToGameClicked(object sender, EventArgs e)
        => ReturnToGameRequested?.Invoke(this, EventArgs.Empty);

    private void OnExitToMenuClicked(object sender, EventArgs e)
        => ExitToMenuRequested?.Invoke(this, EventArgs.Empty);

    private void OnOptionsClicked(object sender, EventArgs e)
    {
        PausePanel.IsVisible = false;
        OptionsPanel.IsVisible = true;
    }

    private void OnOptionsBackClicked(object sender, EventArgs e)
    {
        OptionsPanel.IsVisible = false;
        PausePanel.IsVisible = true;
    }

    // ── Toggle visual helper ──────────────────────────────────────────────────

    /// <summary>
    /// Swaps ToggleBtnActive (green) / ToggleBtnInactive (stone) between the pair.
    /// </summary>
    private void SetToggle(Button btnOn, Button btnOff, bool value)
    {
        btnOn.Style = value
            ? (Style)Resources["ToggleBtnActive"]
            : (Style)Resources["ToggleBtnInactive"];

        btnOff.Style = value
            ? (Style)Resources["ToggleBtnInactive"]
            : (Style)Resources["ToggleBtnActive"];
    }

    private void ApplyAllToggleStates()
    {
        if (_game is null) return;
        GameOption o = _game.options;

        SetToggle(BtnSmoothOn, BtnSmoothOff, o.Smoothshadows);
        SetToggle(BtnDarkenOn, BtnDarkenOff, o.EnableBlockShadow);
        SetToggle(BtnFullscreenOn, BtnFullscreenOff, o.Fullscreen);
        SetToggle(BtnServerTexOn, BtnServerTexOff, o.UseServerTextures);
        SetToggle(BtnSoundOn, BtnSoundOff, _game.AudioEnabled);
        SetToggle(BtnAutoJumpOn, BtnAutoJumpOff, _game.AutoJumpEnabled);
        LblResolution.Text = Resolutions[_resolutionIndex];
    }

    // ── Smooth Shadows ────────────────────────────────────────────────────────
    // Mirrors GraphicsHandleClick(graphicsOptionSmoothShadows) from the old mod:
    //   EnableSmoothLight + BlockShadow are both updated, then a full redraw.

    private void OnSmoothShadowsOnClicked(object sender, EventArgs e)
        => ApplySmoothShadows(true);

    private void OnSmoothShadowsOffClicked(object sender, EventArgs e)
        => ApplySmoothShadows(false);

    private void ApplySmoothShadows(bool value)
    {
        if (_game is null || _tesselator is null) return;

        GameOption o = _game.options;
        o.Smoothshadows = value;
        _tesselator.EnableSmoothLight = value;

        // BlockShadow differs between the two states — matched to the old values.
        o.BlockShadowSave = value ? 0.7f : 0.6f;
        _tesselator.BlockShadow = o.BlockShadowSave;

        _game.RedrawAllBlocks();
        SetToggle(BtnSmoothOn, BtnSmoothOff, value);
    }

    // ── Darken Sides ──────────────────────────────────────────────────────────
    // Mirrors GraphicsHandleClick(graphicsOpti  arkenSides).

    private void OnDarkenSidesOnClicked(object sender, EventArgs e)
        => ApplyDarkenSides(true);

    private void OnDarkenSidesOffClicked(object sender, EventArgs e)
        => ApplyDarkenSides(false);

    private void ApplyDarkenSides(bool value)
    {
        if (_game is null || _tesselator is null) return;

        _game.options.EnableBlockShadow = value;
        _tesselator.DarkenBlockSidesOption = value;

        _game.RedrawAllBlocks();
        SetToggle(BtnDarkenOn, BtnDarkenOff, value);
    }

    // ── Fullscreen ────────────────────────────────────────────────────────────
    // options.Fullscreen is set here; the platform SetWindowState call is
    // delegated to GameView via FullscreenChanged so this view stays
    // platform-agnostic.

    private void OnFullscreenOnClicked(object sender, EventArgs e)
        => ApplyFullscreen(true);

    private void OnFullscreenOffClicked(object sender, EventArgs e)
        => ApplyFullscreen(false);

    private void ApplyFullscreen(bool value)
    {
        if (_game is null) return;

        _game.options.Fullscreen = value;
        FullscreenChanged?.Invoke(this, value);
        SetToggle(BtnFullscreenOn, BtnFullscreenOff, value);
    }

    // ── Server Textures ───────────────────────────────────────────────────────
    // Mirrors GraphicsHandleClick(graphicsUseServerTexturesOption).
    // Texture reload on next map connect — no immediate redraw needed.

    private void OnServerTexturesOnClicked(object sender, EventArgs e)
        => ApplyServerTextures(true);

    private void OnServerTexturesOffClicked(object sender, EventArgs e)
        => ApplyServerTextures(false);

    private void ApplyServerTextures(bool value)
    {
        if (_game is null) return;

        _game.options.UseServerTextures = value;
        SetToggle(BtnServerTexOn, BtnServerTexOff, value);
    }

    // ── Sound ─────────────────────────────────────────────────────────────────
    // Mirrors OtherHandleClick(otherSoundOption).

    private void OnSoundOnClicked(object sender, EventArgs e)
        => ApplySound(true);

    private void OnSoundOffClicked(object sender, EventArgs e)
        => ApplySound(false);

    private void ApplySound(bool value)
    {
        if (_game is null) return;

        _game.AudioEnabled = value;
        SetToggle(BtnSoundOn, BtnSoundOff, value);
    }

    // ── Auto Jump ─────────────────────────────────────────────────────────────
    // Mirrors OtherHandleClick(otherAutoJumpOption).

    private void OnAutoJumpOnClicked(object sender, EventArgs e)
        => ApplyAutoJump(true);

    private void OnAutoJumpOffClicked(object sender, EventArgs e)
        => ApplyAutoJump(false);

    private void ApplyAutoJump(bool value)
    {
        if (_game is null) return;

        _game.AutoJumpEnabled = value;
        SetToggle(BtnAutoJumpOn, BtnAutoJumpOff, value);
    }

    // ── Resolution stepper ────────────────────────────────────────────────────
    // Stores the index in options.Resolution so SaveOptions() picks it up.
    // Actual platform resolution change (ChangeResolution / SetWindowState) is
    // only meaningful in fullscreen mode and requires IGameWindowService — that
    // call lives in GameView. Here we just persist the selection.

    private void OnResolutionPrevClicked(object sender, EventArgs e)
        => StepResolution(-1);

    private void OnResolutionNextClicked(object sender, EventArgs e)
        => StepResolution(+1);

    private void StepResolution(int delta)
    {
        _resolutionIndex = (_resolutionIndex + delta + Resolutions.Length) % Resolutions.Length;
        LblResolution.Text = Resolutions[_resolutionIndex];

        if (_game is not null)
            _game.options.Resolution = _resolutionIndex;
    }
}