public class ModScreenshot : ModBase
{
    public ModScreenshot()
    {
        takeScreenshot = false;
        screenshotFlashFramesLeft = 0;
    }

    private bool takeScreenshot;
    private int screenshotFlashFramesLeft;

    public override void OnNewFrameDraw2d(Game game, float deltaTime)
    {
        if (takeScreenshot)
        {
            takeScreenshot = false;
            // Must be done after rendering, but before SwapBuffers
            game.platform.SaveScreenshot();
            screenshotFlashFramesLeft = 5;
        }
        if (screenshotFlashFramesLeft > 0)
        {
            DrawScreenshotFlash(game);
            screenshotFlashFramesLeft--;
        }
    }

    internal static void DrawScreenshotFlash(Game game)
    {
        game.Draw2dTexture(game.WhiteTexture(), 0, 0, game.platform.GetCanvasWidth(), game.platform.GetCanvasHeight(), null, 0, Game.ColorFromArgb(255, 255, 255, 255), false);
        string screenshottext = "&0Screenshot";
        game.platform.TextSize(screenshottext, 50, out int textWidth, out int textHeight);
        FontCi font = new()
        {
            family = "Arial",
            size = 50
        };
        game.Draw2dText(screenshottext, font, game.xcenter(textWidth), game.ycenter(textHeight), null, false);
    }
    
    public override void OnKeyDown(Game game, KeyEventArgs args)
    {
        if (args.GetKeyCode() == game.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.F12))
        {
            takeScreenshot = true;
            args.SetHandled(true);
        }
    }
}
