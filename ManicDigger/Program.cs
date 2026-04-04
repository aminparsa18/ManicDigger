#region Using Statements
using ManicDigger.ClientNative;
using OpenTK.Windowing.Common;
using System.Diagnostics;
#endregion

public class ManicDiggerProgram
{
	[STAThread]
	public static void Main(string[] args)
	{
		#if !DEBUG
		//Catch unhandled exceptions
		CrashReporter.DefaultFileName = "ManicDiggerClientCrash.txt";
		CrashReporter.EnableGlobalExceptionHandling(false);
		#endif

		new ManicDiggerProgram(args);
	}

	public ManicDiggerProgram(string[] args)
	{
		dummyNetwork = new DummyNetwork();
		dummyNetwork.Start(new MonitorObject(), new MonitorObject());

		#if !DEBUG
		crashreporter = new CrashReporter();
		crashreporter.Start(delegate { Start(args); });
		#else
		Start(args);
		#endif
	}

	CrashReporter crashreporter;

    private static void Log(string msg)
    {
        File.AppendAllText("debug.log", $"{DateTime.Now}: {msg}\n");
    }

    private void Start(string[] args)
    {
        Log("Start() called");
        try
        {
            Log("Before ApplicationConfiguration");
            global::System.Windows.Forms.Application.EnableVisualStyles();
            global::System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            global::System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Log("After ApplicationConfiguration");

            string appPath = Path.GetDirectoryName(Application.ExecutablePath);
            Log($"appPath: {appPath}");
            if (!Debugger.IsAttached)
            {
                System.Environment.CurrentDirectory = appPath;
            }

            Log("Creating MainMenu and platform");
            MainMenu mainmenu = new MainMenu();
            GamePlatformNative platform = new GamePlatformNative();
            platform.SetExit(exit);
            platform.crashreporter = crashreporter;
            platform.singlePlayerServerDummyNetwork = dummyNetwork;
            this.platform = platform;
            platform.StartSinglePlayerServer = (filename) => { savefilename = filename; new Thread(ServerThreadStart).Start(); };

            Log("Creating GameWindowNative");
            using GameWindowNative game = new();
            game.VSync = VSyncMode.Adaptive;
            platform.window = game;
            game.platform = platform;

            //game.OnLoadAction = () =>
            //{
            //    Log("OnLoad fired");
            //    try
            //    {
                    Log("mainmenu.Start");
                    mainmenu.Start(platform);
                    Log("ReadArgs");
                    ReadArgs(mainmenu, args);
                    Log("platform.Start");
                    platform.Start();
                    Log("platform.Start done");
                //}
                //catch (Exception ex)
                //{
                //    Log($"OnLoad EXCEPTION: {ex}");
                //    File.AppendAllText("debug.log", ex.StackTrace + "\n");
                //    game.Close();
                //}
            //};

            Log("game.Run()");
            game.Run();
            Log("game.Run() returned");
        }
        catch (Exception ex)
        {
            Log($"Start EXCEPTION: {ex}");
            File.AppendAllText("debug.log", ex.StackTrace + "\n");
        }
    }

    void ReadArgs(MainMenu mainmenu, string[] args)
	{
		if (args.Length > 0)
		{
			ConnectData connectdata = new ConnectData();
			connectdata = ConnectData.FromUri(new GamePlatformNative().ParseUri(args[0]));

			mainmenu.StartGame(false, null, connectdata);
		}
	}

	DummyNetwork dummyNetwork;
	string savefilename;
	public GameExit exit = new GameExit();
	GamePlatformNative platform;

	public void ServerThreadStart()
	{
		try
		{
			Server server = new Server();
			server.SaveFilenameOverride = savefilename;
			server.exit = exit;
			DummyNetServer netServer = new DummyNetServer();
			netServer.SetPlatform(new GamePlatformNative());
			netServer.SetNetwork(dummyNetwork);
			server.mainSockets = new NetServer[3];
			server.mainSockets[0] = netServer;
			for (; ; )
			{
				server.Process();
				Thread.Sleep(1);
				platform.singlePlayerServerLoaded = true;
				if (exit != null && exit.GetExit()) { server.Stop(); break; }
				if (platform.singlepLayerServerExit)
				{
					// Exit thread and reset shutdown variable
					server.Exit();
					platform.singlepLayerServerExit = false;
				}
			}
			exit.SetExit(false);
		}
		catch (Exception e)
		{
			MessageBox.Show(e.ToString());
		}
	}
}
