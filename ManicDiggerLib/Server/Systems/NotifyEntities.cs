using OpenTK.Mathematics;

/// <summary>
/// Server system responsible for keeping each connected client's view of the world
/// in sync. Every tick it:
/// <list type="bullet">
///   <item>Applies pending position overrides to bot entities.</item>
///   <item>Notifies each human client of player spawns/despawns (<see cref="NotifyPlayers"/>).</item>
///   <item>Sends position and orientation updates for all nearby players, rate-limited
///         to <see cref="PlayerPositionUpdatesPerSecond"/> (<see cref="NotifyPlayerPositions"/>).</item>
///   <item>Manages the per-client entity spawn/despawn/update cycle for world entities,
///         rate-limited to <see cref="EntityPositionUpdatesPerSecond"/> (<see cref="NotifyEntities"/>).</item>
/// </list>
/// </summary>
public class ServerSystemNotifyEntities : ServerSystem
{
    private const int PlayerPositionUpdatesPerSecond = 10;
    private const int EntityPositionUpdatesPerSecond = 10;
    private const int SpawnMaxEntities = 32;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnUpdate(Server server, float dt)
    {
        foreach (var k in server.clients)
        {
            ClientOnServer client = k.Value;

            if (client.IsBot)
            {
                // Apply position overrides to keep bot positions current;
                // bots do not receive any other position packets.
                if (client.positionOverride == null) continue;
                client.entity.position = client.positionOverride;
                client.positionOverride = null;
                continue;
            }

            NotifyPlayers(server, k.Key);
            NotifyPlayerPositions(server, k.Key, dt);
            NotifyEntities(server, k.Key, dt);
        }
    }

    // -------------------------------------------------------------------------
    // Player spawn notifications
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends <c>EntitySpawn</c> packets to <paramref name="clientId"/> for any
    /// players whose dirty flag has been set since the last notification.
    /// </summary>
    private static void NotifyPlayers(Server server, int clientId)
    {
        ClientOnServer client = server.clients[clientId];

        foreach (var k in server.clients)
        {
            if (k.Value.state != ClientStateOnServer.Playing) continue;
            if (!client.playersDirty[k.Key]) continue;

            Packet_ServerEntity entity = ToNetworkEntity(server.serverPlatform, k.Value.entity);
            server.SendPacket(clientId, ServerPackets.EntitySpawn(k.Key, entity));
            client.playersDirty[k.Key] = false;
        }
    }

    // -------------------------------------------------------------------------
    // Player position updates
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends <c>EntityPositionAndOrientation</c> packets to <paramref name="clientId"/>
    /// for all nearby playing clients, throttled to <see cref="PlayerPositionUpdatesPerSecond"/>.
    /// <para>
    /// Spectating players are only visible to other spectators. Position overrides
    /// (used by the server to push corrections) are applied to the client's own entity
    /// before broadcasting.
    /// </para>
    /// </summary>
    private void NotifyPlayerPositions(Server server, int clientId, float dt)
    {
        ClientOnServer client = server.clients[clientId];
        client.notifyPlayerPositionsAccum += dt;
        if (client.notifyPlayerPositionsAccum < (One / PlayerPositionUpdatesPerSecond)) return;
        client.notifyPlayerPositionsAccum = 0;

        foreach (var k in server.clients)
        {
            if (k.Value.state != ClientStateOnServer.Playing) continue;
            if (!client.IsSpectator && k.Value.IsSpectator) continue;

            if (k.Key == clientId)
            {
                // Apply any server-side position correction for the local player
                if (k.Value.positionOverride == null) continue;
                k.Value.entity.position = k.Value.positionOverride;
                k.Value.positionOverride = null;
            }
            else
            {
                // Skip players beyond the configured draw distance
                Vector3i otherPos = Server.PlayerBlockPosition(server.clients[k.Key]);
                Vector3i selfPos = Server.PlayerBlockPosition(server.clients[clientId]);
                int drawDistanceSq = server.config.PlayerDrawDistance * server.config.PlayerDrawDistance;
                if (Server.DistanceSquared(otherPos, selfPos) > drawDistanceSq) continue;
            }

            Packet_PositionAndOrientation position =
                ToNetworkEntityPosition(server.serverPlatform, server.clients[k.Key].entity.position);
            server.SendPacket(clientId, ServerPackets.EntityPositionAndOrientation(k.Key, position));
        }
    }

    // -------------------------------------------------------------------------
    // World entity updates
    // -------------------------------------------------------------------------

