namespace ManicDigger.Worker;

public enum ChunkWorkType
{
    Generate,       // world gen for a new chunk
    Tessellate,     // rebuild mesh after block change
    RelightBase,    // LightBase pass only
    RelightFull,    // LightBase + LightBetweenChunks
}