public interface IVoxelMap
{
    Chunk[] Chunks { get; }
    int Mapsizexchunks { get; }
    int Mapsizeychunks { get; }
    int Mapsizezchunks { get; }

    int MapSizeX { get; set; }
    int MapSizeY { get; set; }
    int MapSizeZ { get; set; }

    int GetBlock(int x, int y, int z);
    int GetBlockValid(int x, int y, int z);
    Chunk GetChunk(int x, int y, int z);
    Chunk GetChunkAt(int cx, int cy, int cz);
    void GetMapPortion(int[] outPortion, int x, int y, int z, int portionsizex, int portionsizey, int portionsizez);
    bool IsChunkRendered(int cx, int cy, int cz);
    bool IsValidChunkPos(int cx, int cy, int cz);
    bool IsValidPos(int x, int y, int z);
    int MaybeGetLight(int x, int y, int z);
    void Reset(int sizex, int sizey, int sizez);
    void SetBlockDirty(int x, int y, int z);
    void SetBlockRaw(int x, int y, int z, int tileType);
    void SetChunkDirty(int cx, int cy, int cz, bool dirty, bool blockschanged);
    void SetChunksAroundDirty(int cx, int cy, int cz);
    void SetMapPortion(int x, int y, int z, int[] chunk, int sizeX, int sizeY, int sizeZ);
}