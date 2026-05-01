using ManicDigger;

/// <summary>
/// Renders the player's held item as a 2D hand image overlay in first-person view.
/// </summary>
public class ModDrawHand2d : ModBase
{
    private string lastHandImage;
    private readonly IGameService platform;
    private readonly IMeshDrawer meshDrawer;
    private readonly IOpenGlService openGlService;

    public ModDrawHand2d(IGameService platform, IMeshDrawer meshDrawer, IOpenGlService openGlService, IGame game) : base(game)
    {
        this.platform = platform;
        this.meshDrawer = meshDrawer;
        this.openGlService = openGlService;
    }

    public override void OnNewFrameDraw3d(float deltaTime)
    {
        if (!ShouldDrawHand())
        {
            return;
        }

        string img = HandImage2d();
        if (img == null)
        {
            return;
        }

        meshDrawer.OrthoMode(platform.CanvasWidth, platform.CanvasHeight);

        if (lastHandImage != img)
        {
            lastHandImage = img;
            byte[] file = Game.GetAssetFile(img);
            Bitmap bmp = PixelBuffer.BitmapFromPng(file, file.Length);
            if (bmp != null)
            {
                Game.handTexture = openGlService.LoadTextureFromBitmap(bmp);
                bmp.Dispose();
            }
        }

        Game.Draw2dTexture(Game.handTexture, platform.CanvasWidth / 2, platform.CanvasHeight - 512, 512, 512, null, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), false);
        meshDrawer.PerspectiveMode();
    }

    /// <summary>Returns true if the hand should be drawn (first-person view with 2D enabled).</summary>
    public bool ShouldDrawHand() => !Game.EnableTppView && Game.ENABLE_DRAW2D;

    /// <summary>Returns the appropriate hand image path for the currently held item, or null if none.</summary>
    public string HandImage2d()
    {
        InventoryItem item = Game.Inventory.RightHand[Game.ActiveMaterial];
        if (item == null)
        {
            return null;
        }

        return Game.IronSights
            ? Game.BlockTypes[item.BlockId].IronSightsImage
            : Game.BlockTypes[item.BlockId].handimage;
    }
}