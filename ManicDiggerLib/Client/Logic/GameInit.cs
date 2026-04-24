using OpenTK.Mathematics;
using System.Collections.Concurrent;

/// <summary>
/// Partial class containing field declarations and constructor initialization
/// for the core Game state. Fields owned by other subsystem partials are
/// initialized via their own partial-class initializer methods called from here.
/// </summary>
public partial class Game
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const int MaxBlockTypes = 1024;

    public const int HourDetail = 4;
    public const int ChatFontSize = 11;
    public const int DISCONNECTED_ICON_AFTER_SECONDS = 10;

    internal const int chunksize = 16;
    internal const int chunksizebits = 4;
    internal const int speculativeMax = 8 * 1024;

    public const int minlight = 0;
    public const int maxlight = 15;

    public const int entityMonsterIdStart = 128;
    public const int entityMonsterIdCount = 128;
    public const int entityLocalIdStart = 256;

    public const int KeyAltLeft = 5;
    public const int KeyAltRight = 6;

    // -------------------------------------------------------------------------
    // Fields — rendering / textures
    // -------------------------------------------------------------------------

    internal List<Asset> assets;
    internal float assetsLoadProgress;
    internal TextRenderer textRenderer;

    /// <summary>Texture IDs indexed by [blockId][TileSide].</summary>
    public int[][] TextureId { get; set; }
    public int[] TextureIdForInventory { get; set; }

    internal int terrainTexture;
    public int[] TerrainTextures1d { get; set; }

    /// <summary>Maximum texture size detected at runtime.</summary>
    internal int maxTextureSize;
    internal int Atlas1dheight() => maxTextureSize;

    internal static int TexturesPacked => GlobalVar.MAX_BLOCKTYPES_SQRT; // 16x16
    internal static int Atlas2DTiles => GlobalVar.MAX_BLOCKTYPES_SQRT;   // 16x16

    internal int handTexture;
    public bool HandRedraw { get; set; }
    internal bool handSetAttackBuild;
    internal bool handSetAttackDestroy;

    internal int whitetexture;
    internal Dictionary<TextStyle, CachedTexture> cachedTextTextures = [];
    internal Dictionary<string, int> textures;
    internal List<string> AllowedFonts;

    // -------------------------------------------------------------------------
    // Fields — world / map
    // -------------------------------------------------------------------------

    public VoxelMap VoxelMap { get; set; }
    public ChunkedMap2d<int> Heightmap { get; set; }
    public Config3d Config3d { get; set; }

    public int LastplacedblockX { get; set; }
    public int LastplacedblockY { get; set; }
    public int LastplacedblockZ { get; set; }

    // -------------------------------------------------------------------------
    // Fields — player
    // -------------------------------------------------------------------------

    public Entity Player { get; set; }

    public float PlayerPositionSpawnX { get; set; }
    public float PlayerPositionSpawnY { get; set; }
    public float PlayerPositionSpawnZ { get; set; }

    internal bool isplayeronground;

    internal float pushX;
    internal float pushY;
    internal float pushZ;

    private float lastplayerpositionX;
    private float lastplayerpositionY;
    private float lastplayerpositionZ;

    internal bool reachedwall;
    internal bool reachedwall_1blockhigh;
    internal bool reachedHalfBlock;
    internal float movedz;

    internal float constWallDistance;
    internal float constRotationSpeed;
    internal AnimationHint localplayeranimationhint;
    internal bool enable_move;

    internal bool stopPlayerMove;
    public GuiState GuiState { get; set; }

    internal byte localstance;
    public bool Spawned {  get; set; }
    internal bool IsShiftPressed;
    internal int playertexturedefault;
    public const string playertexturedefaultfilename = "mineplayer.png";

    internal int reloadblock;
    internal int reloadstartMilliseconds;
    internal int lastOxygenTickMilliseconds;
    public int LastReceivedMilliseconds { get; set; }

    public int LocalPlayerId { get; set; }
    internal int currentlyAttackedEntity;
    internal int selectedmodelid;
    internal Vector3 playervelocity;
    internal float RadiusWhenMoving;
    public float Basemovespeed { get; set; }
    public float MoveSpeed { get; set; }
    internal float rotationspeed;
    internal float PICK_DISTANCE;
    internal int grenadetime;
    internal int PlayerPushDistance;
    internal bool AudioEnabled;
    internal bool AutoJumpEnabled;
    public int[] TotalAmmo { get; set; }
    public int[] LoadedAmmo { get; set; }
    internal Dictionary<(int x, int y, int z), float> blockHealth = new();
    public VisibleDialog[] Dialogs { get; set; }
    internal List<string> typinglog;

    internal bool IronSights;
    internal Random rnd;
    internal Vector3i? currentAttackedBlock;
    public int CurrentTimeMilliseconds { get; set; }
    internal int totaltimeMilliseconds;
    public int ReceivedMapLength { get; set; }
    internal int maxdrawdistance;

    internal bool leftpressedpicking;
    internal int pistolcycle;
    internal int lastironsightschangeMilliseconds;
    internal int grenadecookingstartMilliseconds;
    internal int lastpositionsentMilliseconds;

    internal bool shadowssimple;
    public bool ShouldRedrawAllBlocks { get; set; }
    internal bool escapeMenuRestart;

    // -------------------------------------------------------------------------
    // Fields — camera
    // -------------------------------------------------------------------------

    internal Matrix4 camera;
    internal float CameraEyeX;
    internal float CameraEyeY;
    internal float CameraEyeZ;

    internal bool currentMatrixModeProjection;
    internal Stack<Matrix4> mvMatrix;
    internal Stack<Matrix4> pMatrix;

    internal CameraMatrixProvider CameraMatrix;
    internal float fov;
    internal CameraType cameratype;
    internal bool ENABLE_TPP_VIEW;
    internal float znear;
    internal bool ENABLE_ZFAR;
    internal float overheadcameradistance;
    internal Camera overheadcameraK;
    internal float tppcameradistance;
    internal float TPP_CAMERA_DISTANCE_MIN;
    internal float TPP_CAMERA_DISTANCE_MAX;
    internal bool enableCameraControl;
    internal Matrix4 identityMatrix;
    internal Matrix4 Set3dProjectionTempMat4;

    // -------------------------------------------------------------------------
    // Fields — lighting
    // -------------------------------------------------------------------------

    /// <summary>Maps light level (0–15) to a GL color multiplier.</summary>
    public float[] LightLevels { get; set; }
    public int Sunlight { get; set; }
    public int[] NightLevels { get; set; }

    internal float sunPositionX;
    internal float sunPositionY;
    internal float sunPositionZ;
    internal float moonPositionX;
    internal float moonPositionY;
    internal float moonPositionZ;
    internal bool isNight;
    internal bool fancySkysphere;
    public bool SkySphereNight { get; set; }
    internal ModSkySphereStatic skysphere;

    // -------------------------------------------------------------------------
    // Fields — input
    // -------------------------------------------------------------------------

    public Controls Controls { get; set; }
    internal bool mouseSmoothing;
    internal bool mouseleftclick;
    internal bool mouseleftdeclick;
    internal bool wasmouseleft;
    internal bool mouserightclick;
    internal bool mouserightdeclick;
    internal bool wasmouseright;
    internal bool[] keyboardState;
    internal bool[] keyboardStateRaw;

    internal bool mouseLeft;
    internal bool mouseMiddle;
    internal bool mouseRight;

    internal int mouseCurrentX;
    internal int mouseCurrentY;
    internal float mouseDeltaX;
    internal float mouseDeltaY;
    private float mouseSmoothingVelX;
    private float mouseSmoothingVelY;
    private float mouseSmoothingAccum;
    private bool mousePointerLockShouldBe;
    internal bool overheadcamera;

    internal float touchMoveDx;
    internal float touchMoveDy;
    internal float touchOrientationDx;
    internal float touchOrientationDy;

    internal bool drawblockinfo;

    // -------------------------------------------------------------------------
    // Fields — audio
    // -------------------------------------------------------------------------

    internal AudioControl audio;
    internal bool soundnow;

    // -------------------------------------------------------------------------
    // Fields — networking / server
    // -------------------------------------------------------------------------

    public ClientPacketHandler[] PacketHandlers { get; set; }
    public string ServerGameVersion { get; set; }

    public bool AmmoStarted { get; set; }
    public Packet_BlockType[] NewBlockTypes { get; set; }
    public Packet_BlockType[] Blocktypes { get; }

    public string BlobDownloadName { get; set; }
    public string BlobDownloadMd5 { get; set; }
    public MemoryStream BlobDownload { get; set; }

    internal ConnectionData connectdata;
    internal bool issingleplayer;
    internal bool reconnect;
    internal bool exitToMainMenu;

    // -------------------------------------------------------------------------
    // Fields — speculative block placement
    // -------------------------------------------------------------------------

    internal Speculative[] speculative;
    internal int speculativeCount;

    // -------------------------------------------------------------------------
    // Fields — subsystems
    // -------------------------------------------------------------------------

    internal List<ModBase> clientmods;

    public IGamePlatform Platform { get; set; }
    public Language Language { get; set; }
    internal FrustumCulling FrustumCulling;
    public TerrainChunkTesselator TerrainChunkTesselator { get; set; }
    public MeshBatcher Batcher { get; set; }
    public SunMoonRenderer SunMoonRenderer { get; set; }
    internal InventoryUtilClient d_InventoryUtil;
    internal ModDrawParticleEffectBlockBreak particleEffectBlockBreak;
    public BlockTypeRegistry BlockRegistry { get; set; }
    internal Packet_Inventory d_Inventory;

    internal int[] materialSlots;
    internal int Font;
    internal GameExit d_Exit;

    internal int typinglogpos;
    internal TypingState GuiTyping;

    internal bool ENABLE_DRAW_TEST_CHARACTER;
    internal bool ENABLE_DRAWPOSITION;
    internal bool ENABLE_DRAW2D;
    internal int ENABLE_LAG;
    public bool AllowFreeMove { get; set; }
    internal MenuState menustate;
    public ServerInformation ServerInfo { get; set; }
    internal GameOption options;
    internal Dictionary<string, string> performanceinfo;
    public Packet_ServerPlayerStats PlayerStats { get; set; }
    internal string[] getAsset;
    public int FillAreaLimit { get; set; }

    public List<Entity> Entities { get; set; }

    internal int ChatLinesMax;
    internal List<Chatline> ChatLines;

    internal MapLoadingProgressEventArgs maploadingprogress;
    internal Font fontMapLoading;
    public string InvalidVersionDrawMessage { get; set; }
    public Packet_Server InvalidVersionPacketIdentification { get; set;}

    internal Vector3 playerdestination;
    public string Follow { get; set; }
    private bool startedconnecting;

    private int lastWidth;
    private int lastHeight;

    private float accumulator;
    private TaskScheduler taskScheduler;

    internal ConcurrentQueue<Action> commitActions;

    internal int SelectedBlockPositionX;
    internal int SelectedBlockPositionY;
    internal int SelectedBlockPositionZ;
    internal int SelectedEntityId;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Game(IGamePlatform platform)
    {
        Platform = platform;
        InitCore();
        InitMap();
        InitTextures();
        InitPlayer();
        InitCamera();
        InitLighting();
        InitInput();
        InitOptions();
        InitNetworking();
        InitChat();
        InitAudio();
    }

    // -------------------------------------------------------------------------
    // Initialization helpers
    // -------------------------------------------------------------------------

    private void InitCore()
    {
        performanceinfo = [];
        Language = new Language();
        particleEffectBlockBreak = new ModDrawParticleEffectBlockBreak();
        ServerInfo = new ServerInformation();
        options = new GameOption();
        getAsset = new string[1024 * 2];
        identityMatrix = Matrix4.Identity;
        Set3dProjectionTempMat4 = Matrix4.Identity;
        PlayerStats = new Packet_ServerPlayerStats();
        taskScheduler = new TaskScheduler();
        commitActions = new();
        Entities = [];
    }

    private void InitMap()
    {
        VoxelMap = new VoxelMap();
        LastplacedblockX = -1;
        LastplacedblockY = -1;
        LastplacedblockZ = -1;
        FillAreaLimit = 200;
    }

    private void InitTextures()
    {
        TextureId = new int[MaxBlockTypes][];
        for (int i = 0; i < MaxBlockTypes; i++)
            TextureId[i] = new int[6];

        TextureIdForInventory = new int[MaxBlockTypes];
        handTexture = -1;
        whitetexture = -1;
        textures = [];
        cachedTextTextures = [];

        AllowedFonts = ["Verdana"];
    }

    private void InitPlayer()
    {
        Player = new Entity { position = new EntityPosition_() };
        LocalPlayerId = -1;
        currentlyAttackedEntity = -1;
        selectedmodelid = -1;
        playertexturedefault = -1;

        PlayerPositionSpawnX = 15.5f;
        PlayerPositionSpawnY = 64;
        PlayerPositionSpawnZ = 15.5f;

        playervelocity = new Vector3();
        movedz = 0;
        constWallDistance = 0.3f;
        constRotationSpeed = 180 / 20;
        RadiusWhenMoving = 3f / 10;
        Basemovespeed = 5;
        MoveSpeed = 5;
        rotationspeed = 15f / 100;
        PICK_DISTANCE = 4.1f;
        grenadetime = 3;
        PlayerPushDistance = 2;

        localplayeranimationhint = new AnimationHint();
        enable_move = true;
        AudioEnabled = true;
        AutoJumpEnabled = false;

        TotalAmmo = new int[GlobalVar.MAX_BLOCKTYPES];
        LoadedAmmo = new int[GlobalVar.MAX_BLOCKTYPES];
        blockHealth = [];
        Dialogs = new VisibleDialog[512];
        typinglog = [];
    }

    private void InitCamera()
    {
        camera = Matrix4.Identity;
        mvMatrix = new();
        pMatrix = new();
        mvMatrix.Push(Matrix4.Identity);
        pMatrix.Push(Matrix4.Identity);

        CameraEyeX = -1;
        CameraEyeY = -1;
        CameraEyeZ = -1;

        CameraMatrix = new CameraMatrixProvider();
        fov = MathF.PI / 3;
        cameratype = CameraType.Fpp;
        ENABLE_TPP_VIEW = false;
        znear = 1f / 10;
        ENABLE_ZFAR = true;
        overheadcameradistance = 10;
        overheadcameraK = new Camera();
        tppcameradistance = 3;
        TPP_CAMERA_DISTANCE_MIN = 1;
        TPP_CAMERA_DISTANCE_MAX = 10;
        enableCameraControl = true;
    }

    private void InitLighting()
    {
        Sunlight = 15;
        LightLevels = new float[16];
        for (int i = 0; i < 16; i++)
            LightLevels[i] = 0.15f;
    }

    private void InitInput()
    {
        Controls = new Controls();
        mouseSmoothing = true;
        mouseleftclick = false;
        mouseleftdeclick = false;
        wasmouseleft = false;
        mouserightclick = false;
        mouserightdeclick = false;
        wasmouseright = false;

        const int KeysMax = 360;
        keyboardState = new bool[KeysMax];
        for (int i = 0; i < KeysMax; i++)
            keyboardState[i] = false;

        keyboardStateRaw = new bool[KeysMax];
        for (int i = 0; i < KeysMax; i++)
            keyboardStateRaw[i] = false;
    }

    private void InitOptions()
    {
        ENABLE_DRAW2D = true;
        ENABLE_LAG = 0;
        AllowFreeMove = true;
        menustate = new MenuState();
    }

    private void InitNetworking()
    {
        PacketHandlers = new ClientPacketHandler[256];
        NewBlockTypes = new Packet_BlockType[GlobalVar.MAX_BLOCKTYPES];
        speculativeCount = 0;
        speculative = new Speculative[speculativeMax];
    }

    private void InitChat()
    {
        ChatLinesMax = 1;
        ChatLines = [];
    }

    private void InitAudio()
    {
        audio = new AudioControl();
    }
}