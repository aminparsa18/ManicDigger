using OpenTK.Graphics.ES30;
using OpenTK.Mathematics;
using SkiaSharp.Views.Maui;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ManicDigger.Maui.Views;

public partial class GameView : ContentPage
{
    private bool _glInitialized = false;
    private IDispatcherTimer _gameLoopTimer;
    private DateTime _lastFrame = DateTime.UtcNow;

    private readonly IOpenGlService _openGlService;
    private readonly IGameWindowService _gameWindowService;
    private readonly IAssetManager _assetManager;

    private Matrix4 mvMatrix = Matrix4.Identity;
    private Matrix4 pMatrix = Matrix4.Identity;
    private GeometryModel cubeModel;

    private readonly MenuWidget buttonSingleplayer;
    private readonly MenuWidget buttonMultiplayer;
    private readonly MenuWidget buttonExit;

    /// <summary>Current canvas width, updated every frame. Exposed for external layout queries.</summary>
    internal float windowX;

    /// <summary>Current canvas height, updated every frame. Exposed for external layout queries.</summary>
    internal float windowY;

    private bool queryStringChecked;
    private bool cursorLoaded;

    private float xRot;
    private bool xInv;
    public float xSpeed;

    private float yRot;
    private bool yInv;
    public float ySpeed;

    private const int ButtonHeight = 64;
    private const int ButtonWidth = 256;
    private const int SpaceBetween = 5;
    private const int OffsetFromBorder = 50;

    [DllImport("libEGL.dll")]
    private static extern IntPtr eglGetProcAddress(string procName);

    private class AngleBindingsContext : OpenTK.IBindingsContext
    {
        public IntPtr GetProcAddress(string procName) => eglGetProcAddress(procName);
    }

    public GameView(IOpenGlService openGlService, IGameWindowService gameWindowService, IAssetManager assetManager)
    {
        InitializeComponent();
        _openGlService = openGlService;
        _gameWindowService = gameWindowService;
        _assetManager = assetManager;

        buttonSingleplayer = new MenuWidget { Text = "Singleplayer" };
        buttonMultiplayer = new MenuWidget { Text = "Multiplayer" };
        buttonExit = new MenuWidget { Text = "Quit" };

        Widgets.Add(buttonSingleplayer);
        Widgets.Add(buttonMultiplayer);
        Widgets.Add(buttonExit);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _gameLoopTimer = Dispatcher.CreateTimer();
        _gameLoopTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 fps
        _gameLoopTimer.Tick += (_, _) => GlView.InvalidateSurface();
        _gameLoopTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _gameLoopTimer?.Stop();
        _gameLoopTimer = null;
    }

    private void GlView_PaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
    {
        if (!_glInitialized)
        {
            _gameWindowService.AddOnNewFrame(Draw);

            GL.LoadBindings(new AngleBindingsContext());
            InitGL();
            _glInitialized = true;
            _gameWindowService.Start();
        }

        // Compute delta time
        var now = DateTime.UtcNow;
        float dt = (float)(now - _lastFrame).TotalSeconds;
        _lastFrame = now;

        Draw(dt);
    }

    private void InitGL()
    {
        _openGlService.InitShaders();
        _openGlService.GlClearColorRgbaf(0, 0, 0, 1);
        _openGlService.GlEnableDepthTest();
    }

    private void Draw(float dt)
    {
        _openGlService.GlViewport(0, 0, (int)GlView.CanvasSize.Width, (int)GlView.CanvasSize.Height);
        _openGlService.GlClearColorBufferAndDepthBuffer();
        _openGlService.GlDisableDepthTest();
        _openGlService.GlDisableCullFace();

        Matrix4.CreateOrthographicOffCenter(
            0, GlView.CanvasSize.Width,
            GlView.CanvasSize.Height, 0,
            0, 10,
            out pMatrix);

        Render();
    }

