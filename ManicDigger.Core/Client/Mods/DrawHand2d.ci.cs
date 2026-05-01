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

    public ModDrawHand2d(IGameService platform, IMeshDrawer meshDrawer, IOpenGlService openGlService)
    {
        this.platform = platform;
        this.meshDrawer = meshDrawer;
        this.openGlService = openGlService;
    }

    public override void OnNewFrameDraw3d(IGame game, float deltaTime)
    {
        if (!ShouldDrawHand(game)) return;

        string img = HandImage2d(game);
        if (img == null) return;

        meshDrawer.OrthoMode(platform.CanvasWidth, platform.CanvasHeight);

        if (lastHandImage != img)
        {
            lastHandImage = img;
            byte[] file = game.GetAssetFile(img);
            Bitmap bmp = PixelBuffer.BitmapFromPng(file, file.Length);
            if (bmp != null)
            {
                game.handTexture = openGlService.LoadTextureFromBitmap(bmp);
                bmp.Dispose();
            }
        }

        game.Draw2dTexture(game.handTexture, platform.CanvasWidth / 2, platform.CanvasHeight - 512, 512, 512, null, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), false);
        meshDrawer.PerspectiveMode();
    }

    /// <summary>Returns true if the hand should be drawn (first-person view with 2D enabled).</summary>
    public bool ShouldDrawHand(IGame game) => !game.EnableTppView && game.ENABLE_DRAW2D;

    /// <summary>Returns the appropriate hand image path for the currently held item, or null if none.</summary>
    public string HandImage2d(IGame game)
    {
        InventoryItem item = game.Inventory.RightHand[game.ActiveMaterial];
        if (item == null) return null;

        return game.IronSights
            ? game.BlockTypes[item.BlockId].IronSightsImage
            : game.BlockTypes[item.BlockId].handimage;
    }
}