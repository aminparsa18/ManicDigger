public class ServerEntityId
{
    internal int chunkx;
    internal int chunky;
    internal int chunkz;
    internal int id;

    internal ServerEntityId Clone()
    {
        ServerEntityId ret = new()
        {
            chunkx = chunkx,
            chunky = chunky,
            chunkz = chunkz,
            id = id
        };
        return ret;
    }
}
