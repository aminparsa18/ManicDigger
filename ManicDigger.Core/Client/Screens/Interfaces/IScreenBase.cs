using OpenTK.Windowing.Common;

public interface IScreenBase
{
    void DrawScene(float dt);
    void DrawWidgets();
    void LoadTranslations();
    void OnBackPressed();
    void OnButton(MenuWidget w);
    void OnKeyDown(KeyEventArgs e);
    void OnKeyPress(KeyPressEventArgs e);
    void OnKeyUp(KeyEventArgs e);
    void OnMouseDown(MouseEventArgs e);
    void OnMouseMove(MouseEventArgs e);
    void OnMouseUp(MouseEventArgs e);
    void OnMouseWheel(MouseWheelEventArgs e);
    void OnTouchEnd(TouchEventArgs e);
    void OnTouchMove(TouchEventArgs e);
    void OnTouchStart(TouchEventArgs e);
    void Render(float dt);
}