    public void Render()
    {
        windowX = GlView.CanvasSize.Width;
        windowY = GlView.CanvasSize.Height;

        float scale = GetScale();

        //if (AssetManager.AssetsLoadProgress != 1)
        //{
        //    string s = string.Format(_languageService.Get("MainMenu_AssetsLoadProgress"),
        //        ((int)(AssetManager.AssetsLoadProgress * 100)).ToString());
        //    DrawText(s, 20 * scale, windowX / 2, windowY / 2, TextAlign.Center, TextBaseline.Middle);
        //    return;
        //}

        if (!cursorLoaded)
        {
            //_gameWindowService.SetWindowCursor(0, 0, 32, 32,
            //    GetFile("mousecursor.png"),
            //    GetFileLength("mousecursor.png"));
            cursorLoaded = true;
        }

        DrawBackground();
        Draw2dQuad(GetTexture("logo.png"),
            (windowX / 2) - (1024 * scale / 2), 0, 1024 * scale, 512 * scale);

        float centerX = (windowX / 2) - (ButtonWidth / 2 * scale);

        buttonSingleplayer.X = centerX;
        buttonSingleplayer.Y = ButtonY(3, scale);
        buttonSingleplayer.Sizex = ButtonWidth * scale;
        buttonSingleplayer.Sizey = ButtonHeight * scale;

        buttonMultiplayer.X = centerX;
        buttonMultiplayer.Y = ButtonY(2, scale);
        buttonMultiplayer.Sizex = ButtonWidth * scale;
        buttonMultiplayer.Sizey = ButtonHeight * scale;

        buttonExit.Visible = true;
        buttonExit.X = centerX;
        buttonExit.Y = ButtonY(1, scale);
        buttonExit.Sizex = ButtonWidth * scale;
        buttonExit.Sizey = ButtonHeight * scale;

        DrawWidgets();
    }

    private float ButtonY(int slot, float scale)
        => windowY - (slot * ((ButtonHeight * scale) + SpaceBetween)) - (OffsetFromBorder * scale);

    public float GetScale()
      => _gameWindowService.IsSmallScreen()
          ? GlView.CanvasSize.Width / 1280f
          : 1f;

    private int BackgroundW;
    private int BackgroundH;

    private int overlap = 200;

    /// <summary>Fixed background tile size in pixels.</summary>
    private const int BackgroundTileSize = 512;

    /// <summary>Maximum delta-time cap per frame to avoid physics tunnelling on hitches.</summary>
    private const float MaxDeltaTime = 1f;

    protected void DrawBackground()
    {
        BackgroundW = BackgroundTileSize;
        BackgroundH = BackgroundTileSize;

        int countX = (int)(GlView.CanvasSize.Width + (2 * overlap)) / BackgroundW + 1;
        int countY = (int)(GlView.CanvasSize.Height + (2 * overlap)) / BackgroundH + 1;

        int bgTexture = GetTexture("background.png");
        for (int x = 0; x < countX; x++)
        {
            for (int y = 0; y < countY; y++)
            {
                Draw2dQuad(
                    bgTexture,
                    (x * BackgroundW) + xRot - overlap,
                    (y * BackgroundH) + yRot - overlap,
                    BackgroundW, BackgroundH);
            }
        }
    }

    protected Dictionary<string, int> Textures { get; set; } = [];

    protected int GetTexture(string name)
    {
        if (!Textures.TryGetValue(name, out int value))
        {
            Bitmap bmp = PixelBuffer.BitmapFromPng(GetFile(name), GetFileLength(name));
            value = _openGlService.LoadTextureFromBitmap(bmp);
            Textures[name] = value;
            bmp.Dispose();
        }

        return value;
    }

