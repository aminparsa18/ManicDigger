/// <summary>
/// Displays an FPS counter and frame-time history graph.
/// Toggle with F7 or the "fps" command.
/// </summary>
public class ModFpsHistoryGraph : ModBase
{
    private const int MaxCount = 300;
    private const int ChatFontSize = 11;
    private const int GraphHeight = 80;
    private const int GraphPosX = 25;
    private const int PerLine = 2;

    private readonly IGameService _platform;

    private readonly float[] dtHistory = new float[MaxCount];
    private readonly Draw2dData[] todraw = new Draw2dData[MaxCount];

    private int lasttitleUpdateMilliseconds;
    private int fpsCount;
    private string fpsText;
    private float longestFrameDt;
    private bool drawFpsText;
    private bool drawFpsGraph;

    public ModFpsHistoryGraph(IGameService platform)
    {
        _platform = platform;

        for (int i = 0; i < MaxCount; i++)
            todraw[i] = new Draw2dData();
    }
    
    /// <inheritdoc/>
    public override void OnNewFrame(IGame game, float dt)
    {
        UpdateGraph(dt);
        UpdateTitleFps(game, dt);
        Draw(game);
    }

    /// <inheritdoc/>
    public override void OnKeyDown(IGame game, KeyEventArgs args)
    {
        if (args.KeyChar == (int)Keys.F7)
        {
            drawFpsText = !drawFpsGraph;
            drawFpsGraph = !drawFpsGraph;
        }
    }

    /// <inheritdoc/>
    public override bool OnClientCommand(IGame game, ClientCommandArgs args)
    {
        if (args.command != "fps") return false;

        (drawFpsText, drawFpsGraph) = args.arguments.Trim() switch
        {
            "" or "1" => (true, false),
            "2" => (true, true),
            _ => (false, false)
        };
        return true;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>Shifts frame-time history left and appends the latest dt.</summary>
    private void UpdateGraph(float dt)
    {
        Array.Copy(dtHistory, 1, dtHistory, 0, MaxCount - 1);
        dtHistory[MaxCount - 1] = dt;
    }

    /// <summary>Updates the FPS counter and performance info string once per second.</summary>
    private void UpdateTitleFps(IGame _game, float dt)
    {
        fpsCount++;
        longestFrameDt = Math.Max(longestFrameDt, dt);

        int now = _platform.TimeMillisecondsFromStart;
        float elapsed = (now - lasttitleUpdateMilliseconds) / 1000f;
        if (elapsed < 1f) return;

        lasttitleUpdateMilliseconds = now;

        int fps = (int)(fpsCount / elapsed);
        int minFps = (int)(1f / longestFrameDt);

        longestFrameDt = 0;
        fpsCount = 0;

        _game.PerformanceInfo["fps"] = $"FPS: {fps} (min: {minFps})";

        var sb = new System.Text.StringBuilder();
        int idx = 0;
        foreach (string value in _game.PerformanceInfo.Values)
        {
            sb.Append(value);
            if (idx % PerLine == 0 && idx != _game.PerformanceInfo.Count - 1)
                sb.Append(", ");
            else if (idx % PerLine != 0)
                sb.Append('\n');
            idx++;
        }
        fpsText = sb.ToString();
    }

    private void Draw(IGame _game)
    {
        if (!drawFpsGraph && !drawFpsText) return;

        _game.OrthoMode(_platform.CanvasWidth, _platform.CanvasHeight);
        if (drawFpsGraph) DrawGraph(_game);
        if (drawFpsText) _game.Draw2dText(fpsText, new Font("Arial", ChatFontSize), 20, 20, null, false);
        _game.PerspectiveMode();
    }

    private void DrawGraph(IGame _game)
    {
        int posx = GraphPosX;
        int posy = _platform.CanvasHeight - GraphHeight - 20;

        int[] colors =
        [
            ColorUtils.ColorFromArgb(255, 0,   0, 0),
            ColorUtils.ColorFromArgb(255, 255, 0, 0)
        ];

        int whiteTexture = _game.GetOrCreateWhiteTexture();

        for (int i = 0; i < MaxCount; i++)
        {
            float barHeight = dtHistory[i] * 60 * GraphHeight;
            todraw[i].x1 = posx + i;
            todraw[i].y1 = posy - barHeight;
            todraw[i].width = 1;
            todraw[i].height = barHeight;
            todraw[i].inAtlasId = -1;
            todraw[i].color = ColorUtils.InterpolateColor((float)i / MaxCount, colors, 2);
        }
        _game.Draw2dTextures(todraw, MaxCount, whiteTexture);

        // Reference FPS lines
        int lineColor = ColorUtils.ColorFromArgb(255, 255, 255, 255);
        DrawFpsLine(_game, posy, 30, lineColor);
        DrawFpsLine(_game, posy, 60, lineColor);
        DrawFpsLine(_game, posy, 75, lineColor);
        DrawFpsLine(_game, posy, 150, lineColor);
    }

    /// <summary>Draws a horizontal reference line at the given target FPS level.</summary>
    private void DrawFpsLine(IGame _game, int posy, int fps, int color)
    {
        int whiteTexture = _game.GetOrCreateWhiteTexture();
        float y = posy - GraphHeight * (60f / fps);

        _game.Draw2dTexture(whiteTexture, GraphPosX, (int)y, MaxCount, 1, -1, 0, color, false);
        _game.Draw2dText(fps.ToString(), new Font("Arial", 6), GraphPosX, y, null, false);
    }
}