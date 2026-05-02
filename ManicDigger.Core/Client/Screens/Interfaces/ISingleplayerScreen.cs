public interface ISingleplayerScreen
{
    void LoadTranslations();
    void OnBackPressed();
    void OnButton(MenuWidget w);
    void Render(float dt);
}