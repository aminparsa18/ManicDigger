/// <summary>
/// Renders the sun and moon as billboarded sprites and updates their positions based on time of day.
/// </summary>
public class SunMoonRenderer : ModBase
{
    private const float TwoPi = 2 * MathF.PI;
    private const float OrbitRadius = 20f;
    private const float SpriteScale = 0.02f;

    internal int ImageSize = 96;
    internal float day_length_in_seconds = 30f;

    private int hour = 6;
    private float t;
    private int sunTexture = -1;
    private int moonTexture = -1;

    private readonly IMeshDrawer meshDrawer;

    public SunMoonRenderer(IMeshDrawer meshDrawer)
    {
        this.meshDrawer = meshDrawer;
    }

    public int GetHour() => hour;

    public void SetHour(int value)
    {
        hour = value;
        t = (hour - 6) / 24f * TwoPi;
    }

    public override void OnNewFrameDraw3d(IGame game, float dt)
    {
        if (sunTexture == -1)
        {
            sunTexture = game.GetTexture("sun.png");
            moonTexture = game.GetTexture("moon.png");
        }

        UpdateSunMoonPosition(game, dt);

        float bodyX = (game.isNight ? game.moonPosition.X : game.sunPosition.X) + game.Player.position.x;
        float bodyY = (game.isNight ? game.moonPosition.Y : game.sunPosition.Y) + game.Player.position.y;
        float bodyZ = (game.isNight ? game.moonPosition.Z : game.sunPosition.Z) + game.Player.position.z;

        meshDrawer.GLMatrixModeModelView();
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(bodyX, bodyY, bodyZ);
        VectorUtils.Billboard(meshDrawer);
        meshDrawer.GLScale(SpriteScale, SpriteScale, SpriteScale);
        game.Draw2dTexture(game.isNight ? moonTexture : sunTexture, 0, 0, ImageSize, ImageSize, null, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), false);
        meshDrawer.GLPopMatrix();
    }

    private void UpdateSunMoonPosition(IGame game, float dt)
    {
        t += dt * TwoPi / day_length_in_seconds;

        game.isNight = (t + TwoPi) % TwoPi > MathF.PI;

        game.sunPosition = new OpenTK.Mathematics.Vector3(MathF.Cos(t) * OrbitRadius, MathF.Sin(t) * OrbitRadius, MathF.Sin(t) * OrbitRadius);
        game.moonPosition = new OpenTK.Mathematics.Vector3(MathF.Cos(-t) * OrbitRadius, MathF.Sin(-t) * OrbitRadius, MathF.Sin(t) * OrbitRadius);
    }
}