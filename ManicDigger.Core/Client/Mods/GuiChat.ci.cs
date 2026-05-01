using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public class ModGuiChat : ModBase
{
    public ModGuiChat(IGameService platform, IGame game) : base(game)
    {
        this.platform = platform;
        ChatFontSize = 11;
        currentFontSize = ChatFontSize;
        ChatScreenExpireTimeSeconds = 20;
        ChatLinesMaxToDraw = 10;
        font = new Font("Arial", currentFontSize, currentFontStyle);
        chatlines2 = new Chatline[1024];
    }

    private readonly IGameService platform;

    internal float ChatFontSize;
    internal int ChatScreenExpireTimeSeconds;
    internal int ChatLinesMaxToDraw;
    internal int ChatPageScroll;
    private float currentFontSize = 11;
    private FontStyle currentFontStyle = FontStyle.Regular;

    public override void OnNewFrameDraw2d(float deltaTime)
    {
        if (Game.GuiState == GuiState.MapLoading)
        {
            return;
        }
        DrawChatLines(Game.GuiTyping == TypingState.Typing);
        if (Game.GuiTyping == TypingState.Typing)
        {
            DrawTypingBuffer(Game);
        }
    }

    public override void OnMouseDown(MouseEventArgs args)
    {
        for (int i = 0; i < chatlines2Count; i++)
        {
            float dx = 20;
            if (!platform.IsMousePointerLocked())
            {
                dx += 100;
            }
            float chatlineStartX = dx * Game.Scale();
            float chatlineStartY = (90 + i * 25) * Game.Scale();
            float chatlineSizeX = 500 * Game.Scale();
            float chatlineSizeY = 20 * Game.Scale();
            if (args.GetX() > chatlineStartX && args.GetX() < chatlineStartX + chatlineSizeX)
            {
                if (args.GetY() > chatlineStartY && args.GetY() < chatlineStartY + chatlineSizeY)
                {
                    //Mouse over chatline at position i
                    if (chatlines2[i].clickable)
                    {
                        platform.OpenLinkInBrowser(chatlines2[i].linkTarget);
                    }
                }
            }
        }
    }

    private readonly Chatline[] chatlines2;
    private int chatlines2Count;
    public void DrawChatLines(bool all)
    {
        chatlines2Count = 0;
        int timeNow = platform.TimeMillisecondsFromStart;
        int scroll;
        if (!all)
        {
            scroll = 0;
        }
        else
        {
            scroll = ChatPageScroll;
        }
        int first = Game.ChatLinesCount - ChatLinesMaxToDraw * (scroll + 1);
        if (first < 0)
        {
            first = 0;
        }
        int count = Game.ChatLinesCount;
        if (count > ChatLinesMaxToDraw)
        {
            count = ChatLinesMaxToDraw;
        }
        for (int i = first; i < first + count; i++)
        {
            Chatline c = Game.ChatLines[i];
            if (all || ((1f * (timeNow - c.timeMilliseconds) / 1000) < ChatScreenExpireTimeSeconds))
            {
                chatlines2[chatlines2Count++] = c;
            }
        }
        currentFontSize = ChatFontSize * Game.Scale();
        font = new Font("Arial", currentFontSize, currentFontStyle);

        float dx = 20;
        //if (!game.Platform.IsMousePointerLocked())
        //{
        //    dx += 100;
        //}
        for (int i = 0; i < chatlines2Count; i++)
        {
            if (chatlines2[i].clickable)
            {
                //Different display of links in chat
                //2 = italic
                //3 = bold italic
                currentFontStyle = FontStyle.Italic;
                font = new Font("Arial", currentFontSize, currentFontStyle);
            }
            else
            {
                //0 = normal
                //1 = bold
                currentFontStyle = FontStyle.Bold;
                font = new Font("Arial", currentFontSize, currentFontStyle);
            }
            Game.Draw2dText(chatlines2[i].text, font, dx * Game.Scale(), (90 + i * 25) * Game.Scale(), null, false);
        }
        if (ChatPageScroll != 0)
        {
            Game.Draw2dText(string.Format("&7Page: {0}", ChatPageScroll.ToString()), font, dx * Game.Scale(), (90 + (-1) * 25) * Game.Scale(), null, false);
        }
    }
    private Font font;
    public void DrawTypingBuffer(IGame game)
    {
        currentFontSize = ChatFontSize * game.Scale();
        font = new Font("Arial", currentFontSize, currentFontStyle);
        string s = game.GuiTypingBuffer;
        if (game.IsTeamchat)
        {
            s = string.Format("To team: {0}", s);
        }
        if (platform.IsSmallScreen())
        {
            game.Draw2dText(string.Format("{0}_", s), font, 50 * game.Scale(), (platform.CanvasHeight / 2) - 100 * game.Scale(), null, true);
        }
        else
        {
            game.Draw2dText(string.Format("{0}_", s), font, 50 * game.Scale(), platform.CanvasHeight - 100 * game.Scale(), null, true);
        }
    }

    public override void OnKeyDown(KeyEventArgs args)
    {
        if (Game.GuiState != GuiState.Normal)
        {
            //Don't open chat when not in normal game
            return;
        }
        int eKey = args.KeyChar;
        if (eKey == Game.GetKey(Keys.KeyPad7) && Game.IsShiftPressed && Game.GuiTyping == TypingState.None) // don't need to hit enter for typing commands starting with slash
        {
            Game.GuiTyping = TypingState.Typing;
            Game.IsTyping = true;
            Game.GuiTypingBuffer = "";
            Game.IsTeamchat = false;
            args.Handled = true;
            return;
        }
        if (eKey == Game.GetKey(Keys.PageUp) && Game.GuiTyping == TypingState.Typing)
        {
            ChatPageScroll++;
            args.Handled = true;
        }
        if (eKey == Game.GetKey(Keys.PageDown) && Game.GuiTyping == TypingState.Typing)
        {
            ChatPageScroll--;
            args.Handled = true;
        }
        ChatPageScroll = Math.Clamp(ChatPageScroll, 0, Game.ChatLinesCount / ChatLinesMaxToDraw);
        if (eKey == Game.GetKey(Keys.Enter) || eKey == Game.GetKey(Keys.KeyPadEnter))
        {
            if (Game.GuiTyping == TypingState.Typing)
            {
                Game.TypingLog.Add(Game.GuiTypingBuffer);
                Game.TypingLogPos = Game.TypingLog.Count;
                Game.ExecuteChat(Game.GuiTypingBuffer);

                Game.GuiTypingBuffer = "";
                Game.IsTyping = false;

                Game.GuiTyping = TypingState.None;
                platform.ShowKeyboard(false);
            }
            else if (Game.GuiTyping == TypingState.None)
            {
                Game.StartTyping();
            }
            else if (Game.GuiTyping == TypingState.Ready)
            {
                Console.WriteLine("Keyboard_KeyDown ready");
            }
            args.Handled = true;
            return;
        }
        if (Game.GuiTyping == TypingState.Typing)
        {
            int key = eKey;
            if (key == Game.GetKey(Keys.Backspace))
            {
                if (Game.GuiTypingBuffer.Length > 0)
                {
                    Game.GuiTypingBuffer = Game.GuiTypingBuffer[..^1];
                }
                args.Handled = true;
                return;
            }
            if (Game.KeyboardStateRaw[Game.GetKey(Keys.LeftControl)] || Game.KeyboardStateRaw[Game.GetKey(Keys.RightControl)])
            {
                if (key == Game.GetKey(Keys.V))
                {
                    if (Clipboard.ContainsText())
                    {
                        Game.GuiTypingBuffer = string.Concat(Game.GuiTypingBuffer, Clipboard.GetText());
                    }
                    args.Handled = true;
                    return;
                }
            }
            if (key == Game.GetKey(Keys.Up))
            {
                Game.TypingLogPos--;
                if (Game.TypingLogPos < 0) { Game.TypingLogPos = 0; }
                if (Game.TypingLogPos >= 0 && Game.TypingLogPos < Game.TypingLog.Count)
                {
                    Game.GuiTypingBuffer = Game.TypingLog[Game.TypingLogPos];
                }
                args.Handled = true;
            }
            if (key == Game.GetKey(Keys.Down))
            {
                Game.TypingLogPos++;
                if (Game.TypingLogPos > Game.TypingLog.Count) { Game.TypingLogPos = Game.TypingLog.Count; }
                if (Game.TypingLogPos >= 0 && Game.TypingLogPos < Game.TypingLog.Count)
                {
                    Game.GuiTypingBuffer = Game.TypingLog[Game.TypingLogPos];
                }
                if (Game.TypingLogPos == Game.TypingLog.Count)
                {
                    Game.GuiTypingBuffer = "";
                }
                args.Handled = true;
            }
            //Handles player name autocomplete in chat
            if (eKey == Game.GetKey(Keys.Tab) && Game.GuiTypingBuffer.Trim() != "")
            {
                string[] parts = Game.GuiTypingBuffer.Split(" ");
                string completed = DoAutocomplete(parts[parts.Length - 1]);
                if (completed == "")
                {
                    //No completion available. Abort.
                    args.Handled = true;
                    return;
                }
                else if (parts.Length == 1)
                {
                    //Part is first word. Format as "<name>: "
                    Game.GuiTypingBuffer = string.Concat(completed, ": ");
                }
                else
                {
                    //Part is not first. Just complete "<name> "
                    parts[parts.Length - 1] = completed;
                    Game.GuiTypingBuffer = string.Concat(string.Join(" ", parts), " ");
                }
                args.Handled = true;
                return;
            }
            args.Handled = true;
            return;
        }
    }

    public override void OnKeyPress(KeyPressEventArgs args)
    {
        if (Game.GuiState != GuiState.Normal)
        {
            //Don't open chat when not in normal game
            return;
        }
        int eKeyChar = args.KeyChar;
        int chart = 116;
        int charT = 84;
        int chary = 121;
        int charY = 89;
        if ((eKeyChar == chart || eKeyChar == charT) && Game.GuiTyping == TypingState.None)
        {
            Game.GuiTyping = TypingState.Typing;
            Game.GuiTypingBuffer = "";
            Game.IsTeamchat = false;
            return;
        }
        if ((eKeyChar == chary || eKeyChar == charY) && Game.GuiTyping == TypingState.None)
        {
            Game.GuiTyping = TypingState.Typing;
            Game.GuiTypingBuffer = "";
            Game.IsTeamchat = true;
            return;
        }
        if (Game.GuiTyping == TypingState.Typing)
        {
            int c = eKeyChar;
            if (EncodingHelper.IsValidTypingChar(c))
            {
                Game.GuiTypingBuffer = string.Concat(Game.GuiTypingBuffer, (char)c);
            }
        }
    }

    public string DoAutocomplete(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            for (int i = 0; i < Game.Entities.Count; i++)
            {
                Entity entity = Game.Entities[i];
                if (entity == null) { continue; }
                if (entity.drawName == null) { continue; }
                if (!entity.drawName.ClientAutoComplete) { continue; }
                DrawName p = entity.drawName;
                //Use substring here because player names are internally in format &xNAME (so we need to cut first 2 characters)
                if (p.Name[2..].StartsWith(text, StringComparison.InvariantCultureIgnoreCase))
                {
                    return p.Name[2..];
                }
            }
        }
        return "";
    }
}

public class Chatline
{
    internal string text;
    internal int timeMilliseconds;
    internal bool clickable;
    internal string linkTarget;

    internal static Chatline Create(string text_, int timeMilliseconds_)
    {
        Chatline c = new()
        {
            text = text_,
            timeMilliseconds = timeMilliseconds_,
            clickable = false
        };
        return c;
    }

    internal static Chatline CreateClickable(string text_, int timeMilliseconds_, string linkTarget_)
    {
        Chatline c = new()
        {
            text = text_,
            timeMilliseconds = timeMilliseconds_,
            clickable = true,
            linkTarget = linkTarget_
        };
        return c;
    }
}
