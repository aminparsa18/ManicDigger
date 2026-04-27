public class InfiniteMapChunked2dServer
{
    public IMapStorage? d_Map;
    public int chunksize = 16;
    public ushort[][]? chunks;

    public unsafe int GetBlock(int x, int y)
    {
        ushort[] chunk = GetChunk(x, y);
        return chunk[VectorIndexUtil.Index2d(x % chunksize, y % chunksize, chunksize)];
    }

    public ushort[] GetChunk(int x, int y)
    {
        ushort[] chunk = null;
        int kx = x / chunksize;
        int ky = y / chunksize;
        if (chunks[VectorIndexUtil.Index2d(kx, ky, d_Map.MapSizeX / chunksize)] == null)
        {
            chunk = new ushort[chunksize * chunksize];// (byte*)Marshal.AllocHGlobal(chunksize * chunksize);
            for (int i = 0; i < chunksize * chunksize; i++)
            {
                chunk[i] = 0;
            }
            chunks[VectorIndexUtil.Index2d(kx, ky, d_Map.MapSizeX / chunksize)] = chunk;
        }
        chunk = chunks[VectorIndexUtil.Index2d(kx, ky, d_Map.MapSizeX / chunksize)];
        return chunk;
    }

    public void SetBlock(int x, int y, int blocktype)
    {
        GetChunk(x, y)[VectorIndexUtil.Index2d(x % chunksize, y % chunksize, chunksize)] = (byte)blocktype;
    }

    public void Restart()
    {
        int n = (d_Map.MapSizeX / chunksize) * (d_Map.MapSizeY / chunksize);
        chunks = new ushort[n][];
        for (int i = 0; i < n; i++)
        {
            chunks[i] = null;
        }
    }

    public void ClearChunk(int x, int y)
    {
        int px = x / chunksize;
        int py = y / chunksize;
        chunks[VectorIndexUtil.Index2d(px, py, d_Map.MapSizeX / chunksize)] = null;
    }
}
