using ManicDigger;
using OpenTK.Mathematics;

public class ModDraw2dMisc : ModBase
{
    private readonly IOpenGlService platformOpenGl;
    private readonly IGameService platform;
    private readonly ISinglePlayerService singlePlayerService;
    private readonly IVoxelMap voxelMap;
    private readonly IBlockTypeRegistry blockTypeRegistry;

    public ModDraw2dMisc(IOpenGlService platformOpenGl, IGameService platform, ISinglePlayerService singlePlayerService,
        IVoxelMap voxelMap, IBlockTypeRegistry blockTypeRegistry, IGame game) : base(game)
    {
        this.platformOpenGl = platformOpenGl;
        this.platform = platform;
        this.singlePlayerService = singlePlayerService;
        this.voxelMap = voxelMap;
        this.blockTypeRegistry = blockTypeRegistry;
    }

    public override void OnNewFrameDraw2d(float deltaTime)
    {
        if (Game.GuiState == GuiState.Normal)
            DrawAim(Game);

        if (Game.GuiState != GuiState.MapLoading)
        {
            DrawEnemyHealthBlock(Game);
            DrawAmmo(Game);
            DrawLocalPosition();
            DrawBlockInfo(Game);
        }

        DrawMouseCursor(Game);
        DrawDisconnected();
    }

    // ── Block / entity health display ─────────────────────────────────────────

    public void DrawBlockInfo(IGame game)
    {
        if (!game.DrawBlockInfo) return;

        int x = game.SelectedBlockPositionX;
        int y = game.SelectedBlockPositionZ;
        int z = game.SelectedBlockPositionY;

        if (!voxelMap.IsValidPos(x, y, z)) return;

        int blocktype = voxelMap.GetBlock(x, y, z);
        if (!game.IsValid(blocktype)) return;

        game.CurrentAttackedBlock = new Vector3i(x, y, z);
        DrawEnemyHealthBlock(game);
    }

    internal void DrawEnemyHealthBlock(IGame game)
    {
        if (game.CurrentAttackedBlock != null)
        {
            int x = game.CurrentAttackedBlock.Value.X;
            int y = game.CurrentAttackedBlock.Value.Y;
            int z = game.CurrentAttackedBlock.Value.Z;
            int blocktype = voxelMap.GetBlock(x, y, z);
            float health = game.GetCurrentBlockHealth(x, y, z);
            float progress = health / blockTypeRegistry.Strength[blocktype];

            // Cache the translated name — used in up to two calls below.
            string name = game.Language.Get("Block_" + game.BlockTypes[blocktype].Name);

            if (Game.IsUsableBlock(blocktype))
                DrawEnemyHealthUseInfo(name, progress, true);

            DrawEnemyHealthBackground(name);
        }

        if (Game.CurrentlyAttackedEntity != -1)
        {
            Entity e = Game.Entities[Game.CurrentlyAttackedEntity];
            if (e == null) return;

            float health = e.playerStats != null
                ? (float)e.playerStats.CurrentHealth / e.playerStats.MaxHealth
                : 1f;

            string name = e.drawName?.Name ?? "Unknown";
            string translatedName = game.Language.Get(name);

            if (e.usable)
                DrawEnemyHealthUseInfo(translatedName, health, useInfo: true);

            DrawEnemyHealthBackground(translatedName);
        }
    }

    /// <summary>
    /// Draws the full-width background bar and name label (no progress, no use hint).
    /// Replaces the old <c>DrawEnemyHealthCommon</c> which accepted a <c>progress</c>
    /// parameter it silently ignored.
    /// </summary>
    internal void DrawEnemyHealthBackground(string name)
        => DrawEnemyHealthUseInfo(name, progress: 1f, useInfo: false);

    internal void DrawEnemyHealthUseInfo(string name, float progress, bool useInfo)
    {
        int barHeight = useInfo ? 55 : 35;
        int whiteTexId = Game.GetOrCreateWhiteTexture(); // cache — was called twice

        Game.Draw2dTexture(whiteTexId, Game.Xcenter(300), 40, 300, barHeight, null, 0,
            ColorUtils.ColorFromArgb(255, 0, 0, 0), false);
        Game.Draw2dTexture(whiteTexId, Game.Xcenter(300), 40, 300 * progress, barHeight, null, 0,
            ColorUtils.ColorFromArgb(255, 255, 0, 0), false);

        TextRenderer.TextSize(name, 14, out int w, out _);
        Game.Draw2dText1(name, Game.Xcenter(w), 40, 14, null, false);
        if (useInfo)
        {
            string hint = string.Format(Game.Language.PressToUse(), "E");
            TextRenderer.TextSize(hint, 10, out w, out _);
            Game.Draw2dText1(hint, Game.Xcenter(w), 70, 10, null, false);
        }
    }

