public class SinglePlayerService : ISinglePlayerService
{
    private bool singlePlayerServerAvailable = true;

    public bool SinglePlayerServerAvailable()
    {
        return singlePlayerServerAvailable;
    }

    public void SinglePlayerServerDisable()
    {
        singlePlayerServerAvailable = false;
    }

    public bool SinglePlayerServerExit { get; set; }

    public DummyNetwork singlePlayerServerDummyNetwork;
    public DummyNetwork SinglePlayerServerGetNetwork()
    {
        return singlePlayerServerDummyNetwork;
    }

    public Action<string> StartSinglePlayerServer;
    public bool SinglePlayerServerLoaded { get; set; }

    public void SinglePlayerServerStart(string saveFilename)
    {
        SinglePlayerServerExit = false;
        StartSinglePlayerServer(saveFilename);
    }
}
