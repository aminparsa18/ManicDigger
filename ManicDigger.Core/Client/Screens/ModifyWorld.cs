/// <summary>
/// Screen providing world-modification tools. Currently a stub — only the
/// Back button is implemented.
/// </summary>
public class ModifyWorldScreen : ScreenBase
{
    private readonly MenuWidget buttonBack;

    private string title = "Modify World";

    public ModifyWorldScreen(IMenuRenderer renderer, IMenuNavigator navigator, IGameService platform)
        : base(renderer, navigator, platform, default, default)
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
        buttonBack.text = Renderer.Translate("MainMenu_ButtonBack");
        title = Renderer.Translate("MainMenu_ModifyWorld");
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        float scale = Renderer.GetScale();

        Renderer.DrawBackground();
        Renderer.DrawText(title, 14 * scale, Platform.CanvasWidth / 2, 0, TextAlign.Center, TextBaseline.Top);

        buttonBack.x = 40 * scale;
        buttonBack.y = Platform.CanvasHeight - 104 * scale;
        buttonBack.sizex = 256 * scale;
        buttonBack.sizey = 64 * scale;
        buttonBack.fontSize = 14 * scale;

        DrawWidgets();
    }

    /// <inheritdoc/>
    public override void OnBackPressed() => Navigator.StartSingleplayer();

    /// <inheritdoc/>
    public override void OnButton(MenuWidget w)
    {
        if (w == buttonBack) { OnBackPressed(); }
    }
}