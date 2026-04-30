using ManicDigger;
using ManicDigger.Mods;

public partial class Game
{
    // ── Startup constants ─────────────────────────────────────────────────────

    /// <summary>Default view distance in blocks on fast (desktop) hardware.</summary>
    private const int ViewDistanceFast = 128;

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
        BlockTypeRegistry gamedata = new();
        gamedata.Start();
        BlockRegistry = gamedata;

        Config3d config3d = new()
        {
            ViewDistance = GameService.IsFastSystem() ? ViewDistanceFast : ViewDistanceSlow
        };
        Config3d = config3d;

        // ── Rendering subsystems ──────────────────────────────────────────────

        FrustumCulling = new() { CameraMatrix = CameraMatrix };

        TerrainChunkTesselator = new TerrainChunkTesselator(this, GameService);

        Batcher = new MeshBatcher(OpenGlService, this);

        SunMoonRenderer = new(this);

        // ── World / map ───────────────────────────────────────────────────────
        VoxelMap.Reset(DefaultMapSizeX, DefaultMapSizeY, DefaultMapSizeZ);
        Heightmap = new ChunkedMap2d<int>(VoxelMap.MapSizeX, VoxelMap.MapSizeY);

        // ── Inventory ─────────────────────────────────────────────────────────
        Packet_Inventory inventory = new() { RightHand = new InventoryItem[10] };
        InventoryService dataItems = new(this);
        Inventory = inventory;
        InventoryUtil = new InventoryUtilClient(inventory, dataItems);

        // ── Misc ──────────────────────────────────────────────────────────────
        rnd = new Random();
        GameService.AddOnCrash(OnCrashHandlerLeave.Create(this));

        // ── Mods ──────────────────────────────────────────────────────────────
        InitMods();

        BlockOctreeSearcher = new();

        // Prevent the loading screen from immediately showing the lag symbol.
        LastReceivedMilliseconds = GameService.TimeMillisecondsFromStart;
        EnableDrawTestCharacter = GameService.IsDebuggerAttached();

        int detectedSize = OpenGlService.GlGetMaxTextureSize();
        maxTextureSize = Math.Max(detectedSize, 1024);

        taskScheduler.Initialise();
        MapLoadingStart();
    }

    private void InitMods()
    {
        ClientMods = [];

        // ── Core loop ─────────────────────────────────────────────────────────
        AddMod(new ModDrawMain(this));
        AddMod(new ModUpdateMain(this));
        AddMod(new ModNetworkProcess(this, GameService));
        AddMod(new ModNetworkEntity(this));
        AddMod(new ModUnloadRendererChunks(this, GameService));
        AddMod(new ModDiagLog(this));

        // ── Camera ────────────────────────────────────────────────────────────
        AddMod(new ModAutoCamera(this, GameService));
        AddMod(new ModCameraKeys(this, GameService));
        AddMod(new ModCamera(this));

        // ── Player logic ──────────────────────────────────────────────────────
        AddMod(new ModFallDamageToPlayer(this, GameService));
        AddMod(new ModBlockDamageToPlayer(this, GameService));
        AddMod(new ModLoadPlayerTextures(this, GameService));
        AddMod(new ModSendPosition(this, GameService));
        AddMod(new ModInterpolatePositions(this));
        AddMod(new ModPush(this));
        AddMod(new ModFly(this));

        // ── Gameplay mechanics ────────────────────────────────────────────────
        AddMod(new ModRail(this, GameService));
        AddMod(new ModCompass(this, GameService));
        AddMod(new ModGrenade(this));
        AddMod(new ModBullet(this));
        AddMod(new ModExpire(this));
        AddMod(new ModPicking(this, GameService));

        // ── Inventory / ammo ──────────────────────────────────────────────────
        AddMod(new ModReloadAmmo(this, GameService));
        AddMod(new ModSendActiveMaterial(this));
        AddMod(new ModGuiCrafting(this));
        AddMod(new ModGuiInventory(this, GameService));

        // ── Audio ─────────────────────────────────────────────────────────────
        AddMod(new ModWalkSound(this));
        AddMod(new ModAudio(this, AudioService));

        // ── Sky / environment (must precede terrain) ──────────────────────────
        if (GameService.IsFastSystem())
            AddMod(new ModSkySphereAnimated(this, OpenGlService));
        else
            AddMod(new ModSkySphereStatic(this, OpenGlService));
        AddMod(SunMoonRenderer);

        // ── World rendering ───────────────────────────────────────────────────
        AddMod(new ModDrawTerrain(this, GameService));
        AddMod(new ModDrawArea(this, OpenGlService));
        AddMod(new ModDrawSprites(this));
        AddMod(new ModDrawMinecarts(this));
        AddMod(new ModDrawLinesAroundSelectedBlock(this, OpenGlService));
        AddMod(new ModDebugChunk(this, OpenGlService));

        // ── Particle effects ──────────────────────────────────────────────────
        // Create the instance, store the field reference, then register as a mod
        // so that both the field and the entry in clientmods point to the same
        // object. Previously InitSubsystems created one instance and InitMods
        // added a second independent instance — the field reference was dead.
        particleEffectBlockBreak = new ModDrawParticleEffectBlockBreak();
        AddMod(particleEffectBlockBreak);

        // ── Entity / player rendering ─────────────────────────────────────────
        AddMod(new ModDrawPlayers(this, GameService));
        AddMod(new ModDrawPlayerNames(this));
        AddMod(new ModDrawTestModel(this, OpenGlService));
        AddMod(new ModClearInactivePlayersDrawInfo(this, GameService));

        // ── HUD / 2D overlay ──────────────────────────────────────────────────
        AddMod(new ModDrawHand2d(this, GameService));
        AddMod(new ModDrawHand3d(this, OpenGlService));
        AddMod(new ModDrawText(this));
        AddMod(new ModDraw2dMisc(this, OpenGlService, GameService, SinglePlayerService));
        AddMod(new ModFpsHistoryGraph(this, GameService));

        // ── GUI (topmost — rendered last) ─────────────────────────────────────
        AddMod(new ModDialog(this, GameService));
        AddMod(new ModGuiTouchButtons(this, GameService));
        AddMod(new ModGuiEscapeMenu(this, GameService, preferences));
        AddMod(new ModGuiMapLoading(this, GameService, SinglePlayerService));
        AddMod(new ModGuiPlayerStats(this, GameService));
        AddMod(new ModGuiChat(this, GameService));
        AddMod(new ModScreenshot(this, GameService));
    }

    private void InitRenderer()
    {
        OpenGlService.GlClearColorRgbaf(0, 0, 0, 1);

        if (Config3d.EnableBackfaceCulling)
        {
            OpenGlService.GlDepthMask(true);
            OpenGlService.GlEnableDepthTest();
            OpenGlService.GlCullFaceBack();
            OpenGlService.GlEnableCullFace();
        }
    }

    private void AddMod(ModBase mod)
    {
        ClientMods.Add(mod);
    }
}