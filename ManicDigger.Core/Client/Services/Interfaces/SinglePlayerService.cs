public class SinglePlayerService : ISinglePlayerService
{
    public bool SinglePlayerServerAvailable { get; set; } = true;

    public bool SinglePlayerServerExit { get; set; }

    public DummyNetwork SinglePlayerServerNetwork { get; set; }

    public Action<string> StartSinglePlayerServer;
    public bool SinglePlayerServerLoaded { get; set; }

    public void SinglePlayerServerStart(string saveFilename)
    {
        SinglePlayerServerExit = false;
        StartSinglePlayerServer(saveFilename);
    }
}
