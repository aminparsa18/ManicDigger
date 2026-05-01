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

    public SunMoonRenderer(IMeshDrawer meshDrawer, IGame game) : base(game)
    {
        this.meshDrawer = meshDrawer;
    }

    public int GetHour() => hour;

    public void SetHour(int value)
    {
        hour = value;
        t = (hour - 6) / 24f * TwoPi;
    }

    public override void OnNewFrameDraw3d(float dt)
    {
        if (sunTexture == -1)
        {
            sunTexture = Game.GetTexture("sun.png");
            moonTexture = Game.GetTexture("moon.png");
        }

        UpdateSunMoonPosition(dt);

        float bodyX = (Game.isNight ? Game.moonPosition.X : Game.sunPosition.X) + Game.Player.position.x;
        float bodyY = (Game.isNight ? Game.moonPosition.Y : Game.sunPosition.Y) + Game.Player.position.y;
        float bodyZ = (Game.isNight ? Game.moonPosition.Z : Game.sunPosition.Z) + Game.Player.position.z;

        meshDrawer.GLMatrixModeModelView();
        meshDrawer.GLPushMatrix();
        meshDrawer.GLTranslate(bodyX, bodyY, bodyZ);
        VectorUtils.Billboard(meshDrawer);
        meshDrawer.GLScale(SpriteScale, SpriteScale, SpriteScale);
        Game.Draw2dTexture(Game.isNight ? moonTexture : sunTexture, 0, 0, ImageSize, ImageSize, null, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), false);
        meshDrawer.GLPopMatrix();
    }

    private void UpdateSunMoonPosition(float dt)
    {
        t += dt * TwoPi / day_length_in_seconds;

        Game.isNight = (t + TwoPi) % TwoPi > MathF.PI;

        Game.sunPosition = new OpenTK.Mathematics.Vector3(MathF.Cos(t) * OrbitRadius, MathF.Sin(t) * OrbitRadius, MathF.Sin(t) * OrbitRadius);
        Game.moonPosition = new OpenTK.Mathematics.Vector3(MathF.Cos(-t) * OrbitRadius, MathF.Sin(-t) * OrbitRadius, MathF.Sin(t) * OrbitRadius);
    }
}