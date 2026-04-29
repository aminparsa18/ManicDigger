/// <summary>
/// Renders a static textured sky sphere, switching between day and night textures based on game state.
/// </summary>
public class ModSkySphereStatic : ModBase
{
    private const int SphereSize = 1000;
    private const int SphereSegments = 20;

    internal int SkyTexture = -1;
    private int skySphereTexture = -1;
    private int skySphereNightTexture = -1;
    private GeometryModel skyModel;
    private readonly IGameClient game;
    private readonly IGamePlatform platform;

    public ModSkySphereStatic(IGameClient game, IGamePlatform platform)
    {
        this.game = game;
        this.platform = platform;
    }

    public override void OnNewFrameDraw3d(float deltaTime)
    {
        platform.GlDisableFog();
        DrawSkySphere();
        game.SetFog();
    }

    internal void DrawSkySphere()
    {
        if (skySphereTexture == -1)
        {
            skySphereTexture = LoadTexture("skysphere.png");
            skySphereNightTexture = LoadTexture("skyspherenight.png");
        }

        // Simple shadows always use the day texture
        SkyTexture = (!game.SkySphereNight || game.shadowssimple)
            ? skySphereTexture
            : skySphereNightTexture;

        Draw(game.CurrentFov());
    }

    public void Draw(float fov)
    {
        if (SkyTexture == -1)
            throw new InvalidOperationException($"error in {nameof(ModSkySphereStatic)} - {nameof(DrawSkySphere)}");

        skyModel ??= platform.CreateModel(Sphere.Create(SphereSize, SphereSize, SphereSegments, SphereSegments));

        game.Set3dProjection(SphereSize * 2, fov);
        game.GLMatrixModeModelView();
        game.GLPushMatrix();
        game.GLTranslate(game.Player.position.x, game.Player.position.y, game.Player.position.z);
        platform.BindTexture2d(SkyTexture);
        game.DrawModel(skyModel);
        game.GLPopMatrix();
        game.Set3dProjection(game.Zfar(), fov);
    }

    private int LoadTexture(string filename)
    {
        Bitmap bmp = PixelBuffer.BitmapFromPng(game.GetAssetFile(filename), game.GetAssetFileLength(filename));
        int texture = platform.LoadTextureFromBitmap(bmp);
        bmp.Dispose();
        return texture;
    }
}