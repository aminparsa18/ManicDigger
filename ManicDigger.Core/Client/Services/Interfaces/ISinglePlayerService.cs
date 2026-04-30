
// ─────────────────────────────────────────────────────────────────────────────
// Single-player server lifecycle + casting helpers
// ─────────────────────────────────────────────────────────────────────────────

public interface ISinglePlayerService
{
    bool SinglePlayerServerAvailable();
    void SinglePlayerServerStart(string saveFilename);
    bool SinglePlayerServerExit { get; set; }
    bool SinglePlayerServerLoaded { get;set; }
    void SinglePlayerServerDisable();
    DummyNetwork SinglePlayerServerGetNetwork();
}