    // ── Crosshair ─────────────────────────────────────────────────────────────

    internal void DrawAim(IGame game)
    {
        if (game.CameraType == CameraType.Overhead) return;

        const int AimSize = 32;
        platformOpenGl.BindTexture2d(0);

        if (game.CurrentAimRadius() > 1)
        {
            float fov_ = game.CurrentFov();
            game.Circle3i(platform.CanvasWidth / 2, platform.CanvasHeight / 2,
                game.CurrentAimRadius() * game.CurrentFov() / fov_);
        }

        game.Draw2dBitmapFile("target.png",
            platform.CanvasWidth / 2 - AimSize / 2,
            platform.CanvasHeight / 2 - AimSize / 2,
            AimSize, AimSize);
    }

    // ── Mouse cursor ──────────────────────────────────────────────────────────

    internal void DrawMouseCursor(IGame game)
    {
        if (!game.GetFreeMouse()) return;
        if (!platform.MouseCursorIsVisible())
            game.Draw2dBitmapFile("mousecursor.png", game.MouseCurrentX, game.MouseCurrentY, 32, 32);
    }

    // ── Ammo counter ──────────────────────────────────────────────────────────

    internal void DrawAmmo(IGame game)
    {
        InventoryItem item = game.Inventory.RightHand[game.ActiveMaterial];
        if (item == null || item.InventoryItemType != InventoryItemType.Block) return;
        if (!game.BlockTypes[item.BlockId].IsPistol) return;

        int loaded = game.LoadedAmmo[item.BlockId];
        int total = game.TotalAmmo[item.BlockId];
        string s = $"{loaded}/{total - loaded}";
        int color = loaded == 0
            ? ColorUtils.ColorFromArgb(255, 255, 0, 0)
            : ColorUtils.ColorFromArgb(255, 255, 255, 255);

        game.Draw2dText1(s, platform.CanvasWidth - game.TextSizeWidth(s, 18) - 50,
            platform.CanvasWidth - game.TextSizeHeight(s, 18) - 50, 18, color, false);

        if (loaded == 0)
        {
            const string PressR = "Press R to reload"; // TODO: move to game.language
            game.Draw2dText1(PressR,
                platform.CanvasWidth - game.TextSizeWidth(PressR, 14) - 50,
                platform.CanvasHeight - game.TextSizeHeight(s, 14) - 80,
                14, ColorUtils.ColorFromArgb(255, 255, 0, 0), false);
        }
    }

    // ── Debug position overlay ────────────────────────────────────────────────

    private void DrawLocalPosition()
    {
        if (!Game.EnableDrawPosition) return;

        float heading = EncodingHelper.HeadingByte(
            Game.Player.position.rotx, Game.Player.position.roty, Game.Player.position.rotz);
        float pitch = EncodingHelper.PitchByte(
            Game.Player.position.rotx, Game.Player.position.roty, Game.Player.position.rotz);

        // Single interpolated string replaces seven string.Concat calls.
        string postext =
            $"X: {MathF.Floor(Game.Player.position.x)},\t" +
            $"Y: {MathF.Floor(Game.Player.position.z)},\t" +
            $"Z: {MathF.Floor(Game.Player.position.y)}\n" +
            $"Heading: {MathF.Floor(heading)}\n" +
            $"Pitch: {MathF.Floor(pitch)}";

        Game.Draw2dText1(postext, 100, 460, GameConstants.ChatFontSize, null, false);
    }

    // ── Disconnected overlay ──────────────────────────────────────────────────

    private void DrawDisconnected()
    {
        float lagSeconds =
            (platform.TimeMillisecondsFromStart - Game.LastReceivedMilliseconds) / 1000f;

        if (lagSeconds < GameConstants.DISCONNECTED_ICON_AFTER_SECONDS)
            return;
        if (lagSeconds >= 60 * 60 * 24)
            return;
        if (Game.InvalidVersionDrawMessage != null)
            return;
        if (Game.IsSinglePlayer && !singlePlayerService.SinglePlayerServerLoaded)
            return;

        Game.Draw2dBitmapFile("disconnected.png", platform.CanvasWidth - 100, 50, 50, 50);

        Game.Draw2dText1(((int)lagSeconds).ToString(),
            platform.CanvasWidth - 100, 50 + 50 + 10, 12, null, false);

        const string Reconnect = "Press F6 to reconnect";
        Game.Draw2dText1(Reconnect,
            platform.CanvasWidth / 2 - 200 / 2, 50, 12, null, false);
    }
}