using OpenTK.Windowing.Common;

public interface IModBase : IDisposable
{
    void OnBeforeNewFrameDraw3d(float deltaTime);
    bool OnClientCommand(ClientCommandArgs args);
    void OnHitEntity(OnUseEntityArgs e);
    void OnKeyDown(KeyEventArgs args);
    void OnKeyPress(KeyPressEventArgs args);
    void OnKeyUp(KeyEventArgs args);
    void OnMouseDown(MouseEventArgs args);
    void OnMouseMove(MouseEventArgs args);
    void OnMouseUp(MouseEventArgs args);
    void OnMouseWheelChanged(MouseWheelEventArgs args);
    void OnNewFrame(float args);
    void OnNewFrameDraw2d(float deltaTime);
    void OnNewFrameDraw3d(float deltaTime);
    void OnNewFrameFixed(float args);
    void OnNewFrameReadOnlyMainThread(float deltaTime);
    void OnReadOnlyBackgroundThread(float dt);
    void OnReadOnlyMainThread(float dt);
    void OnReadWriteMainThread(float dt);
    void OnTouchEnd(TouchEventArgs e);
    void OnTouchMove(TouchEventArgs e);
    void OnTouchStart(TouchEventArgs e);
    void OnUseEntity(OnUseEntityArgs e);
}