/// <summary>
/// Screen that shows available singleplayer save files and lets the player
/// launch, create, or modify a world.
/// </summary>
/// <remarks>
/// When the platform does not support singleplayer (i.e. web/mobile), all world
/// widgets are hidden and an explanatory message is displayed instead.
/// </remarks>
public class SingleplayerScreen : ScreenBase, ISingleplayerScreen
{
    // Maximum number of save-file buttons shown in the list.
    private const int MaxWorldButtons = 10;

    private readonly MenuWidget play;
    private readonly MenuWidget newWorld;
    private readonly MenuWidget modify;
    private readonly MenuWidget back;
    private readonly MenuWidget open;

    /// <summary>Dynamically populated buttons, one per discovered save file (up to <see cref="MaxWorldButtons"/>).</summary>
    private readonly MenuWidget[] worldButtons;

    private readonly IScreenManager _menu;
    private readonly ISinglePlayerService singlePlayerService;
    private readonly ILanguageService _languageService;

    /// <summary>
    /// Save files discovered on first render. <c>null</c> until the first call to
    /// <see cref="Render"/> so that the file scan is deferred until the screen is actually shown.
    /// </summary>
    private List<string> savegames;

    private string title;

    public SingleplayerScreen(IGameService platform, IOpenGlService platformOpenGl, IAssetManager assetManager,
        ISinglePlayerService singlePlayerService, ILanguageService languageService, IScreenManager menu) : base(platform, platformOpenGl, assetManager)
    {
        play = new MenuWidget
        {
            text = "Play"
        };
        newWorld = new MenuWidget
        {
            text = "New World"
        };
        modify = new MenuWidget
        {
            text = "Modify"
        };
        back = new MenuWidget
        {
            text = "Back",
            type = UIWidgetType.Button
        };
        open = new MenuWidget
        {
            text = "Create or open...",
            type = UIWidgetType.Button
        };

        title = "Singleplayer";

        Widgets.Add(play);
        Widgets.Add(newWorld);
        Widgets.Add(modify);
        Widgets.Add(back);
        Widgets.Add(open);

        worldButtons = new MenuWidget[MaxWorldButtons];
        for (int i = 0; i < MaxWorldButtons; i++)
        {
            worldButtons[i] = new MenuWidget { visible = false };
            Widgets.Add(worldButtons[i]);
        }

        this._menu = menu;
        this._languageService = languageService;
        this.singlePlayerService = singlePlayerService;
    }

    /// <inheritdoc/>
    public override void LoadTranslations()
    {
        back.text = _languageService.Get("MainMenu_ButtonBack");
        open.text = _languageService.Get("MainMenu_SingleplayerButtonCreate");
        title = _languageService.Get("MainMenu_Singleplayer");
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        float scale = GetScale();

        DrawBackground();
        DrawText(title, 20 * scale, GameService.CanvasWidth / 2, 10, TextAlign.Center, TextBaseline.Top);

        float leftx = (GameService.CanvasWidth / 2) - (128 * scale);
        float y = (GameService.CanvasHeight / 2) + (0 * scale);

        play.x = leftx;
        play.y = y + (100 * scale);
        play.sizex = 256 * scale;
        play.sizey = 64 * scale;
        play.fontSize = 14 * scale;

        newWorld.x = leftx;
        newWorld.y = y + (170 * scale);
        newWorld.sizex = 256 * scale;
        newWorld.sizey = 64 * scale;
        newWorld.fontSize = 14 * scale;

        modify.x = leftx;
        modify.y = y + (240 * scale);
        modify.sizex = 256 * scale;
        modify.sizey = 64 * scale;
        modify.fontSize = 14 * scale;

        back.x = 40 * scale;
        back.y = GameService.CanvasHeight - (104 * scale);
        back.sizex = 256 * scale;
        back.sizey = 64 * scale;
        back.fontSize = 14 * scale;

        open.x = leftx;
        open.y = y + (0 * scale);
        open.sizex = 256 * scale;
        open.sizey = 64 * scale;
        open.fontSize = 14 * scale;

        // Deferred scan: only read the filesystem once, on the first render.
        savegames ??= GetSaveGames();

        // Reset all world buttons, then re-enable one per discovered save file.
        for (int i = 0; i < MaxWorldButtons; i++)
        {
            worldButtons[i].visible = false;
        }

        for (int i = 0; i < savegames.Count; i++)
        {
            worldButtons[i].visible = true;
            worldButtons[i].text = Path.GetFileNameWithoutExtension(savegames[i]);
            worldButtons[i].x = leftx;
            worldButtons[i].y = 100 + (100 * scale * i);
            worldButtons[i].sizex = 256 * scale;
            worldButtons[i].sizey = 64 * scale;
            worldButtons[i].fontSize = 14 * scale;
        }

        // Only the Open button is active on supporting platforms.
        // Play, NewWorld, Modify, and worldButtons are reserved for a future
        // save-file browser and are hidden until that UI is implemented.
        open.visible = singlePlayerService.SinglePlayerServerAvailable;
        play.visible = false;
        newWorld.visible = false;
        modify.visible = false;
        for (int i = 0; i < savegames.Count; i++)
        {
            worldButtons[i].visible = false;
        }

        DrawWidgets();

        if (!singlePlayerService.SinglePlayerServerAvailable)
        {
            DrawText(
                "Singleplayer is only available on desktop (Windows, Linux, Mac) version of game.",
                16 * scale, GameService.CanvasWidth / 2, GameService.CanvasHeight / 2,
                TextAlign.Center, TextBaseline.Middle);
        }
    }

    // -------------------------------------------------------------------------
    // Save-game helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans <see cref="GameService.GameSavePath"/> for <c>.mddbs</c> files
    /// and returns their paths. Files without the expected extension are excluded.
    /// </summary>
    /// <returns>List of fully-qualified paths to every discovered <c>.mddbs</c> save file.</returns>
    private List<string> GetSaveGames()
    {
        string[] files = FileHelper.DirectoryGetFiles(GameService.GameSavePath);
        List<string> savegames = [];

        foreach (string file in files)
        {
            if (file.EndsWith(FileConstatns.DbFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                savegames.Add(file);
            }
        }

        return savegames;
    }

    private static string FileOpenDialog(string extension, string extensionName, string initialDirectory)
    {
        OpenFileDialog d = new()
        {
            InitialDirectory = initialDirectory,
            FileName = "Default." + extension,
            Filter = string.Format("{1}|*.{0}|All files|*.*", extension, extensionName),
            CheckFileExists = false,
            CheckPathExists = true
        };
        string dir = Environment.CurrentDirectory;
        DialogResult result = d.ShowDialog();
        Environment.CurrentDirectory = dir;
        return result == DialogResult.OK ? d.FileName : null;
    }

    /// <inheritdoc/>
    public override void OnBackPressed() => _menu.StartMainMenu();

    /// <inheritdoc/>
    public override void OnButton(MenuWidget w)
    {
        for (int i = 0; i < MaxWorldButtons; i++)
        {
            worldButtons[i].selected = false;
        }

        for (int i = 0; i < MaxWorldButtons; i++)
        {
            if (worldButtons[i] == w)
            {
                worldButtons[i].selected = true;
            }
        }

        if (w == back)
        {
            OnBackPressed();
        }

        if (w == open)
        {
            string extension = singlePlayerService.SinglePlayerServerAvailable ? "mddbs" : "mdss";
            string result = FileOpenDialog(extension, "Manic Digger Savegame", GameService.GameSavePath);
            if (result != null)
            {
                _menu.ConnectToSingleplayer(result);
            }
        }
    }
}