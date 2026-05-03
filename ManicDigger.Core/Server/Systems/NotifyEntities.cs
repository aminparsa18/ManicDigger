using ManicDigger;
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

    public ServerSystemNotifyEntities(IModEvents modEvents) : base(modEvents)
    {
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void OnUpdate(Server server, float dt)
    {
        foreach (var k in server.Clients)
        {
            ClientOnServer client = k.Value;

            if (client.IsBot)
            {
                // Apply position overrides to keep bot positions current;
                // bots do not receive any other position packets.
                if (client.PositionOverride == null)
                {
                    continue;
                }

                client.Entity.Position = client.PositionOverride;
                client.PositionOverride = null;
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
        ClientOnServer client = server.Clients[clientId];

        foreach (var k in server.Clients)
        {
            if (k.Value.State != ClientStateOnServer.Playing)
            {
                continue;
            }

            if (!client.PlayersDirty[k.Key])
            {
                continue;
            }

            Packet_ServerEntity entity = ToNetworkEntity(k.Value.Entity);
            server.SendPacket(clientId, ServerPackets.EntitySpawn(k.Key, entity));
            client.PlayersDirty[k.Key] = false;
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
        ClientOnServer client = server.Clients[clientId];
        client.NotifyPlayerPositionsAccum += dt;
        if (client.NotifyPlayerPositionsAccum < (1f / PlayerPositionUpdatesPerSecond))
        {
            return;
        }

        client.NotifyPlayerPositionsAccum = 0;

        foreach (var k in server.Clients)
        {
            if (k.Value.State != ClientStateOnServer.Playing)
            {
                continue;
            }

            if (!client.IsSpectator && k.Value.IsSpectator)
            {
                continue;
            }

            if (k.Key == clientId)
            {
                // Apply any server-side position correction for the local player
                if (k.Value.PositionOverride == null)
                {
                    continue;
                }

                k.Value.Entity.Position = k.Value.PositionOverride;
                k.Value.PositionOverride = null;
            }
            else
            {
                // Skip players beyond the configured draw distance
                Vector3i otherPos = server.PlayerBlockPosition(server.Clients[k.Key]);
                Vector3i selfPos = server.PlayerBlockPosition(server.Clients[clientId]);
                int drawDistanceSq = server.Config.PlayerDrawDistance * server.Config.PlayerDrawDistance;
                if (VectorUtils.DistanceSquared(otherPos, selfPos) > drawDistanceSq)
                {
                    continue;
                }
            }

            Packet_PositionAndOrientation position =
                ToNetworkEntityPosition(server.Clients[k.Key].Entity.Position);
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
        ClientOnServer client = server.Clients[clientId];
        client.NotifyEntitiesAccum += dt;
        if (client.NotifyEntitiesAccum < (1f / EntityPositionUpdatesPerSecond))
        {
            return;
        }

        client.NotifyEntitiesAccum = 0;

        // --- Collect nearest entities ---
        ServerEntityId[] nearestEntities = new ServerEntityId[SpawnMaxEntities];
        FindNearEntities(server, client, SpawnMaxEntities, nearestEntities);

        // --- Run update handlers ---
        foreach (ServerEntityId e in nearestEntities)
        {
            if (e == null)
            {
                continue;
            }

            ModEvents.RaiseUpdateEntity(e.ChunkX, e.ChunkY, e.ChunkZ, e.Id);
        }

        // --- Despawn entities that left range ---
        for (int i = 0; i < client.SpawnedEntities.Length; i++)
        {
            ServerEntityId e = client.SpawnedEntities[i];
            if (e == null)
            {
                continue;
            }

            if (Contains(nearestEntities, SpawnMaxEntities, e))
            {
                continue;
            }

            client.SpawnedEntities[i] = null;
            server.SendPacket(clientId, ServerPackets.EntityDespawn(64 + i));
        }

        // --- Spawn entities that entered range ---
        for (int i = 0; i < SpawnMaxEntities; i++)
        {
            ServerEntityId e = nearestEntities[i];
            if (e == null)
            {
                continue;
            }

            if (Contains(client.SpawnedEntities, SpawnMaxEntities, e))
            {
                continue;
            }

            int slotId = IndexOfNull(client.SpawnedEntities, client.SpawnedEntities.Length);
            client.SpawnedEntities[slotId] = e.Clone();

            ServerEntity entity = GetEntity(server, e);
            server.SendPacket(clientId, ServerPackets.EntitySpawn(64 + slotId, ToNetworkEntity(entity)));
        }

        // --- Re-send dirty entity data ---
        for (int i = 0; i < SpawnMaxEntities; i++)
        {
            if (!client.UpdateEntity[i])
            {
                continue;
            }

            client.UpdateEntity[i] = false;

            ServerEntityId e = client.SpawnedEntities[i];
            ServerEntity entity = GetEntity(server, e);
            server.SendPacket(clientId, ServerPackets.EntitySpawn(64 + i, ToNetworkEntity(entity)));
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

        List<ServerEntityId> candidates = new();

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int chunkX = (playerX / server.ChunkSize) + dx;
                    int chunkY = (playerY / server.ChunkSize) + dy;
                    int chunkZ = (playerZ / server.ChunkSize) + dz;

                    if (!VectorUtils.IsValidChunkPos(server.Map, chunkX, chunkY, chunkZ, server.ChunkSize))
                    {
                        continue;
                    }

                    ServerChunk chunk = server.Map.GetChunk(
                        chunkX * server.ChunkSize,
                        chunkY * server.ChunkSize,
                        chunkZ * server.ChunkSize);

                    if (chunk?.Entities == null)
                    {
                        continue;
                    }

                    foreach (var (id, entity) in chunk.Entities)
                    {
                        if (entity?.Position == null)
                        {
                            continue;
                        }

                        candidates.Add(new ServerEntityId { ChunkX = chunkX, ChunkY = chunkY, ChunkZ = chunkZ, Id = id });
                    }
                }
            }
        }

        Vector3i playerPos = new(
            client.PositionMul32GlX / 32,
            client.PositionMul32GlY / 32,
            client.PositionMul32GlZ / 32);

        candidates.Sort((a, b) =>
        {
            Vector3i posA = EntityBlockPosition(server, a);
            Vector3i posB = EntityBlockPosition(server, b);
            return VectorUtils.DistanceSquared(posA, playerPos)
                  .CompareTo(VectorUtils.DistanceSquared(posB, playerPos));
        });

        int count = Math.Min(candidates.Count, maxCount);
        for (int i = 0; i < count; i++)
        {
            result[i] = candidates[i];
        }
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
        {
            if (list[i] == null)
            {
                return i;
            }
        }

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
                && s.ChunkX == value.ChunkX
                && s.ChunkY == value.ChunkY
                && s.ChunkZ == value.ChunkZ
                && s.Id == value.Id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Retrieves the <see cref="ServerEntity"/> for a given <see cref="ServerEntityId"/>.</summary>
    private static ServerEntity GetEntity(Server server, ServerEntityId id)
        => server.Map.GetChunk(
            id.ChunkX * server.ChunkSize,
            id.ChunkY * server.ChunkSize,
            id.ChunkZ * server.ChunkSize)
        .Entities[id.Id];

    /// <summary>
    /// Returns the block-space position of a world entity as a <see cref="Vector3i"/>.
    /// </summary>
    private static Vector3i EntityBlockPosition(Server server, ServerEntityId id)
    {
        ServerEntityPositionAndOrientation pos = GetEntity(server, id).Position;
        return new Vector3i((int)pos.X, (int)pos.Y, (int)pos.Z);
    }

    // -------------------------------------------------------------------------
    // Network packet helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts a server-side position/orientation to the fixed-point network
    /// representation (coordinates multiplied by 32).
    /// </summary>
    private static Packet_PositionAndOrientation ToNetworkEntityPosition(ServerEntityPositionAndOrientation position)
        => new()
        {
            X = (int)(position.X * 32),
            Y = (int)(position.Y * 32),
            Z = (int)(position.Z * 32),
            Heading = position.Heading,
            Pitch = position.Pitch,
            Stance = position.Stance
        };

    /// <summary>
    /// Converts a <see cref="ServerEntity"/> to its full network packet representation,
    /// mapping each optional component (model, name, text, push, area) only when present.
    /// </summary>
    private static Packet_ServerEntity ToNetworkEntity(ServerEntity entity)
    {
        Packet_ServerEntity p = new();

        if (entity.Position != null)
        {
            p.Position = ToNetworkEntityPosition(entity.Position);
        }

        if (entity.DrawModel != null)
        {
            p.DrawModel = new Packet_ServerEntityAnimatedModel
            {
                EyeHeight = (int)(entity.DrawModel.EyeHeight * 32),
                Model_ = entity.DrawModel.Model,
                ModelHeight = (int)(entity.DrawModel.ModelHeight * 32),
                Texture_ = entity.DrawModel.Texture,
                DownloadSkin = entity.DrawModel.DownloadSkin ? 1 : 0
            };
        }

        if (entity.DrawName != null)
        {
            p.DrawName_ = new Packet_ServerEntityDrawName
            {
                Name = entity.DrawName.Name,
                Color = entity.DrawName.Color,
                OnlyWhenSelected = entity.DrawName.OnlyWhenSelected,
                ClientAutoComplete = entity.DrawName.ClientAutoComplete
            };
        }

        if (entity.DrawText != null)
        {
            p.DrawText = new Packet_ServerEntityDrawText
            {
                Dx = (int)(entity.DrawText.Dx * 32),
                Dy = (int)(entity.DrawText.Dy * 32),
                Dz = (int)(entity.DrawText.Dz * 32),
                Rotx = (int)entity.DrawText.RotX,
                Roty = (int)entity.DrawText.RotY,
                Rotz = (int)entity.DrawText.RotZ,
                Text = entity.DrawText.Text
            };
        }

        if (entity.Push != null)
        {
            p.Push = new Packet_ServerEntityPush
            {
                RangeFloat = (int)(entity.Push.Range * 32)
            };
        }

        if (entity.DrawArea != null)
        {
            p.DrawArea = new Packet_ServerEntityDrawArea
            {
                X = entity.DrawArea.X,
                Y = entity.DrawArea.Y,
                Z = entity.DrawArea.Z,
                Sizex = entity.DrawArea.SizeX,
                Sizey = entity.DrawArea.SizeY,
                Sizez = entity.DrawArea.SizeZ,
                VisibleToClientId = entity.DrawArea.VisibleToClientId
            };
        }

        p.Usable = entity.Usable;
        return p;
    }
}