public interface ILoginScreen
{
    void Configure(string serverHash, string ip, int port);
    void LoadTranslations();
    void OnBackPressed();
    void OnButton(MenuWidget w);
    void Render(float dt);
}