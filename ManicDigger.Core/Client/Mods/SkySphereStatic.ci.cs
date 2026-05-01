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

    public ModSkySphereStatic(IOpenGlService platform, IMeshDrawer meshDrawer, IGame game) : base(game)
    {
        this.platform = platform;
        this.meshDrawer = meshDrawer;
    }

    public override void OnNewFrameDraw3d( float deltaTime)
    {
        platform.GlDisableFog();
        DrawSkySphere();
        Game.SetFog();
    }

    internal void DrawSkySphere()
    {
        if (skySphereTexture == -1)
        {
            skySphereTexture = LoadTexture( "skysphere.png");
            skySphereNightTexture = LoadTexture("skyspherenight.png");
        }

        // Simple shadows always use the day texture
        SkyTexture = (!Game.SkySphereNight || Game.shadowssimple)
            ? skySphereTexture
            : skySphereNightTexture;

        Draw(Game.CurrentFov());
    }

    public void Draw( float fov)
    {
        if (SkyTexture == -1)
            throw new InvalidOperationException($"error in {nameof(ModSkySphereStatic)} - {nameof(DrawSkySphere)}");

        skyModel ??= platform.CreateModel(Sphere.Create(SphereSize, SphereSize, SphereSegments, SphereSegments));

        Game.Set3dProjection(SphereSize * 2, fov);
        meshDrawer.GLMatrixModeModelView();
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(Game.Player.position.x, Game.Player.position.y, Game.Player.position.z);
        platform.BindTexture2d(SkyTexture);
        meshDrawer.DrawModel(skyModel);
        meshDrawer.GLPopMatrix();
        Game.Set3dProjection(Game.Zfar(), fov);
    }

    private int LoadTexture( string filename)
    {
        Bitmap bmp = PixelBuffer.BitmapFromPng(Game.GetAssetFile(filename), Game.GetAssetFileLength(filename));
        int texture = platform.LoadTextureFromBitmap(bmp);
        bmp.Dispose();
        return texture;
    }
}