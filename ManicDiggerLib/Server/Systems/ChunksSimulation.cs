using OpenTK.Mathematics;

public class ServerSystemChunksSimulation : ServerSystem
{
    public int[] MonsterTypesUnderground = [1, 2];
    public int[] MonsterTypesOnGround = [0, 3, 4];

    private const int ChunksSimulated = 1;
    private const int MonsterSpawnMaxTries = 500;
    private const int MinMonstersPerChunk = 1;

    private int simulationInterval = -1;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    protected override void Initialize(Server server)
    {
        // Frames between full chunk updates: once per 10 minutes of simulation time
        simulationInterval = (int)(1f / server.SIMULATION_STEP_LENGTH) * 60 * 10;
    }

    protected override void OnUpdate(Server server, float dt)
    {
        unchecked
        {
            for (int i = 0; i < ChunksSimulated; i++)
                SimulateNextChunk(server);
        }
    }

    // -------------------------------------------------------------------------
    // Chunk simulation
    // -------------------------------------------------------------------------

    private void SimulateNextChunk(Server server)
    {
        unchecked
        {
            foreach (var k in server.clients)
            {
                Vector3i playerPos = Server.PlayerBlockPosition(k.Value);

                long oldestTime = long.MaxValue;
                Vector3i oldestPos = default;

                foreach (Vector3i chunkPos in ChunksAroundPlayer(server, playerPos))
                {
                    if (!MapUtil.IsValidPos(server.d_Map, chunkPos.X, chunkPos.Y, chunkPos.Z))
                        continue;

                    ServerChunk chunk = server.d_Map.GetChunkValid(
                        Server.invertChunk(chunkPos.X),
                        Server.invertChunk(chunkPos.Y),
                        Server.invertChunk(chunkPos.Z));

                    if (chunk?.Data == null) continue;

                    // Guard against future timestamps
                    if (chunk.LastUpdate > server.simulationcurrentframe)
                        chunk.LastUpdate = server.simulationcurrentframe;

                    if (chunk.LastUpdate < oldestTime)
                    {
                        oldestTime = chunk.LastUpdate;
                        oldestPos = chunkPos;
                    }

                    if (!chunk.IsPopulated)
                    {
                        chunk.IsPopulated = true;
                        PopulateChunk(server, chunkPos);
                    }
                }

                if (server.simulationcurrentframe - oldestTime > simulationInterval)
                {
                    UpdateChunk(server, oldestPos);

                    ServerChunk chunk = server.d_Map.GetChunkValid(
                        Server.invertChunk(oldestPos.X),
                        Server.invertChunk(oldestPos.Y),
                        Server.invertChunk(oldestPos.Z));

                    chunk.LastUpdate = server.simulationcurrentframe;
                    return;
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Chunk population
    // -------------------------------------------------------------------------

    private static void PopulateChunk(Server server, Vector3i chunkPos)
    {
        unchecked
        {
            var handlers = server.modEventHandlers.populatechunk;
            int x = (int)(chunkPos.X * Server.invertedChunkSize);
            int y = (int)(chunkPos.Y * Server.invertedChunkSize);
            int z = (int)(chunkPos.Z * Server.invertedChunkSize);
            for (int i = 0; i < handlers.Count; i++)
                handlers[i](x, y, z);
        }
    }

    // -------------------------------------------------------------------------
    // Chunk update (block ticks + monsters)
    // -------------------------------------------------------------------------

    private void UpdateChunk(Server server, Vector3i chunkPos)
    {
        unchecked
        {
            if (server.config.Monsters)
                AddMonsters(server, chunkPos);

            var blockTicks = server.modEventHandlers.blockticks;
            int tickCount = blockTicks.Count;

            for (int xx = 0; xx < Server.chunksize; xx++)
            {
                int px = xx + chunkPos.X;
                for (int yy = 0; yy < Server.chunksize; yy++)
                {
                    int py = yy + chunkPos.Y;
                    for (int zz = 0; zz < Server.chunksize; zz++)
                    {
                        int pz = zz + chunkPos.Z;
                        for (int i = 0; i < tickCount; i++)
                            blockTicks[i](px, py, pz);
                    }
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Chunk enumeration
    // -------------------------------------------------------------------------

    private static IEnumerable<Vector3i> ChunksAroundPlayer(Server server, Vector3i playerPos)
    {
        int zDrawDistance = Server.invertChunk(server.d_Map.MapSizeZ);
        unchecked
        {
            for (int x = -server.chunkdrawdistance; x <= server.chunkdrawdistance; x++)
                for (int y = -server.chunkdrawdistance; y <= server.chunkdrawdistance; y++)
                    for (int z = 0; z < zDrawDistance; z++)
                    {
                        var p = new Vector3i(
                            playerPos.X + x * Server.chunksize,
                            playerPos.Y + y * Server.chunksize,
                            z * Server.chunksize);

                        if (MapUtil.IsValidPos(server.d_Map, p.X, p.Y, p.Z))
                            yield return p;
                    }
        }
    }

    // -------------------------------------------------------------------------
    // Monster spawning
    // -------------------------------------------------------------------------

    public void AddMonsters(Server server, Vector3i chunkPos)
    {
        ServerChunk chunk = server.d_Map.GetChunkValid(
            chunkPos.X / Server.chunksize,
            chunkPos.Y / Server.chunksize,
            chunkPos.Z / Server.chunksize);

        for (int tries = 0; chunk.Monsters.Count < MinMonstersPerChunk && tries < MonsterSpawnMaxTries; tries++)
        {
            int px = chunkPos.X + server.rnd.Next(Server.chunksize);
            int py = chunkPos.Y + server.rnd.Next(Server.chunksize);
            int pz = chunkPos.Z + server.rnd.Next(Server.chunksize);

            if (!IsValidSpawnPosition(server, px, py, pz)) continue;

            int monsterType = ChooseMonsterType(server, px, py, pz);

            if (IsOpenGround(server, px, py, pz))
            {
                chunk.Monsters.Add(new Monster
                {
                    X = px,
                    Y = py,
                    Z = pz,
                    Id = server.LastMonsterId++,
                    Health = 20,
                    MonsterType = monsterType
                });
            }
        }
    }

    private static bool IsValidSpawnPosition(Server server, int px, int py, int pz) =>
        MapUtil.IsValidPos(server.d_Map, px, py, pz) &&
        MapUtil.IsValidPos(server.d_Map, px, py, pz + 1) &&
        MapUtil.IsValidPos(server.d_Map, px, py, pz - 1);

    private int ChooseMonsterType(Server server, int px, int py, int pz)
    {
        int surfaceHeight = MapUtil.blockheight(server.d_Map, 0, px, py);
        return pz >= surfaceHeight
            ? MonsterTypesOnGround[server.rnd.Next(MonsterTypesOnGround.Length)]
            : MonsterTypesUnderground[server.rnd.Next(MonsterTypesUnderground.Length)];
    }

    private static bool IsOpenGround(Server server, int px, int py, int pz) =>
        server.d_Map.GetBlock(px, py, pz) == 0 &&
        server.d_Map.GetBlock(px, py, pz + 1) == 0 &&
        server.d_Map.GetBlock(px, py, pz - 1) != 0 &&
        !server.BlockTypes[server.d_Map.GetBlock(px, py, pz - 1)].IsFluid();
}