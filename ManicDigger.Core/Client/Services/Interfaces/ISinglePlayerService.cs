
// ─────────────────────────────────────────────────────────────────────────────
// Single-player server lifecycle + casting helpers
// ─────────────────────────────────────────────────────────────────────────────

public interface ISinglePlayerService
{
    bool SinglePlayerServerAvailable { get; set; }
    void SinglePlayerServerStart(string saveFilename);
    bool SinglePlayerServerExit { get; set; }
    bool SinglePlayerServerLoaded { get;set; }
    DummyNetwork SinglePlayerServerNetwork { get; set; }
}