public class MouseEventArgs
{
    private int x;
    private int y;
    private int movementX;
    private int movementY;
    private int button;
    public int GetX() { return x; } public void SetX(int value) { x = value; }
    public int GetY() { return y; } public void SetY(int value) { y = value; }
    public int GetMovementX() { return movementX; } public void SetMovementX(int value) { movementX = value; }
    public int GetMovementY() { return movementY; } public void SetMovementY(int value) { movementY = value; }
    public int GetButton() { return button; } public void SetButton(int value) { button = value; }
    private bool handled;
    public bool GetHandled() { return handled; }
    public void SetHandled(bool value) { handled = value; }
    private bool forceUsage;
    public bool GetForceUsage() { return forceUsage; }
    public void SetForceUsage(bool value) { forceUsage = value; }
    private bool emulated;
    public bool GetEmulated() { return emulated; }
    public void SetEmulated(bool value) { emulated = value; }
}

