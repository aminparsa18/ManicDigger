using OpenTK.Windowing.Common;

public interface IModBase : IDisposable
{
    void OnBeforeNewFrameDraw3d(IGame game, float deltaTime);
    bool OnClientCommand(IGame _game, ClientCommandArgs args);
    void OnHitEntity(IGame game, OnUseEntityArgs e);
    void OnKeyDown(IGame game, KeyEventArgs args);
    void OnKeyPress(IGame game, KeyPressEventArgs args);
    void OnKeyUp(IGame _game, KeyEventArgs args);
    void OnMouseDown(IGame game, MouseEventArgs args);
    void OnMouseMove(MouseEventArgs args);
    void OnMouseUp(IGame game, MouseEventArgs args);
    void OnMouseWheelChanged(IGame game, MouseWheelEventArgs args);
    void OnNewFrame(IGame game, float args);
    void OnNewFrameDraw2d(IGame game, float deltaTime);
    void OnNewFrameDraw3d(IGame _game, float deltaTime);
    void OnNewFrameFixed(IGame game, float args);
    void OnNewFrameReadOnlyMainThread(IGame game, float deltaTime);
    void OnReadOnlyBackgroundThread(IGame game, float dt);
    void OnReadOnlyMainThread(IGame game, float dt);
    void OnReadWriteMainThread(float dt);
    void OnTouchEnd(IGame game, TouchEventArgs e);
    void OnTouchMove(TouchEventArgs e);
    void OnTouchStart(IGame game, TouchEventArgs e);
    void OnUseEntity(IGame game, OnUseEntityArgs e);
}