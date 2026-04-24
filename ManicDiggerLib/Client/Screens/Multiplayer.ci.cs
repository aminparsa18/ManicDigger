using OpenTK.Windowing.Common;

public class MultiplayerScreen : ScreenBase
{
    public MultiplayerScreen(IMenuRenderer renderer, IMenuNavigator navigator, IGamePlatform platform)
        : base(renderer, navigator, platform)
    {
        // Tab chain (by list index): [0] Back → [1] Connect → [3] ConnectToIp → [2] Refresh → [0] Back
        back = new MenuWidget { text = "Back", type = UIWidgetType.Button, nextWidget = 1 };
        connect = new MenuWidget { text = "Connect", type = UIWidgetType.Button, nextWidget = 3 };
        connectToIp = new MenuWidget { text = "Connect to IP", type = UIWidgetType.Button, nextWidget = 2 };
        refresh = new MenuWidget { text = "Refresh", type = UIWidgetType.Button, nextWidget = 0 };

        page = 0;
        pageUp = new MenuWidget
        {
            text = "",
            type = UIWidgetType.Button,
            buttonStyle = ButtonStyle.Text,
            visible = false
        };
        pageDown = new MenuWidget
        {
            text = "",
            type = UIWidgetType.Button,
            buttonStyle = ButtonStyle.Text,
            visible = false
        };
        loggedInName = new MenuWidget
        {
            text = "",
            type = UIWidgetType.Button,
            buttonStyle = ButtonStyle.Text
        };
        logout = new MenuWidget
        {
            text = "",
            type = UIWidgetType.Button,
            buttonStyle = ButtonStyle.Button
        };

        title = "Multiplayer";

        Widgets.Add(back);         // 0
        Widgets.Add(connect);      // 1
        Widgets.Add(refresh);      // 2
        Widgets.Add(connectToIp);  // 3
        Widgets.Add(pageUp);       // 4
        Widgets.Add(pageDown);     // 5
        Widgets.Add(loggedInName); // 6
        Widgets.Add(logout);       // 7

        serverListAddress = new HttpResponse();
        serverListCsv = new HttpResponse();
        serversOnList = new ServerOnList[serversOnListCount];
        thumbResponses = new ThumbnailResponseCi[serversOnListCount];

        serverButtons = new MenuWidget[serverButtonsCount];
        for (int i = 0; i < serverButtonsCount; i++)
        {
            MenuWidget b = new MenuWidget
            {
                text = "Invalid",
                type = UIWidgetType.Button,
                visible = false,
                image = "serverlist_entry_noimage.png"
            };
            serverButtons[i] = b;
            Widgets.Add(b); // 8 + i
        }
        loading = true;
    }

    private bool loaded;
    private readonly HttpResponse serverListAddress;
    private readonly HttpResponse serverListCsv;
    private readonly ServerOnList[] serversOnList;
    private const int serversOnListCount = 1024;
    private int page;
    private int serversPerPage;
    private string title;
    private bool loading;

    public override void LoadTranslations()
    {
        back.text = Renderer.Translate("MainMenu_ButtonBack");
        connect.text = Renderer.Translate("MainMenu_MultiplayerConnect");
        connectToIp.text = Renderer.Translate("MainMenu_MultiplayerConnectIP");
        refresh.text = Renderer.Translate("MainMenu_MultiplayerRefresh");
        title = Renderer.Translate("MainMenu_Multiplayer");
    }

