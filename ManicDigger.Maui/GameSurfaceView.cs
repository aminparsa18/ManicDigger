namespace ManicDigger.Maui;

/// <summary>
/// A MAUI ContentView that represents the game's rendering surface.
/// The platform handler wires this up to the actual native window/surface.
/// </summary>
public class GameSurfaceView : View
{
    // Raised by the handler once the native surface is ready
    public event EventHandler? SurfaceReady;

    internal void NotifySurfaceReady() => SurfaceReady?.Invoke(this, EventArgs.Empty);
}