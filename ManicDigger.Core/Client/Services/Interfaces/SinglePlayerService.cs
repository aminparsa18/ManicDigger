public class SinglePlayerService(IDummyNetwork dummyNetwork) : ISinglePlayerService
{
    public bool SinglePlayerServerAvailable { get; set; } = true;

    public bool SinglePlayerServerExit { get; set; }

    public IDummyNetwork SinglePlayerServerNetwork { get; set; } = dummyNetwork;

    public bool SinglePlayerServerLoaded { get; set; }

    public void SinglePlayerServerStart(string saveFilename)
    {
        SinglePlayerServerExit = false;
    }
}