    public override void Render(float dt)
    {
        if (!loaded)
        {
            Platform.WebClientDownloadDataAsync("http://manicdigger.sourceforge.net/serverlistcsv.php", serverListAddress);
            loaded = true;
        }
        if (serverListAddress.Done)
        {
            serverListAddress.Done = false;
            Platform.WebClientDownloadDataAsync(serverListAddress.GetString(), serverListCsv);
        }
        if (serverListCsv.Done)
        {
            loading = false;
            serverListCsv.Done = false;
            for (int i = 0; i < serversOnListCount; i++)
            {
                serversOnList[i] = null;
                thumbResponses[i] = null;
            }
            string[] servers = serverListCsv.GetString().Split("\n");
            for (int i = 0; i < servers.Length; i++)
            {
                string[] ss = servers[i].Split("\t");
                if (ss.Length < 10)
                {
                    continue;
                }
                ServerOnList s = new()
                {
                    Hash = ss[0],
                    Name = StringUtils.DecodeHTMLEntities(ss[1]),
                    Motd = StringUtils.DecodeHTMLEntities(ss[2]),
                    Port = int.Parse(ss[3]),
                    Ip = ss[4],
                    Version = ss[5],
                    Users = int.Parse(ss[6]),
                    Max = int.Parse(ss[7]),
                    GameMode = ss[8],
                    Players = ss[9]
                };
                serversOnList[i] = s;
            }
        }


        float scale = Renderer.GetScale();

        back.x = 40 * scale;
        back.y = Platform.GetCanvasHeight() - 104 * scale;
        back.sizex = 256 * scale;
        back.sizey = 64 * scale;
        back.fontSize = 14 * scale;

        connect.x = Platform.GetCanvasWidth() / 2 - 300 * scale;
        connect.y = Platform.GetCanvasHeight() - 104 * scale;
        connect.sizex = 256 * scale;
        connect.sizey = 64 * scale;
        connect.fontSize = 14 * scale;

        connectToIp.x = Platform.GetCanvasWidth() / 2 - 0 * scale;
        connectToIp.y = Platform.GetCanvasHeight() - 104 * scale;
        connectToIp.sizex = 256 * scale;
        connectToIp.sizey = 64 * scale;
        connectToIp.fontSize = 14 * scale;

        refresh.x = Platform.GetCanvasWidth() / 2 + 350 * scale;
        refresh.y = Platform.GetCanvasHeight() - 104 * scale;
        refresh.sizex = 256 * scale;
        refresh.sizey = 64 * scale;
        refresh.fontSize = 14 * scale;

        pageUp.x = Platform.GetCanvasWidth() - 94 * scale;
        pageUp.y = 100 * scale + (serversPerPage - 1) * 70 * scale;
        pageUp.sizex = 64 * scale;
        pageUp.sizey = 64 * scale;
        pageUp.image = "serverlist_nav_down.png";

        pageDown.x = Platform.GetCanvasWidth() - 94 * scale;
        pageDown.y = 100 * scale;
        pageDown.sizex = 64 * scale;
        pageDown.sizey = 64 * scale;
        pageDown.image = "serverlist_nav_up.png";

        loggedInName.x = Platform.GetCanvasWidth() - 228 * scale;
        loggedInName.y = 32 * scale;
        loggedInName.sizex = 128 * scale;
        loggedInName.sizey = 32 * scale;
        loggedInName.fontSize = 12 * scale;
        if (loggedInName.text == "")
        {
            if (Platform.GetPreferences().GetString("Password", "") != "")
            {
                loggedInName.text = Platform.GetPreferences().GetString("Username", "Invalid");
            }
        }
        logout.visible = loggedInName.text != "";

        logout.x = Platform.GetCanvasWidth() - 228 * scale;
        logout.y = 62 * scale;
        logout.sizex = 128 * scale;
        logout.sizey = 32 * scale;
        logout.fontSize = 12 * scale;
        logout.text = "Logout";

        Renderer.DrawBackground();
        Renderer.DrawText(title, 20 * scale, Platform.GetCanvasWidth() / 2, 10, TextAlign.Center, TextBaseline.Top);
        Renderer.DrawText((page + 1).ToString(), 14 * scale, Platform.GetCanvasWidth() - 68 * scale, Platform.GetCanvasHeight() / 2, TextAlign.Center, TextBaseline.Middle);

        if (loading)
        {
            Renderer.DrawText(Renderer.Translate("MainMenu_MultiplayerLoading"), 14 * scale, 100 * scale, 50 * scale, TextAlign.Left, TextBaseline.Top);
        }

        UpdateThumbnails();
        for (int i = 0; i < serverButtonsCount; i++)
        {
            serverButtons[i].visible = false;
        }

        serversPerPage = (int)((Platform.GetCanvasHeight() - (2 * 100 * scale)) / 70 * scale);
        if (serversPerPage <= 0)
        {
            // Do not let this get negative
            serversPerPage = 1;
        }
        for (int i = 0; i < serversPerPage; i++)
        {
            int index = i + (serversPerPage * page);
            if (index > serversOnListCount)
            {
                //Reset to first page
                page = 0;
                index = i + (serversPerPage * page);
            }
            ServerOnList s = serversOnList[index];
            if (s == null)
            {
                continue;
            }
            string t = string.Format("{1}", index.ToString(), s.Name);
            t = string.Format("{0}\n{1}", t, s.Motd);
            t = string.Format("{0}\n{1}", t, s.GameMode);
            t = string.Format("{0}\n{1}", t, s.Users.ToString());
            t = string.Format("{0}/{1}", t, s.Max.ToString());
            t = string.Format("{0}\n{1}", t, s.Version);

            serverButtons[i].text = t;
            serverButtons[i].x = 100 * scale;
            serverButtons[i].y = 100 * scale + i * 70 * scale;
            serverButtons[i].sizex = Platform.GetCanvasWidth() - 200 * scale;
            serverButtons[i].sizey = 64 * scale;
            serverButtons[i].visible = true;
            serverButtons[i].buttonStyle = ButtonStyle.ServerEntry;
            if (s.ThumbnailError)
            {
                //Server did not respond to ServerQuery. Maybe not reachable?
                serverButtons[i].description = "Server did not respond to query!";
            }
            else
            {
                serverButtons[i].description = null;
            }
            if (s.ThumbnailFetched && !s.ThumbnailError)
            {
                serverButtons[i].image = string.Format("serverlist_entry_{0}.png", s.Hash);
            }
            else
            {
                serverButtons[i].image = "serverlist_entry_noimage.png";
            }
        }
        UpdateScrollButtons();
        DrawWidgets();
    }