    /// <summary>
    /// Manages the per-client world entity lifecycle, throttled to
    /// <see cref="EntityPositionUpdatesPerSecond"/>. Each tick it:
    /// <list type="number">
    ///   <item>Runs <c>onupdateentity</c> handlers for all nearby entities.</item>
    ///   <item>Despawns entities that are no longer in range.</item>
    ///   <item>Spawns entities that have come into range.</item>
    ///   <item>Re-sends spawn packets for entities flagged as dirty (<c>updateEntity</c>).</item>
    /// </list>
    /// Entity slot IDs on the client are offset by 64 to avoid colliding with player IDs.
    /// </summary>
    private void NotifyEntities(Server server, int clientId, float dt)
    {
        ClientOnServer client = server.clients[clientId];
        client.notifyEntitiesAccum += dt;
        if (client.notifyEntitiesAccum < (One / EntityPositionUpdatesPerSecond)) return;
        client.notifyEntitiesAccum = 0;

        // --- Collect nearest entities ---
        var nearestEntities = new ServerEntityId[SpawnMaxEntities];
        FindNearEntities(server, client, SpawnMaxEntities, nearestEntities);

        // --- Run update handlers ---
        foreach (ServerEntityId e in nearestEntities)
        {
            if (e == null) continue;
            foreach (var handler in server.modEventHandlers.onupdateentity)
                handler(e.chunkx, e.chunky, e.chunkz, e.id);
        }

        // --- Despawn entities that left range ---
        for (int i = 0; i < client.spawnedEntitiesCount; i++)
        {
            ServerEntityId e = client.spawnedEntities[i];
            if (e == null) continue;
            if (Contains(nearestEntities, SpawnMaxEntities, e)) continue;

            client.spawnedEntities[i] = null;
            server.SendPacket(clientId, ServerPackets.EntityDespawn(64 + i));
        }

        // --- Spawn entities that entered range ---
        for (int i = 0; i < SpawnMaxEntities; i++)
        {
            ServerEntityId e = nearestEntities[i];
            if (e == null) continue;
            if (Contains(client.spawnedEntities, SpawnMaxEntities, e)) continue;

            int slotId = IndexOfNull(client.spawnedEntities, client.spawnedEntitiesCount);
            client.spawnedEntities[slotId] = e.Clone();

            ServerEntity entity = GetEntity(server, e);
            server.SendPacket(clientId, ServerPackets.EntitySpawn(64 + slotId, ToNetworkEntity(server.serverPlatform, entity)));
        }

        // --- Re-send dirty entity data ---
        for (int i = 0; i < SpawnMaxEntities; i++)
        {
            if (!client.updateEntity[i]) continue;
            client.updateEntity[i] = false;

            ServerEntityId e = client.spawnedEntities[i];
            ServerEntity entity = GetEntity(server, e);
            server.SendPacket(clientId, ServerPackets.EntitySpawn(64 + i, ToNetworkEntity(server.serverPlatform, entity)));
        }
    }

    // -------------------------------------------------------------------------
    // Entity search
    // -------------------------------------------------------------------------

    /// <summary>
    /// Populates <paramref name="result"/> with up to <paramref name="maxCount"/>
    /// world entities from the 3×3×3 chunk neighbourhood around the player,
    /// sorted nearest-first.
    /// </summary>
    private static void FindNearEntities(Server server, ClientOnServer client, int maxCount, ServerEntityId[] result)
    {
        int playerX = client.PositionMul32GlX / 32;
        int playerY = client.PositionMul32GlZ / 32;
        int playerZ = client.PositionMul32GlY / 32;

        var candidates = new List<ServerEntityId>();

        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int chunkX = playerX / Server.chunksize + dx;
                    int chunkY = playerY / Server.chunksize + dy;
                    int chunkZ = playerZ / Server.chunksize + dz;

                    if (!MapUtil.IsValidChunkPos(server.d_Map, chunkX, chunkY, chunkZ, Server.chunksize)) continue;

                    ServerChunk chunk = server.d_Map.GetChunk(
                        chunkX * Server.chunksize,
                        chunkY * Server.chunksize,
                        chunkZ * Server.chunksize);

                    if (chunk?.Entities == null) continue;

                    for (int i = 0; i < chunk.EntitiesCount; i++)
                    {
                        if (chunk.Entities[i]?.position == null) continue;
                        candidates.Add(new ServerEntityId { chunkx = chunkX, chunky = chunkY, chunkz = chunkZ, id = i });
                    }
                }

        var playerPos = new Vector3i(
            client.PositionMul32GlX / 32,
            client.PositionMul32GlY / 32,
            client.PositionMul32GlZ / 32);

        candidates.Sort((a, b) =>
        {
            Vector3i posA = EntityBlockPosition(server, a);
            Vector3i posB = EntityBlockPosition(server, b);
            return Server.DistanceSquared(posA, playerPos)
                  .CompareTo(Server.DistanceSquared(posB, playerPos));
        });

