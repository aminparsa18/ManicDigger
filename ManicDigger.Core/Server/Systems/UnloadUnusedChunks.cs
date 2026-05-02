using OpenTK.Mathematics;

/// <summary>
/// Iterates through all map chunks and unloads those no longer within any human
/// player's view. Up to 100 chunks are inspected per tick; at most one chunk is
/// actually unloaded per tick to spread the I/O cost of saving dirty chunks.
/// <para>
/// The unload distance is 1.8× the configured draw distance to add a hysteresis
/// buffer — chunks are loaded in a square pattern but unloaded in a larger area,
/// preventing them from being unloaded and immediately reloaded at view-distance
/// edges.
/// </para>
/// </summary>
public class ServerSystemUnloadUnusedChunks : ServerSystem
{
    private int iterationIndex;

    // Ratio of unload distance to draw distance — larger than 1 to prevent
    // oscillation at the boundary between loaded and unloaded chunks.
    private const float UnloadDistanceMultiplier = 1.8f;

    // Chunks inspected per tick. Kept small to avoid stalling the server loop.
    private const int InspectionsPerTick = 100;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnUpdate(Server server, float dt)
    {
        int totalChunks = server.mapsizexchunks() * server.mapsizeychunks() * server.mapsizezchunks();
        int chunksX = server.mapsizexchunks();
        int chunksY = server.mapsizeychunks();

        for (int i = 0; i < InspectionsPerTick; i++)
        {
            Vector3i chunkPos = new();
            VectorIndexUtil.PosInt(iterationIndex, chunksX, chunksY, ref chunkPos);

            ServerChunk chunk = server.Map.GetChunkValid(chunkPos.X, chunkPos.Y, chunkPos.Z);

            if (chunk != null && ShouldUnload(server, chunkPos))
            {
                UnloadChunk(server, chunkPos, chunk);
                return; // one unload per tick
            }

            iterationIndex = (iterationIndex + 1) % totalChunks;
        }
    }

    // -------------------------------------------------------------------------
    // Unload decision
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns <c>true</c> if no human player is within the unload distance of
    /// <paramref name="chunkPos"/>. Bot players are ignored — they do not keep
    /// chunks resident.
    /// </summary>
    private static bool ShouldUnload(Server server, Vector3i chunkPos)
    {
        var globalPos = ChunkToGlobalPos(chunkPos);
        int unloadDist = (int)(server.ChunkDrawDistance * Server.ChunkSize * UnloadDistanceMultiplier);
        int unloadDistSq = unloadDist * unloadDist;

        foreach (var (_, client) in server.Clients)
        {
            if (client.IsBot)
            {
                continue;
            }

            if (VectorUtils.DistanceSquared(Server.PlayerBlockPosition(client), globalPos) <= unloadDistSq)
            {
                return false;
            }
        }

        return true;
    }

    // -------------------------------------------------------------------------
    // Unload execution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Saves the chunk to disk if it has unsaved changes, then evicts it from
    /// memory and marks it unseen for all connected clients so it will be
    /// re-sent if a player re-enters the area.
    /// </summary>
    private static void UnloadChunk(Server server, Vector3i chunkPos, ServerChunk chunk)
    {
        if (chunk.DirtyForSaving)
        {
            server.DoSaveChunk(chunkPos.X, chunkPos.Y, chunkPos.Z, chunk);
        }

        server.Map.SetChunkValid(chunkPos.X, chunkPos.Y, chunkPos.Z, null);

        foreach (var (clientId, _) in server.Clients)
        {
            server.ClientSeenChunkRemove(clientId, chunkPos.X, chunkPos.Y, chunkPos.Z);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Converts chunk-space coordinates to block-space (global) coordinates.</summary>
    private static Vector3i ChunkToGlobalPos(Vector3i chunkPos)
        => new(chunkPos.X * Server.ChunkSize,
            chunkPos.Y * Server.ChunkSize,
            chunkPos.Z * Server.ChunkSize);
}