    private readonly ThumbnailResponseCi[] thumbResponses;
    public void UpdateThumbnails()
    {
        for (int i = 0; i < serversOnListCount; i++)
        {
            ServerOnList server = serversOnList[i];
            if (server == null)
            {
                continue;
            }
            if (server.ThumbnailFetched)
            {
                //Thumbnail already loaded
                continue;
            }
            if (!server.ThumbnailDownloading)
            {
                //Not started downloading yet
                thumbResponses[i] = new ThumbnailResponseCi();
                Platform.ThumbnailDownloadAsync(server.Ip, server.Port, thumbResponses[i]);
                server.ThumbnailDownloading = true;
            }
            else
            {
                //Download in progress
                if (thumbResponses[i] != null)
                {
                    if (thumbResponses[i].Done)
                    {
                        //Request completed. load received bitmap
                        Bitmap bmp = PixelBuffer.BitmapFromPng(thumbResponses[i].Data, thumbResponses[i].Data.Length);
                        if (bmp != null)
                        {
                            int texture = Platform.LoadTextureFromBitmap(bmp);
                            Renderer.RegisterTexture(string.Format("serverlist_entry_{0}.png", server.Hash), texture);
                            bmp.Dispose();
                        }
                        server.ThumbnailDownloading = false;
                        server.ThumbnailFetched = true;
                    }
                    if (thumbResponses[i].Error)
                    {
                        //Error while trying to download thumbnail
                        server.ThumbnailDownloading = false;
                        server.ThumbnailError = true;
                        server.ThumbnailFetched = true;
                    }
                }
                else
                {
                    //An error occured. stop trying
                    server.ThumbnailDownloading = false;
                    server.ThumbnailError = true;
                    server.ThumbnailFetched = true;
                }
            }
        }
    }

    public void PageUp_()
    {
        if (pageUp.visible && page < serverButtonsCount / serversPerPage - 1)
        {
            page++;
        }
    }
    public void PageDown_()
    {
        if (page > 0)
        {
            page--;
        }
    }
    public void UpdateScrollButtons()
    {
        //Determine if this page is the highest page containing servers
        bool maxpage = false;
        if ((page + 1) * serversPerPage >= serversOnListCount)
        {
            maxpage = true;
        }
        else
        {
            if (serversOnList[(page + 1) * serversPerPage] == null)
            {
                maxpage = true;
            }
        }
        //Hide scroll buttons
        if (page == 0)
        {
            pageDown.visible = false;
        }
        else
        {
            pageDown.visible = true;
        }
        if (maxpage)
        {
            pageUp.visible = false;
        }
        else
        {
            pageUp.visible = true;
        }
    }
    private readonly MenuWidget back;
    private readonly MenuWidget connect;
    private readonly MenuWidget connectToIp;
    private readonly MenuWidget refresh;
    private readonly MenuWidget pageUp;
    private readonly MenuWidget pageDown;
    private readonly MenuWidget loggedInName;
    private readonly MenuWidget logout;
    private readonly MenuWidget[] serverButtons;
    private const int serverButtonsCount = 1024;

    public override void OnBackPressed()
    {
        Navigator.StartMainMenu();
    }
    public override void OnMouseWheel(MouseWheelEventArgs e)
    {
        //menu.p.MessageBoxShowError(menu.p.IntToString(e.GetDelta()), "Delta");
        if (e.OffsetX < 0)
        {
            //Mouse wheel turned down
            PageUp_();
        }
        else if (e.OffsetX > 0)
        {
            //Mouse wheel turned up
            PageDown_();
        }
    }
    private string selectedServerHash;
    public override void OnButton(MenuWidget w)
    {
        for (int i = 0; i < serverButtonsCount; i++)
        {
            serverButtons[i].selected = false;
            if (serverButtons[i] == w)
            {
                serverButtons[i].selected = true;
                if (serversOnList[i + serversPerPage * page] != null)
                {
                    selectedServerHash = serversOnList[i + serversPerPage * page].Hash;
                }
            }
        }
        if (w == pageUp)
        {
            PageUp_();
        }
        if (w == pageDown)
        {
            PageDown_();
        }
        if (w == back)
        {
            OnBackPressed();
        }
        if (w == connect)
        {
            if (selectedServerHash != null)
            {
                Navigator.StartLogin(selectedServerHash, null, 0);
            }
        }
        if (w == connectToIp)
        {
            Navigator.StartConnectToIp();
        }
        if (w == refresh)
        {
            loaded = false;
            loading = true;
        }
        if (w == logout)
        {
            Preferences pref = Platform.GetPreferences();
            pref.Remove("Username");
            pref.Remove("Password");
            Platform.SetPreferences(pref);
            loggedInName.text = "";
        }
    }
}
