#region Using Statements
using ManicDigger.ClientNative;
using OpenTK.Graphics;
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

    private CrashReporter crashreporter;

    private static void Log(string msg)
    {
        File.AppendAllText("debug.log", $"{DateTime.Now}: {msg}\n");
    }

    private void Start(string[] args)
    {
        try
        {
            string appPath = Path.GetDirectoryName(Application.ExecutablePath);
            if (!Debugger.IsAttached)
            {
                Environment.CurrentDirectory = appPath;
            }

            GamePlatformNative platform = new()
            {
                crashreporter = crashreporter,
                singlePlayerServerDummyNetwork = dummyNetwork
            };
            platform.SetExit(exit);

            this.platform = platform;
            platform.StartSinglePlayerServer = (filename) => { savefilename = filename; new Thread(ServerThreadStart).Start(); };

            Log("Creating GameWindowNative");
            GraphicsMode mode = new(OpenTK.DisplayDevice.Default.BitsPerPixel, 24);
            using GameWindowNative game = new(mode);
            game.VSync = OpenTK.VSyncMode.Adaptive;
            platform.window = game;
            game.platform = platform;
            MainMenu mainmenu = new();
            mainmenu.Start(platform);
            ReadArgs(mainmenu, args);
            platform.Start();
            game.Run();
        }
        catch (Exception ex)
        {
            Log($"Start EXCEPTION: {ex}");
            File.AppendAllText("debug.log", ex.StackTrace + "\n");
        }
    }

    private static void ReadArgs(MainMenu mainmenu, string[] args)
	{
		if (args.Length > 0)
		{
			var connectdata = ConnectData.FromUri(new GamePlatformNative().ParseUri(args[0]));
			mainmenu.StartGame(false, null, connectdata);
		}
	}

    private DummyNetwork dummyNetwork;
    private string savefilename;
	public GameExit exit = new();
    private GamePlatformNative platform;

	public void ServerThreadStart()
	{
		try
		{
            Server server = new()
            {
                SaveFilenameOverride = savefilename,
                exit = exit,
                mainSockets = new NetServer[3]
            };
            DummyNetServer netServer = new();
            netServer.SetPlatform(new GamePlatformNative());
            netServer.SetNetwork(dummyNetwork);
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
