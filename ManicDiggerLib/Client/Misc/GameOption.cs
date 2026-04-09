public class GameOption
{
    public GameOption()
    {
        float one = 1;
        Shadows = false;
        Font = 0;
        DrawDistance = 32;
        UseServerTextures = true;
        EnableSound = true;
        EnableAutoJump = false;
        ClientLanguage = "";
        Framerate = 0;
        Resolution = 0;
        Fullscreen = false;
        Smoothshadows = true;
        BlockShadowSave = one * 6 / 10;
        EnableBlockShadow = true;
        Keys = new int[360];
    }
    internal bool Shadows;
    internal int Font;
    internal int DrawDistance;
    internal bool UseServerTextures;
    internal bool EnableSound;
    internal bool EnableAutoJump;
    internal string ClientLanguage;
    internal int Framerate;
    internal int Resolution;
    internal bool Fullscreen;
    internal bool Smoothshadows;
    internal float BlockShadowSave;
    internal bool EnableBlockShadow;
    internal int[] Keys;
}
