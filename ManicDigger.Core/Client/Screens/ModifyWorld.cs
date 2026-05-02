/// <summary>
/// Screen providing world-modification tools. Currently a stub — only the
/// Back button is implemented.
/// </summary>
public class ModifyWorldScreen : ScreenBase
{
    private readonly MenuWidget buttonBack;

    private string title = "Modify World";

    public ModifyWorldScreen(IMenu navigator, IGameService platform)
        : base(navigator, platform)
    {
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
        buttonBack.text = Menu.Translate("MainMenu_ButtonBack");
        title = Menu.Translate("MainMenu_ModifyWorld");
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        float scale = Menu.GetScale();

        Menu.DrawBackground();
        Menu.DrawText(title, 14 * scale, GameService.CanvasWidth / 2, 0, TextAlign.Center, TextBaseline.Top);

        buttonBack.x = 40 * scale;
        buttonBack.y = GameService.CanvasHeight - (104 * scale);
        buttonBack.sizex = 256 * scale;
        buttonBack.sizey = 64 * scale;
        buttonBack.fontSize = 14 * scale;

        DrawWidgets();
    }

    /// <inheritdoc/>
    public override void OnBackPressed() => Menu.StartSingleplayer();

    /// <inheritdoc/>
    public override void OnButton(MenuWidget w)
    {
        if (w == buttonBack)
        {
            OnBackPressed();
        }
    }
}