        int count = Math.Min(candidates.Count, maxCount);
        for (int i = 0; i < count; i++)
            result[i] = candidates[i];
    }

    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the first null slot index in <paramref name="list"/> up to <paramref name="count"/>.
    /// Returns <c>-1</c> if no null slot exists.
    /// </summary>
    private static int IndexOfNull(ServerEntityId[] list, int count)
    {
        for (int i = 0; i < count; i++)
            if (list[i] == null) return i;
        return -1;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="value"/> is present in the first
    /// <paramref name="count"/> elements of <paramref name="list"/>, matched by
    /// chunk coordinates and entity ID.
    /// </summary>
    private static bool Contains(ServerEntityId[] list, int count, ServerEntityId value)
    {
        for (int i = 0; i < count; i++)
        {
            ServerEntityId s = list[i];
            if (s != null
                && s.chunkx == value.chunkx
                && s.chunky == value.chunky
                && s.chunkz == value.chunkz
                && s.id == value.id)
                return true;
        }
        return false;
    }

    /// <summary>Retrieves the <see cref="ServerEntity"/> for a given <see cref="ServerEntityId"/>.</summary>
    private static ServerEntity GetEntity(Server server, ServerEntityId id) =>
        server.d_Map.GetChunk(
            id.chunkx * Server.chunksize,
            id.chunky * Server.chunksize,
            id.chunkz * Server.chunksize)
        .Entities[id.id];

    /// <summary>
    /// Returns the block-space position of a world entity as a <see cref="Vector3i"/>.
    /// </summary>
    private static Vector3i EntityBlockPosition(Server server, ServerEntityId id)
    {
        ServerEntityPositionAndOrientation pos = GetEntity(server, id).position;
        return new Vector3i((int)pos.x, (int)pos.y, (int)pos.z);
    }

    // -------------------------------------------------------------------------
    // Network packet helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts a server-side position/orientation to the fixed-point network
    /// representation (coordinates multiplied by 32).
    /// </summary>
    private static Packet_PositionAndOrientation ToNetworkEntityPosition(
        ServerPlatform platform, ServerEntityPositionAndOrientation position) =>
        new()
        {
            X = (int)(position.x * 32),
            Y = (int)(position.y * 32),
            Z = (int)(position.z * 32),
            Heading = position.heading,
            Pitch = position.pitch,
            Stance = position.stance
        };

    /// <summary>
    /// Converts a <see cref="ServerEntity"/> to its full network packet representation,
    /// mapping each optional component (model, name, text, push, area) only when present.
    /// </summary>
    private static Packet_ServerEntity ToNetworkEntity(ServerPlatform platform, ServerEntity entity)
    {
        var p = new Packet_ServerEntity();

        if (entity.position != null)
            p.Position = ToNetworkEntityPosition(platform, entity.position);

        if (entity.drawModel != null)
            p.DrawModel = new Packet_ServerEntityAnimatedModel
            {
                EyeHeight = (int)(entity.drawModel.eyeHeight * 32),
                Model_ = entity.drawModel.model,
                ModelHeight = (int)(entity.drawModel.modelHeight * 32),
                Texture_ = entity.drawModel.texture,
                DownloadSkin = entity.drawModel.downloadSkin ? 1 : 0
            };

        if (entity.drawName != null)
            p.DrawName_ = new Packet_ServerEntityDrawName
            {
                Name = entity.drawName.name,
                Color = entity.drawName.color,
                OnlyWhenSelected = entity.drawName.onlyWhenSelected,
                ClientAutoComplete = entity.drawName.clientAutoComplete
            };

        if (entity.drawText != null)
            p.DrawText = new Packet_ServerEntityDrawText
            {
                Dx = (int)(entity.drawText.dx * 32),
                Dy = (int)(entity.drawText.dy * 32),
                Dz = (int)(entity.drawText.dz * 32),
                Rotx = (int)entity.drawText.rotx,
                Roty = (int)entity.drawText.roty,
                Rotz = (int)entity.drawText.rotz,
                Text = entity.drawText.text
            };

        if (entity.push != null)
            p.Push = new Packet_ServerEntityPush
            {
                RangeFloat = (int)(entity.push.range * 32)
            };

        if (entity.drawArea != null)
            p.DrawArea = new Packet_ServerEntityDrawArea
            {
                X = entity.drawArea.x,
                Y = entity.drawArea.y,
                Z = entity.drawArea.z,
                Sizex = entity.drawArea.sizex,
                Sizey = entity.drawArea.sizey,
                Sizez = entity.drawArea.sizez,
                VisibleToClientId = entity.drawArea.visibleToClientId
            };

        p.Usable = entity.usable;
        return p;
    }
}