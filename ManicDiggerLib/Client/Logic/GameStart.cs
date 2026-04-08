using ManicDigger.Mods;

public partial class Game
{
    

    public void Start()
    {
        InitSubsystems();
        InitMods();
        InitRenderer();
    }

    // -------------------------------------------------------------------------
    // Start helpers
    // -------------------------------------------------------------------------

    private void InitSubsystems()
    {
        textColorRenderer = new TextColorRenderer { platform = platform };
        language.LoadTranslations();

        GameData gamedata = new();
        gamedata.Start();
        d_Data = gamedata;
        d_DataMonsters = new GameDataMonsters();

        Config3d config3d = new();
        config3d.viewdistance = platform.IsFastSystem() ? 128 : 32;
        d_Config3d = config3d;

        ITerrainTextures terrainTextures = new() { game = this };
        d_TerrainTextures = terrainTextures;
        d_TextureAtlasConverter = new TextureAtlasConverter();

        FrustumCulling frustumculling = new() { d_GetCameraMatrix = CameraMatrix };
        d_FrustumCulling = frustumculling;

        TerrainChunkTesselatorCi terrainchunktesselator = new();
        terrainchunktesselator.game = this;
        d_TerrainChunkTesselator = terrainchunktesselator;

        d_Batcher = new MeshBatcher
        {
            d_FrustumCulling = frustumculling,
            game = this
        };

        particleEffectBlockBreak = new ModDrawParticleEffectBlockBreak();

        map.Reset(256, 256, 128);

        SunMoonRenderer sunmoonrenderer = new();
        d_SunMoonRenderer = sunmoonrenderer;

        d_Heightmap = new InfiniteMapChunked2d { d_Map = this };
        d_Heightmap.Restart();

        Packet_Inventory inventory = new() { RightHand = new Packet_Item[10] };
        InventoryUtils dataItems = new(this);
        d_Inventory = inventory;
        d_InventoryUtil = new InventoryUtilClient(inventory, dataItems);

        platform.AddOnCrash(OnCrashHandlerLeave.Create(this));

        rnd = new Random();
        s = new();

        // Prevent loading screen from immediately displaying lag symbol.
        LastReceivedMilliseconds = platform.TimeMillisecondsFromStart();
        ENABLE_DRAW_TEST_CHARACTER = platform.IsDebuggerAttached();

        int detectedSize = platform.GlGetMaxTextureSize();
        maxTextureSize = Math.Max(detectedSize, 1024);

        taskScheduler.Initialise(this);
        MapLoadingStart();
    }

    private void InitMods()
    {
        clientmods = new ModBase[128];
        clientmodsCount = 0;
        modmanager.game = this;

        // Core update loop
        AddMod(new ModDrawMain());
        AddMod(new ModUpdateMain());
        AddMod(new ModNetworkProcess());
        AddMod(new ModUnloadRendererChunks());

        // Camera
        AddMod(new ModAutoCamera());
        AddMod(new ModCameraKeys());
        AddMod(new ModCamera());

        // Player
        AddMod(new ModLoadPlayerTextures());
        AddMod(new ModSendPosition());
        AddMod(new ModInterpolatePositions());
        AddMod(new ModFallDamageToPlayer());
        AddMod(new ModBlockDamageToPlayer());
        AddMod(new ModPush());

        // Inventory / crafting
        AddMod(new ModSendActiveMaterial());
        AddMod(new ModGuiInventory());
        AddMod(new ModGuiCrafting());
        AddMod(new ModReloadAmmo());

        // Gameplay mechanics
        AddMod(new ModRail());
        AddMod(new ModGrenade());
        AddMod(new ModBullet());
        AddMod(new ModExpire());
        AddMod(new ModPicking());
        AddMod(new ModNetworkEntity());
        AddMod(new ModCompass());

        // World rendering
        AddMod(new ModDrawTerrain());
        AddMod(new ModDrawArea());
        AddMod(new ModDrawLinesAroundSelectedBlock());
        AddMod(new ModDebugChunk());
        AddMod(new ModDrawSprites());
        AddMod(new ModDrawMinecarts());
        AddMod(new ModDrawParticleEffectBlockBreak());

        // Sky / environment
        if (platform.IsFastSystem())
            AddMod(new ModSkySphereAnimated());
        else
            AddMod(new ModSkySphereStatic());
        AddMod(d_SunMoonRenderer);

        // Entity / player rendering
        AddMod(new ModDrawPlayers());
        AddMod(new ModDrawPlayerNames());
        AddMod(new ModDrawTestModel());
        AddMod(new ModClearInactivePlayersDrawInfo());

        // HUD / 2D overlay
        AddMod(new ModDrawHand2d());
        AddMod(new ModDrawHand3d());
        AddMod(new ModDrawText());
        AddMod(new ModDraw2dMisc());
        AddMod(new ModFpsHistoryGraph());

        // GUI dialogs
        AddMod(new ModDialog());
        AddMod(new ModGuiTouchButtons());
        AddMod(new ModGuiEscapeMenu());
        AddMod(new ModGuiMapLoading());
        AddMod(new ModGuiPlayerStats());
        AddMod(new ModGuiChat());

        // Misc
        AddMod(new ModWalkSound());
        AddMod(new ModAudio());
        AddMod(new ModScreenshot());
    }

    private void InitRenderer()
    {
        platform.GlClearColorRgbaf(0, 0, 0, 1);

        if (d_Config3d.ENABLE_BACKFACECULLING)
        {
            platform.GlDepthMask(true);
            platform.GlEnableDepthTest();
            platform.GlCullFaceBack();
            platform.GlEnableCullFace();
        }

        platform.GlEnableLighting();
        platform.GlEnableColorMaterial();
        platform.GlColorMaterialFrontAndBackAmbientAndDiffuse();
        platform.GlShadeModelSmooth();
    }

    public void AddMod(ModBase mod)
    {
        clientmods[clientmodsCount++] = mod;
        mod.Start(modmanager);
    }
}