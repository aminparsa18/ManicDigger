using OpenTK.Windowing.Common;

// ─────────────────────────────────────────────────────────────────────────────
// Composite — the full platform contract used throughout the game.
// GamePlatformNative implements this; everything else depends on it.
// ─────────────────────────────────────────────────────────────────────────────

public interface IGameService :
    IPlatformMisc
{
}

// ─────────────────────────────────────────────────────────────────────────────
// Misc / OS / window
// ─────────────────────────────────────────────────────────────────────────────

public interface IPlatformMisc
{
    INetworkService NetworkService{ get; set; }
    IGameExit GameExit { get; set; }
    void AddOnNewFrame(Action<float> handler);
    void AddOnKeyEvent(Action<KeyEventArgs> onKeyDown,
        Action<KeyEventArgs> onKeyUp,
        Action<KeyPressEventArgs> onKeyPress);
    void AddOnMouseEvent(
        Action<MouseEventArgs> onMouseDown,
        Action<MouseEventArgs> onMouseUp,
        Action<MouseEventArgs> onMouseMove,
        Action<MouseWheelEventArgs> onMouseWheel);
    void AddOnTouchEvent(Action<TouchEventArgs> onTouchStart,
        Action<TouchEventArgs> onTouchMove,
        Action<TouchEventArgs> onTouchEnd);
    int GetCanvasWidth();
    int GetCanvasHeight();
    int TimeMillisecondsFromStart { get; }

    void SaveScreenshot();
    Bitmap GrabScreenshot();
    IAviWriter AviWriterCreate();
    string PathStorage();
    void SetVSync(bool enabled);
    string GetGameVersion();
    void GzipDecompress(byte[] compressed, int compressedLength, byte[] ret);
    bool ChatLog(string servername, string p);
    bool IsValidTypingChar(int c);
    void WindowExit();
    void MessageBoxShowError(string text, string caption);
    void SetTitle(string applicationname);
    bool Focused();
    void AddOnCrash(OnCrashHandler handler);
    string KeyName(int key);
    List<DisplayResolutionCi> GetDisplayResolutions();
    WindowState GetWindowState();
    void SetWindowState(WindowState value);
    void ChangeResolution(int width, int height, int bitsPerPixel, float refreshRate);
    DisplayResolutionCi GetDisplayResolutionDefault();
   
    string FileOpenDialog(string extension, string extensionName, string initialDirectory);
    void MouseCursorSetVisible(bool value);
    bool MouseCursorIsVisible();
    void ApplicationDoEvents();
    void ShowKeyboard(bool show);
    bool IsFastSystem();
    Preferences GetPreferences();
    void SetPreferences(Preferences preferences);
    bool IsMousePointerLocked();
    void RequestMousePointerLock();
    void ExitMousePointerLock();
    bool MultithreadingAvailable();
    void QueueUserWorkItem(Action action);
    byte[] GzipCompress(byte[] data, int dataLength);
    bool IsDebuggerAttached();
    bool IsSmallScreen();
    void OpenLinkInBrowser(string url);
    void SaveAssetToCache(Asset tosave);
    Asset LoadAssetFromCache(string md5);
    bool IsCached(string md5);
    string QueryStringValue(string key);
    void SetWindowCursor(int hotx, int hoty, int sizex, int sizey, byte[] imgdata, int imgdataLength);
    void RestoreWindowCursor();
}

public class OnCrashHandler
{
    public virtual void OnCrash() { }
}

