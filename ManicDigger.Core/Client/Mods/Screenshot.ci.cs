/// <summary>
/// Handles screenshot capture on F12 and displays a brief flash overlay after taking one.
/// </summary>
public class ModScreenshot : ModBase
{
    private const int FlashFrames = 5;
    private const int FlashFontSize = 50;
    private const string ScreenshotText = "&0Screenshot";

    private static readonly Font FlashFont = new Font("Arial", FlashFontSize);
    private static readonly int White = ColorUtils.ColorFromArgb(255, 255, 255, 255);

    private bool takeScreenshot;
    private int screenshotFlashFramesLeft;

    private readonly IGameService platform;

    public ModScreenshot(IGameService platform)
    {
        this.platform = platform;
    }

    public override void OnNewFrameDraw2d(IGame game, float deltaTime)
    {
        if (takeScreenshot)
        {
            takeScreenshot = false;
            platform.SaveScreenshot(); // Must be done after rendering, before SwapBuffers
            screenshotFlashFramesLeft = FlashFrames;
        }

        if (screenshotFlashFramesLeft > 0)
        {
            DrawScreenshotFlash(game);
            screenshotFlashFramesLeft--;
        }
    }

    public override void OnKeyDown(IGame game, KeyEventArgs args)
    {
        if (args.KeyChar != game.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.F12)) return;
        takeScreenshot = true;
        args.Handled = true;
    }

    internal void DrawScreenshotFlash(IGame game)
    {
        game.Draw2dTexture(game.GetOrCreateWhiteTexture(), 0, 0, platform.CanvasWidth, platform.CanvasHeight, null, 0, White, false);
        TextRenderer.TextSize(ScreenshotText, FlashFontSize, out int textWidth, out int textHeight);
        game.Draw2dText(ScreenshotText, FlashFont, game.Xcenter(textWidth), game.Ycenter(textHeight), null, false);
    }
}