using ManicDigger;

public partial class Game
{
    // ── Startup constants ─────────────────────────────────────────────────────

    /// <summary>Default view distance in blocks on fast (desktop) hardware.</summary>
    private const int ViewDistanceFast = 512;

    /// <summary>Default view distance in blocks on slow (mobile/web) hardware.</summary>
    private const int ViewDistanceSlow = 32;

    /// <summary>Default map dimensions before the server sends its own size.</summary>
    private const int DefaultMapSizeX = 256;
    private const int DefaultMapSizeY = 256;
    private const int DefaultMapSizeZ = 128;

    // ── Entry point ───────────────────────────────────────────────────────────

    public void Start()
    {
        InitSubsystems();
        InitRenderer();
    }

    // ── Start helpers ─────────────────────────────────────────────────────────

    private void InitSubsystems()
    {
        // ── Text / language ───────────────────────────────────────────────────
        Language.LoadTranslations();

        // ── Core data / config ────────────────────────────────────────────────
        Config3d config3d = new()
        {
            ViewDistance = gameService.IsFastSystem() ? ViewDistanceFast : ViewDistanceSlow
        };
        Config3d = config3d;

        // ── Rendering subsystems ──────────────────────────────────────────────

        TerrainChunkTesselator = new TerrainChunkTesselator(this, gameService, _blockRegistry);

        // ── World / map ───────────────────────────────────────────────────────
        voxelMap.Reset(DefaultMapSizeX, DefaultMapSizeY, DefaultMapSizeZ);
        Heightmap = new ChunkedMap2d<int>(voxelMap.MapSizeX, voxelMap.MapSizeY);

        // ── Inventory ─────────────────────────────────────────────────────────
        Packet_Inventory inventory = new() { RightHand = new InventoryItem[10] };
        InventoryService dataItems = new(this, _blockRegistry);
        Inventory = inventory;
        InventoryUtil = new InventoryUtilClient(inventory, dataItems);

        // ── Misc ──────────────────────────────────────────────────────────────
        rnd = new Random();
        gameService.AddOnCrash(OnCrashHandlerLeave.Create(this));

        // Prevent the loading screen from immediately showing the lag symbol.
        LastReceivedMilliseconds = gameService.TimeMillisecondsFromStart;

        int detectedSize = openGlService.GlGetMaxTextureSize();
        maxTextureSize = Math.Max(detectedSize, 1024);

        taskScheduler.Initialise();
        MapLoadingStart();
    }

    private void InitRenderer()
    {
        openGlService.GlClearColorRgbaf(0, 0, 0, 1);

        if (Config3d.EnableBackfaceCulling)
        {
            openGlService.GlDepthMask(true);
            openGlService.GlEnableDepthTest();
            openGlService.GlCullFaceBack();
            openGlService.GlEnableCullFace();
        }
    }
}