//Block definition:
//
//      Z
//      |
//      |
//      |
//      +----- X
//     /
//    /
//   Y
//

public interface ITerrainChunkTesselator
{
    float BlockShadow { get; set; }
    bool DarkenBlockSidesOption { get; set; }
    int TerrainTexturesPerAtlas { get; set; }
    int[] TerrainTextures1d { get; set; }
    Dictionary<int, int[]> TextureId { get; set; }
    bool ENABLE_TEXTURE_TILING { get; set; }
    bool EnableSmoothLight { get; set; }

    VerticesIndicesToLoad[] MakeChunk(int x, int y, int z, int[] chunk18, byte[] shadows18, float[] lightlevels_, out int retCount);
    void RefreshBlockTypeCache();
    void Start();
}