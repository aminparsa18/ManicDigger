/// <summary>
/// Screen providing world-modification tools. Currently a stub — only the
/// Back button is implemented.
/// </summary>
public class ModifyWorldScreen : ScreenBase
{
    private readonly MenuWidget buttonBack;

    private string title = "Modify World";

    private readonly ILanguageService _languageService;
    private readonly IScreenManager _menu;

    public ModifyWorldScreen(IGameService platform, IOpenGlService openGlService, ILanguageService languageService, IAssetManager assetManager, IScreenManager menu)
        : base(platform, openGlService, assetManager)
    {
        _languageService = languageService;
        _menu = menu;
        buttonBack = new MenuWidget
        {
            text = "Back",
            type = UIWidgetType.Button
        };

        Widgets.Add(buttonBack);
    }

    /// <inheritdoc/>
    public override void LoadTranslations()
    {
        buttonBack.text = _languageService.Get("MainMenu_ButtonBack");
        title = _languageService.Get("MainMenu_ModifyWorld");
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        float scale = GetScale();

        DrawBackground();
        DrawText(title, 14 * scale, GameService.CanvasWidth / 2, 0, TextAlign.Center, TextBaseline.Top);

        buttonBack.x = 40 * scale;
        buttonBack.y = GameService.CanvasHeight - (104 * scale);
        buttonBack.sizex = 256 * scale;
        buttonBack.sizey = 64 * scale;
        buttonBack.fontSize = 14 * scale;

        DrawWidgets();
    }

    /// <inheritdoc/>
    public override void OnBackPressed() => _menu.StartSingleplayer();

    /// <inheritdoc/>
    public override void OnButton(MenuWidget w)
    {
        if (w == buttonBack)
        {
            OnBackPressed();
        }
    }
}