    protected byte[] GetFile(string name)
    {
        string lower = name.ToLowerInvariant();
        for (int i = 0; i < _assetManager.Assets.Count; i++)
        {
            if (_assetManager.Assets[i].name == lower)
            {
                return _assetManager.Assets[i].data;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the byte length of the named asset's data, or 0 if not found.
    /// </summary>
    /// <param name="name">Asset filename.</param>
    protected int GetFileLength(string name)
    {
        string lower = name.ToLowerInvariant();
        for (int i = 0; i < _assetManager.Assets.Count; i++)
        {
            if (_assetManager.Assets[i].name == lower)
            {
                return _assetManager.Assets[i].dataLength;
            }
        }

        return 0;
    }

    protected List<MenuWidget> Widgets { get; set; } = [];

    public void DrawWidgets()
    {
        foreach (MenuWidget w in Widgets)
        {
            if (!w.Visible)
            {
                continue;
            }

            string text = w.Selected ? string.Concat("&2", w.Text) : w.Text;

            if (w.Type == UIWidgetType.Button)
            {
                DrawButton(w, text);
            }
            else if (w.Type == UIWidgetType.Textbox)
            {
                DrawTextbox(w, text);
            }
        }
    }

    private void DrawTextbox(MenuWidget w, string text)
    {
        if (w.Password)
        {
            text = new string('*', w.Text.Length);
        }

        if (w.Editing)
        {
            text = string.Concat(text, "_");
        }

        if (w.ButtonStyle == ButtonStyle.Text)
        {
            if (w.Image != null)
            {
                Draw2dQuad(GetTexture(w.Image), w.X, w.Y, w.Sizex, w.Sizey);
            }

            DrawText(text, w.FontSize, w.X, w.Y, TextAlign.Left, TextBaseline.Top);
        }
        else
        {
            DrawButton(text, w.FontSize, w.X, w.Y, w.Sizex, w.Sizey,
                w.Hover || w.Editing || w.HasKeyboardFocus);
        }

        if (w.Description != null)
        {
            DrawText(w.Description, w.FontSize, w.X, w.Y + (w.Sizey / 2), TextAlign.Right, TextBaseline.Middle);
        }
    }

    private void DrawButton(string text, float fontSize, float dx, float dy, float dw, float dh, bool pressed)
    {
        string textureName = pressed ? "button_sel.png" : "button.png";
        Draw2dQuad(GetTexture(textureName), dx, dy, dw, dh);

        if (!string.IsNullOrEmpty(text))
        {
            DrawText(text, fontSize, dx + (dw / 2), dy + (dh / 2), TextAlign.Center, TextBaseline.Middle);
        }
    }

    /// <summary>
    /// Renders a string at the given canvas position using a cached rasterised texture.
    /// </summary>
    /// <param name="text">The string to render.</param>
    /// <param name="fontSize">Font size in points.</param>
    /// <param name="x">Horizontal anchor in canvas pixels.</param>
    /// <param name="y">Vertical anchor in canvas pixels.</param>
    /// <param name="align">Horizontal alignment relative to <paramref name="x"/>.</param>
    /// <param name="baseline">Vertical alignment relative to <paramref name="y"/>.</param>
    protected void DrawText(string text, float fontSize, float x, float y, TextAlign align, TextBaseline baseline)
    {
        TextTexture t = GetTextTexture(text, fontSize);

        int dx = align switch
        {
            TextAlign.Center => -t.TextWidth / 2,
            TextAlign.Right => -t.TextWidth,
            _ => 0
        };

        int dy = baseline switch
        {
            TextBaseline.Middle => -t.TextHeight / 2,
            TextBaseline.Bottom => -t.TextHeight,
            _ => 0
        };

        Draw2dQuad(t.Texture, x + dx, y + dy, t.TextureWidth, t.TextureHeight);
    }

    private readonly Dictionary<(string Text, float Size), TextTexture> textTextureCache = [];

    private TextTexture GetTextTexture(string text, float fontSize)
    {
        if (textTextureCache.TryGetValue((text, fontSize), out TextTexture cached))
        {
            return cached;
        }

        TextStyle style = new()
        {
            Text = text,
            FontSize = fontSize,
            FontFamily = "Arial",
            Color = ColorUtils.ColorFromArgb(255, 255, 255, 255)
        };

        Bitmap textBitmap = TextColorRenderer.CreateTextTexture(style);
        int texture = _openGlService.LoadTextureFromBitmap(textBitmap);
        TextRenderer.TextSize(text, fontSize, out int textWidth, out int textHeight);

        TextTexture entry = new()
        {
            Texture = texture,
            TextureWidth = textBitmap.Width,
            TextureHeight = textBitmap.Height,
            Text = text,
            Size = fontSize,
            TextWidth = textWidth,
            TextHeight = textHeight
        };

        textBitmap.Dispose();
        textTextureCache[(text, fontSize)] = entry;
        return entry;
    }

    /// <summary>
    /// Draws a single button widget using the style indicated by <see cref="MenuWidget.ButtonStyle"/>.
    /// </summary>
    private void DrawButton(MenuWidget w, string text)
    {
        switch (w.ButtonStyle)
        {
            case ButtonStyle.Text:
                if (w.Image != null)
                {
                    Draw2dQuad(GetTexture(w.Image), w.X, w.Y, w.Sizex, w.Sizey);
                }

                DrawText(text, w.FontSize, w.X, w.Y + (w.Sizey / 2), TextAlign.Left, TextBaseline.Middle);
                break;

            case ButtonStyle.Button:
                DrawButton(text, w.FontSize, w.X, w.Y, w.Sizex, w.Sizey, w.Hover || w.HasKeyboardFocus);
                if (w.Description != null)
                {
                    DrawText(w.Description, w.FontSize, w.X, w.Y + (w.Sizey / 2), TextAlign.Right, TextBaseline.Middle);
                }

                break;

            default:
                // Server-list entry: text is packed as five newline-separated fields:
                //   [0] server name  [1] player count  [2] map name  [3] game mode  [4] server version
                string[] fields = w.Text.Split('\n');

                if (w.Selected)
                {
                    fields[0] = string.Concat("&2", fields[0]);
                    fields[1] = string.Concat("&2", fields[1]);
                    fields[2] = string.Concat("&2", fields[2]);
                    fields[3] = string.Concat("&2", fields[3]);
                }

                DrawServerButton(fields[0], fields[1], fields[2], fields[3],
                    w.X, w.Y, w.Sizex, w.Sizey, w.Image);

                if (w.Description != null)
                {
                    // Server did not respond to the last ping — show a warning icon.
                    Draw2dQuad(GetTexture("serverlist_entry_noresponse.png"),
                        w.X - (38 * GetScale()), w.Y, w.Sizey / 2, w.Sizey / 2);
                }

                if (fields[4] != _gameWindowService.GetGameVersion())
                {
                    // Server version differs from the client — show a version-mismatch icon.
                    Draw2dQuad(GetTexture("serverlist_entry_differentversion.png"),
                        w.X - (38 * GetScale()), w.Y + (w.Sizey / 2), w.Sizey / 2, w.Sizey / 2);
                }

                break;
        }
    }

    protected void Draw2dQuad(int textureid, float dx, float dy, float dw, float dh)
    {
        // Build model-view: translate → scale to size → halve → shift origin to centre
        Matrix4.CreateTranslation(dx, dy, 0, out Matrix4 translation);
        Matrix4.CreateScale(dw, dh, 0, out Matrix4 scale);
        Matrix4.CreateScale(0.5f, 0.5f, 0, out Matrix4 halfScale);
        Matrix4.CreateTranslation(1, 1, 0, out Matrix4 centreShift);

        mvMatrix = centreShift * halfScale * scale * translation;

        SetMatrixUniforms();
        cubeModel ??= _openGlService.CreateModel(Quad.Create());
        _openGlService.BindTexture2d(textureid);
        _openGlService.DrawModel(cubeModel);
    }

    public void DrawServerButton(
       string name, string motd, string gamemode, string playercount,
       float x, float y, float width, float height, string image)
    {
        // Background panel (screen-width minus margin × 64 px by convention)
        Draw2dQuad(GetTexture("serverlist_entry_background.png"), x, y, width, height);
        // Square icon on the left; height × height keeps it 1:1
        Draw2dQuad(GetTexture(image), x, y, height, height);

        //         value          size   xPos                    yPos                    align               baseline
        DrawText(name, 14, x + 70, y + 5, TextAlign.Left, TextBaseline.Top);
        DrawText(gamemode, 12, x + width - 10, y + height - 5, TextAlign.Right, TextBaseline.Bottom);
        DrawText(playercount, 12, x + width - 10, y + 5, TextAlign.Right, TextBaseline.Top);
        DrawText(motd, 12, x + 70, y + height - 5, TextAlign.Left, TextBaseline.Bottom);
    }

    private void SetMatrixUniforms()
    {
        _openGlService.SetMatrixUniformProjection(ref pMatrix);
        _openGlService.SetMatrixUniformModelView(ref mvMatrix);
    }
}