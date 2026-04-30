using OpenTK.Windowing.Common;

/// <summary>
/// Base class for all client-side mods. Override only the hooks you need.
/// </summary>
public abstract class ModBase : IModBase
{
    /// <summary>Called each tick on the main thread; game state is read-only.</summary>
    public virtual void OnReadOnlyMainThread(IGame game, float dt) { }

    /// <summary>Called each tick on a background thread; game state is read-only.</summary>
    public virtual void OnReadOnlyBackgroundThread(IGame game, float dt) { }

    /// <summary>Called each tick on the main thread; game state may be modified.</summary>
    public virtual void OnReadWriteMainThread(float dt) { }

    /// <summary>Called when the player issues a client-side command. Return <see langword="true"/> to mark the command as handled.</summary>
    public virtual bool OnClientCommand(IGame _game, ClientCommandArgs args) => false;

    /// <summary>Called at the start of each frame.</summary>
    public virtual void OnNewFrame(IGame game, float args) { }

    /// <summary>Called each fixed-timestep frame update.</summary>
    public virtual void OnNewFrameFixed(IGame game, float args) { }

    /// <summary>Called during the 2D draw pass of each frame.</summary>
    public virtual void OnNewFrameDraw2d(IGame game, float deltaTime) { }

    /// <summary>Called before the 3D draw pass of each frame.</summary>
    public virtual void OnBeforeNewFrameDraw3d(IGame game, float deltaTime) { }

    /// <summary>Called during the 3D draw pass of each frame.</summary>
    public virtual void OnNewFrameDraw3d(IGame _game, float deltaTime) { }

    /// <summary>Called during the read-only main thread phase of each frame.</summary>
    public virtual void OnNewFrameReadOnlyMainThread(IGame game, float deltaTime) { }

    /// <summary>Called when a keyboard key is pressed down.</summary>
    public virtual void OnKeyDown(IGame game, KeyEventArgs args) { }

    /// <summary>Called when a character key is pressed (text input).</summary>
    public virtual void OnKeyPress(IGame game, KeyPressEventArgs args) { }

    /// <summary>Called when a keyboard key is released.</summary>
    public virtual void OnKeyUp(IGame _game, KeyEventArgs args) { }

    /// <summary>Called when a mouse button is released.</summary>
    public virtual void OnMouseUp(IGame game, MouseEventArgs args) { }

    /// <summary>Called when a mouse button is pressed down.</summary>
    public virtual void OnMouseDown(IGame game, MouseEventArgs args) { }

    /// <summary>Called when the mouse cursor moves.</summary>
    public virtual void OnMouseMove(MouseEventArgs args) { }

    /// <summary>Called when the mouse wheel is scrolled.</summary>
    public virtual void OnMouseWheelChanged(IGame game, MouseWheelEventArgs args) { }

    /// <summary>Called when a touch gesture begins.</summary>
    public virtual void OnTouchStart(IGame game, TouchEventArgs e) { }

    /// <summary>Called when a touch gesture moves.</summary>
    public virtual void OnTouchMove(TouchEventArgs e) { }

    /// <summary>Called when a touch gesture ends.</summary>
    public virtual void OnTouchEnd(IGame game, TouchEventArgs e) { }

    /// <summary>Called when the player uses (right-clicks) an entity.</summary>
    public virtual void OnUseEntity(IGame game, OnUseEntityArgs e) { }

    /// <summary>Called when the player hits (left-clicks) an entity.</summary>
    public virtual void OnHitEntity(IGame game, OnUseEntityArgs e) { }

    /// <summary>Called when the mod is unloaded. Release any resources here.</summary>
    public virtual void Dispose() { }
}