#if WINDOWS
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Runtime.InteropServices;
using Application = Microsoft.Maui.Controls.Application;

namespace ManicDigger.Maui.Services;

public partial class MauiGameWindowService : IGameWindowService
{
    // -------------------------------------------------------------------------
    // P/Invoke
    // -------------------------------------------------------------------------

    [DllImport("user32.dll")] private static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, byte[] andPlane, byte[] xorPlane);
    [DllImport("user32.dll")] private static extern bool SetSystemCursor(IntPtr hCursor, uint id);
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
    [DllImport("user32.dll")] private static extern bool ClipCursor(ref RECT lpRect);
    [DllImport("user32.dll")] private static extern bool ClipCursor(IntPtr lpRect);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate proc);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr proc);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr prev, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const uint OCR_NORMAL = 32512;
    private const uint SPI_SETCURSORS = 0x0057;
    private const int GWL_WNDPROC = -4;

    // WM_ messages
    private const uint WM_INPUT = 0x00FF;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_MBUTTONDOWN = 0x0207;
    private const uint WM_MBUTTONUP = 0x0208;
    private const uint WM_XBUTTONDOWN = 0x020B;
    private const uint WM_XBUTTONUP = 0x020C;

    // Raw input
    private const uint RID_INPUT = 0x10000003;
    private const uint RIM_TYPEMOUSE = 0;

    // XButton identifiers (high word of wParam)
    private const int XBUTTON1 = 0x0001;
    private const int XBUTTON2 = 0x0002;

    // -------------------------------------------------------------------------
    // Structs
    // -------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWMOUSE
    {
        public ushort Flags;
        public ushort ButtonFlags;  // was: uint Buttons (low word is flags)
        public ushort ButtonData;   // high word — wheel delta for scroll
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER Header;
        public RAWMOUSE Mouse;
    }

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private WndProcDelegate? _wndProc;
    private IntPtr _oldWndProc = IntPtr.Zero;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Raw mouse movement delta (dx, dy) from WM_INPUT.</summary>
    public event Action<int, int>? RawMouseDelta;

    /// <summary>Mouse button pressed. Args: (button, x, y) in client coords.</summary>
    public event Action<MouseButton, int, int>? RawMouseDown;

    /// <summary>Mouse button released. Args: (button, x, y) in client coords.</summary>
    public event Action<MouseButton, int, int>? RawMouseUp;

    public void StartRawInput(IntPtr hwnd)
    {
        var rid = new RAWINPUTDEVICE[]
        {
            new RAWINPUTDEVICE
            {
                UsagePage = 0x01, // Generic Desktop
                Usage     = 0x02, // Mouse
                Flags     = 0,
                Target    = hwnd
            }
        };

        RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());

        _wndProc = WndProc;
        _oldWndProc = SetWindowLongPtr(hwnd, GWL_WNDPROC, _wndProc);

        System.Diagnostics.Debug.WriteLine($"[RawInput] registered hwnd={hwnd} oldWndProc={_oldWndProc}");
    }

    public void StopRawInput(IntPtr hwnd)
    {
        if (_oldWndProc == IntPtr.Zero)
            return;

        SetWindowLongPtr(hwnd, GWL_WNDPROC, _oldWndProc);
        _oldWndProc = IntPtr.Zero;
    }

    public void RecenterCursor()
    {
        if (!_mousePointerLocked)
            return;

        IntPtr hwnd = GetMauiHwnd();
        if (hwnd == IntPtr.Zero)
            return;

        GetWindowRect(hwnd, out RECT rect);
        SetCursorPos((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
    }

    public void CaptureCursor()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Create 1x1 invisible cursor
            IntPtr invisible = CreateCursor(
                IntPtr.Zero, 0, 0, 1, 1,
                [0xFF], // AND mask — fully transparent
                [0x00]);// XOR mask — no pixels

            // Replace the system arrow cursor globally
            SetSystemCursor(invisible, OCR_NORMAL);
        });
    }

    public void ReleaseCursor()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Restore ALL system cursors to Windows defaults in one call
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
            ClipCursor(IntPtr.Zero);
        });
    }

    // -------------------------------------------------------------------------
    // WndProc
    // -------------------------------------------------------------------------

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_INPUT:
                HandleRawInput(lParam);
                break;

            case WM_LBUTTONDOWN:
                RawMouseDown?.Invoke(MouseButton.Left, GetLParamX(lParam), GetLParamY(lParam));
                break;
            case WM_LBUTTONUP:
                RawMouseUp?.Invoke(MouseButton.Left, GetLParamX(lParam), GetLParamY(lParam));
                break;

            case WM_RBUTTONDOWN:
                RawMouseDown?.Invoke(MouseButton.Right, GetLParamX(lParam), GetLParamY(lParam));
                break;
            case WM_RBUTTONUP:
                RawMouseUp?.Invoke(MouseButton.Right, GetLParamX(lParam), GetLParamY(lParam));
                break;

            case WM_MBUTTONDOWN:
                RawMouseDown?.Invoke(MouseButton.Middle, GetLParamX(lParam), GetLParamY(lParam));
                break;
            case WM_MBUTTONUP:
                RawMouseUp?.Invoke(MouseButton.Middle, GetLParamX(lParam), GetLParamY(lParam));
                break;

            case WM_XBUTTONDOWN:
                RawMouseDown?.Invoke(GetXButton(wParam), GetLParamX(lParam), GetLParamY(lParam));
                break;
            case WM_XBUTTONUP:
                RawMouseUp?.Invoke(GetXButton(wParam), GetLParamX(lParam), GetLParamY(lParam));
                break;
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // Button flag constants
    private const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
    private const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
    private const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
    private const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
    private const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
    private const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;
    private const ushort RI_MOUSE_BUTTON4_DOWN = 0x0040;
    private const ushort RI_MOUSE_BUTTON4_UP = 0x0080;
    private const ushort RI_MOUSE_BUTTON5_DOWN = 0x0100;
    private const ushort RI_MOUSE_BUTTON5_UP = 0x0200;

    private void HandleRawInput(IntPtr lParam)
    {
        uint size = 0;
        GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());

        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            GetRawInputData(lParam, RID_INPUT, buf, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            var raw = Marshal.PtrToStructure<RAWINPUT>(buf);

            if (raw.Header.Type != RIM_TYPEMOUSE)
                return;

            // Mouse movement
            if (_isFocused && (raw.Mouse.LastX != 0 || raw.Mouse.LastY != 0))
                RawMouseDelta?.Invoke(raw.Mouse.LastX, raw.Mouse.LastY);

            // Mouse buttons — always fire regardless of focus
            ushort flags = raw.Mouse.ButtonFlags;

            if ((flags & RI_MOUSE_LEFT_BUTTON_DOWN) != 0) RawMouseDown?.Invoke(MouseButton.Left, 0, 0);
            if ((flags & RI_MOUSE_LEFT_BUTTON_UP) != 0) RawMouseUp?.Invoke(MouseButton.Left, 0, 0);
            if ((flags & RI_MOUSE_RIGHT_BUTTON_DOWN) != 0) RawMouseDown?.Invoke(MouseButton.Right, 0, 0);
            if ((flags & RI_MOUSE_RIGHT_BUTTON_UP) != 0) RawMouseUp?.Invoke(MouseButton.Right, 0, 0);
            if ((flags & RI_MOUSE_MIDDLE_BUTTON_DOWN) != 0) RawMouseDown?.Invoke(MouseButton.Middle, 0, 0);
            if ((flags & RI_MOUSE_MIDDLE_BUTTON_UP) != 0) RawMouseUp?.Invoke(MouseButton.Middle, 0, 0);
            if ((flags & RI_MOUSE_BUTTON4_DOWN) != 0) RawMouseDown?.Invoke(MouseButton.Button4, 0, 0);
            if ((flags & RI_MOUSE_BUTTON4_UP) != 0) RawMouseUp?.Invoke(MouseButton.Button4, 0, 0);
            if ((flags & RI_MOUSE_BUTTON5_DOWN) != 0) RawMouseDown?.Invoke(MouseButton.Button5, 0, 0);
            if ((flags & RI_MOUSE_BUTTON5_UP) != 0) RawMouseUp?.Invoke(MouseButton.Button5, 0, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private static int GetLParamX(IntPtr lParam) => (short)(lParam.ToInt32() & 0xFFFF);
    private static int GetLParamY(IntPtr lParam) => (short)(lParam.ToInt32() >> 16);

    private static MouseButton GetXButton(IntPtr wParam)
    {
        int hiWord = (wParam.ToInt32() >> 16) & 0xFFFF;
        return hiWord == XBUTTON1 ? MouseButton.Button4 : MouseButton.Button5;
    }

    private static IntPtr GetMauiHwnd()
    {
        var window = Application.Current?.Windows[0].Handler?.PlatformView
                     as Microsoft.UI.Xaml.Window;

        return window is null ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(window);
    }
}

#endif