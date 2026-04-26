using OpenTK.Mathematics;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static ManicDigger.Mods.ModNetworkProcess;

/// <summary>
/// The main system for loading, unloading, and streaming map chunks to players.
/// Each tick it finds the nearest unseen chunk for every connected client and
/// sends it, continuing until either all dirty chunks are resolved or 10 ms of
/// wall time has elapsed to avoid blocking the server loop.
/// </summary>
public class ServerSystemNotifyMap : ServerSystem
{
    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnUpdate(Server server, float dt)
    {
        var stopwatch = Stopwatch.StartNew();
        bool sentAny = true;

        while (sentAny && stopwatch.ElapsedMilliseconds < 10)
        {
            sentAny = false;
            foreach (var (clientId, client) in server.clients)
            {
                if (client.state == ClientStateOnServer.Connecting) continue;

                Vector3i playerPos = Server.PlayerBlockPosition(client);
                Vector3i? nearest = FindNearestDirtyChunk(server, clientId, playerPos);

                if (nearest == null) continue;

                LoadAndSendChunk(server, clientId, nearest.Value, stopwatch);
                sentAny = true;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Chunk discovery
    // -------------------------------------------------------------------------

    /// <summary>
    /// Searches the area around <paramref name="playerPos"/> for the nearest
    /// chunk that has not yet been sent to <paramref name="clientId"/>.
    /// The search area is bounded by the configured draw distance.
    /// </summary>
    /// <returns>
    /// The chunk-space coordinates of the nearest unseen chunk,
    /// or <c>null</c> if all chunks in range have already been sent.
    /// </returns>
    private static Vector3i? FindNearestDirtyChunk(Server server, int clientId, Vector3i playerPos)
    {
        int px = playerPos.X / Server.chunksize;
        int py = playerPos.Y / Server.chunksize;
        int pz = playerPos.Z / Server.chunksize;

        int halfXY = MapAreaSize(server) / Server.chunksize / 2;
        int halfZ = MapAreaSizeZ(server) / Server.chunksize / 2;

        int startX = Math.Max(0, px - halfXY);
        int startY = Math.Max(0, py - halfXY);
        int startZ = Math.Max(0, pz - halfZ);
        int endX = Math.Min(server.mapsizexchunks() - 1, px + halfXY);
        int endY = Math.Min(server.mapsizeychunks() - 1, py + halfXY);
        int endZ = Math.Min(server.mapsizezchunks() - 1, pz + halfZ);

        int nearestDist = int.MaxValue;
        Vector3i? nearest = null;

        for (int x = startX; x <= endX; x++)
            for (int y = startY; y <= endY; y++)
                for (int z = startZ; z <= endZ; z++)
                {
                    if (server.ClientSeenChunk(clientId, x, y, z)) continue;

                    int dx = px - x;
                    int dy = py - y;
                    int dz = pz - z;
                    int dist = dx * dx + dy * dy + dz * dz;

                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = new Vector3i(x, y, z);
                    }
                }

        return nearest;
    }

    // -------------------------------------------------------------------------
    // Chunk loading and sending
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads the chunk at chunk-space position <paramref name="chunkPos"/> and
    /// sends it to <paramref name="clientId"/> if it has not already been sent.
    /// </summary>
    private static void LoadAndSendChunk(Server server, int clientId, Vector3i chunkPos, Stopwatch stopwatch)
    {
        server.LoadChunk(chunkPos.X, chunkPos.Y, chunkPos.Z);

        if (!server.ClientSeenChunk(clientId, chunkPos.X, chunkPos.Y, chunkPos.Z))
        {
            var globalPos = new Vector3i(
                chunkPos.X * Server.chunksize,
                chunkPos.Y * Server.chunksize,
                chunkPos.Z * Server.chunksize);

            SendChunk(server, clientId, globalPos, chunkPos);
        }
    }

    /// <summary>
    /// Sends a chunk and its associated heightmap data to a client.
    /// <list type="bullet">
    ///   <item>Empty chunks (solid air) are skipped — only the heightmap is sent.</item>
    ///   <item>Non-empty chunks are compressed and split into 1 KB parts before sending.</item>
    ///   <item>A final <c>Chunk_</c> packet is always sent to signal chunk completion.</item>
    /// </list>
    /// </summary>
    /// <param name="server">The running server instance.</param>
    /// <param name="clientId">The client to send the chunk to.</param>
    /// <param name="globalPos">Block-space origin of the chunk.</param>
    /// <param name="chunkPos">Chunk-space coordinates of the chunk.</param>
    public static void SendChunk(Server server, int clientId, Vector3i globalPos, Vector3i chunkPos)
    {
        ClientOnServer client = server.clients[clientId];
        ServerChunk chunk = server.d_Map.GetChunk(globalPos.X, globalPos.Y, globalPos.Z);
        server.ClientSeenChunkSet(clientId, chunkPos.X, chunkPos.Y, chunkPos.Z, server.simulationcurrentframe);

        bool isSolid = MapUtil.IsSolidChunk(chunk.Data);
        int firstBlock = chunk.Data?[0] ?? -1;
        int nonZero = chunk.Data?.Count(b => b != 0) ?? -1;
        DiagLog.Write($"SendChunk ({globalPos.X},{globalPos.Y},{globalPos.Z}): isSolid={isSolid} firstBlock={firstBlock} nonZero={nonZero} dataLen={chunk.Data?.Length ?? -1}");

        byte[] compressedChunk = null;

        if (!MapUtil.IsSolidChunk(chunk.Data) || chunk.Data[0] != 0)
        {
            // Compress and queue block data for sending
            compressedChunk = server.CompressChunkNetwork(chunk.Data);

            // Send heightmap for this column
            ReadOnlySpan<byte> heightmapBytes = MemoryMarshal.AsBytes(
                server.d_Map.GetHeightmapChunk(globalPos.X, globalPos.Y).AsSpan());

            var heightmapPacket = new Packet_ServerHeightmapChunk
            {
                X = globalPos.X,
                Y = globalPos.Y,
                SizeX = Server.chunksize,
                SizeY = Server.chunksize,
                CompressedHeightmap = server.d_NetworkCompression.Compress(heightmapBytes)
            };
            server.SendPacket(clientId, Server.Serialize(new Packet_Server
            {
                Id = Packet_ServerIdEnum.HeightmapChunk,
                HeightmapChunk = heightmapPacket
            }));
            client.heightmapchunksseen[new Vector2i(globalPos.X, globalPos.Y)] = server.simulationcurrentframe;
        }

        // Send block data in 1 KB parts
        if (compressedChunk != null)
        {
            foreach (byte[] part in Server.Parts(compressedChunk, 1024))
            {
                server.SendPacket(clientId, Server.Serialize(new Packet_Server
                {
                    Id = Packet_ServerIdEnum.ChunkPart,
                    ChunkPart = new Packet_ServerChunkPart { CompressedChunkPart = part }
                }));
            }
        }

        // Signal chunk completion
        server.SendPacket(clientId, Server.Serialize(new Packet_Server
        {
            Id = Packet_ServerIdEnum.Chunk_,
            Chunk_ = new Packet_ServerChunk
            {
                X = globalPos.X,
                Y = globalPos.Y,
                Z = globalPos.Z,
                SizeX = Server.chunksize,
                SizeY = Server.chunksize,
                SizeZ = Server.chunksize
            }
        }));
    }

    // -------------------------------------------------------------------------
    // Area size helpers
    // -------------------------------------------------------------------------

    /// <summary>Returns the XY map streaming area size in blocks based on the configured draw distance.</summary>
    public static int MapAreaSize(Server server) => server.chunkdrawdistance * Server.chunksize * 2;

    /// <summary>Returns the Z map streaming area size in blocks. Currently mirrors the XY area.</summary>
    public static int MapAreaSizeZ(Server server) => MapAreaSize(server);
}