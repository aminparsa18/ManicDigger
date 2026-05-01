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
    private readonly IOpenGlService platform;
    private readonly IMeshDrawer meshDrawer;

    public ModSkySphereStatic(IOpenGlService platform, IMeshDrawer meshDrawer)
    {
        this.platform = platform;
        this.meshDrawer = meshDrawer;
    }

    public override void OnNewFrameDraw3d(IGame game, float deltaTime)
    {
        platform.GlDisableFog();
        DrawSkySphere(game);
        game.SetFog();
    }

    internal void DrawSkySphere(IGame game)
    {
        if (skySphereTexture == -1)
        {
            skySphereTexture = LoadTexture(game, "skysphere.png");
            skySphereNightTexture = LoadTexture(game, "skyspherenight.png");
        }

        // Simple shadows always use the day texture
        SkyTexture = (!game.SkySphereNight || game.shadowssimple)
            ? skySphereTexture
            : skySphereNightTexture;

        Draw(game, game.CurrentFov());
    }

    public void Draw(IGame game, float fov)
    {
        if (SkyTexture == -1)
            throw new InvalidOperationException($"error in {nameof(ModSkySphereStatic)} - {nameof(DrawSkySphere)}");

        skyModel ??= platform.CreateModel(Sphere.Create(SphereSize, SphereSize, SphereSegments, SphereSegments));

        game.Set3dProjection(SphereSize * 2, fov);
        meshDrawer.GLMatrixModeModelView();
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(game.Player.position.x, game.Player.position.y, game.Player.position.z);
        platform.BindTexture2d(SkyTexture);
        meshDrawer.DrawModel(skyModel);
        meshDrawer.GLPopMatrix();
        game.Set3dProjection(game.Zfar(), fov);
    }

    private int LoadTexture(IGame game, string filename)
    {
        Bitmap bmp = PixelBuffer.BitmapFromPng(game.GetAssetFile(filename), game.GetAssetFileLength(filename));
        int texture = platform.LoadTextureFromBitmap(bmp);
        bmp.Dispose();
        return texture;
    }
}