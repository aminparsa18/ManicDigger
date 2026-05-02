using OpenTK.Mathematics;

public class ServerSystemChunksSimulation : ServerSystem
{
    private Random _rnd;
    private IBlockRegistry _blockRegistry;

    public ServerSystemChunksSimulation(IBlockRegistry blockRegistry)
    {
        _blockRegistry = blockRegistry;
    }

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
        _rnd = new Random();
    }

    protected override void OnUpdate(Server server, float dt)
    {
        unchecked
        {
            for (int i = 0; i < ChunksSimulated; i++)
            {
                SimulateNextChunk(server);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Chunk simulation
    // -------------------------------------------------------------------------

    private void SimulateNextChunk(Server server)
    {
        unchecked
        {
            foreach (var k in server.Clients)
            {
                Vector3i playerPos = Server.PlayerBlockPosition(k.Value);

                long oldestTime = long.MaxValue;
                Vector3i oldestPos = default;

                foreach (Vector3i chunkPos in ChunksAroundPlayer(server, playerPos))
                {
                    if (!VectorUtils.IsValidPos(server.Map, chunkPos.X, chunkPos.Y, chunkPos.Z))
                    {
                        continue;
                    }

                    ServerChunk chunk = server.Map.GetChunkValid(
                        Server.InvertChunk(chunkPos.X),
                        Server.InvertChunk(chunkPos.Y),
                        Server.InvertChunk(chunkPos.Z));

                    if (chunk?.Data == null)
                    {
                        continue;
                    }

                    // Guard against future timestamps
                    if (chunk.LastUpdate > server.SimulationCurrentFrame)
                    {
                        chunk.LastUpdate = server.SimulationCurrentFrame;
                    }

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

                if (server.SimulationCurrentFrame - oldestTime > simulationInterval)
                {
                    UpdateChunk(server, oldestPos);

                    ServerChunk chunk = server.Map.GetChunkValid(
                        Server.InvertChunk(oldestPos.X),
                        Server.InvertChunk(oldestPos.Y),
                        Server.InvertChunk(oldestPos.Z));

                    chunk.LastUpdate = server.SimulationCurrentFrame;
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
            var handlers = server.ModEventHandlers.PopulateChunk;
            int x = (int)(chunkPos.X * Server.InvertedChunkSize);
            int y = (int)(chunkPos.Y * Server.InvertedChunkSize);
            int z = (int)(chunkPos.Z * Server.InvertedChunkSize);
            for (int i = 0; i < handlers.Count; i++)
            {
                handlers[i](x, y, z);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Chunk update (block ticks + monsters)
    // -------------------------------------------------------------------------

    private void UpdateChunk(Server server, Vector3i chunkPos)
    {
        unchecked
        {
            if (server.Config.Monsters)
            {
                AddMonsters(server, chunkPos);
            }

            var blockTicks = server.ModEventHandlers.BlockTicks;
            int tickCount = blockTicks.Count;

            for (int xx = 0; xx < Server.ChunkSize; xx++)
            {
                int px = xx + chunkPos.X;
                for (int yy = 0; yy < Server.ChunkSize; yy++)
                {
                    int py = yy + chunkPos.Y;
                    for (int zz = 0; zz < Server.ChunkSize; zz++)
                    {
                        int pz = zz + chunkPos.Z;
                        for (int i = 0; i < tickCount; i++)
                        {
                            blockTicks[i](px, py, pz);
                        }
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
        int zDrawDistance = Server.InvertChunk(server.Map.MapSizeZ);
        unchecked
        {
            for (int x = -server.ChunkDrawDistance; x <= server.ChunkDrawDistance; x++)
            {
                for (int y = -server.ChunkDrawDistance; y <= server.ChunkDrawDistance; y++)
                {
                    for (int z = 0; z < zDrawDistance; z++)
                    {
                        Vector3i p = new(
                            playerPos.X + (x * Server.ChunkSize),
                            playerPos.Y + (y * Server.ChunkSize),
                            z * Server.ChunkSize);

                        if (VectorUtils.IsValidPos(server.Map, p.X, p.Y, p.Z))
                        {
                            yield return p;
                        }
                    }
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Monster spawning
    // -------------------------------------------------------------------------

    public void AddMonsters(Server server, Vector3i chunkPos)
    {
        ServerChunk chunk = server.Map.GetChunkValid(
            chunkPos.X / Server.ChunkSize,
            chunkPos.Y / Server.ChunkSize,
            chunkPos.Z / Server.ChunkSize);

        for (int tries = 0; chunk.Monsters.Count < MinMonstersPerChunk && tries < MonsterSpawnMaxTries; tries++)
        {
            int px = chunkPos.X + _rnd.Next(Server.ChunkSize);
            int py = chunkPos.Y + _rnd.Next(Server.ChunkSize);
            int pz = chunkPos.Z + _rnd.Next(Server.ChunkSize);

            if (!IsValidSpawnPosition(server, px, py, pz))
            {
                continue;
            }

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

    private static bool IsValidSpawnPosition(Server server, int px, int py, int pz)
        => VectorUtils.IsValidPos(server.Map, px, py, pz) &&
        VectorUtils.IsValidPos(server.Map, px, py, pz + 1) &&
        VectorUtils.IsValidPos(server.Map, px, py, pz - 1);

    private int ChooseMonsterType(Server server, int px, int py, int pz)
    {
        int surfaceHeight = VectorUtils.BlockHeight(server.Map, 0, px, py);
        return pz >= surfaceHeight
            ? MonsterTypesOnGround[_rnd.Next(MonsterTypesOnGround.Length)]
            : MonsterTypesUnderground[_rnd.Next(MonsterTypesUnderground.Length)];
    }

    private bool IsOpenGround(Server server, int px, int py, int pz)
        => server.Map.GetBlock(px, py, pz) == 0 &&
        server.Map.GetBlock(px, py, pz + 1) == 0 &&
        server.Map.GetBlock(px, py, pz - 1) != 0 &&
        !_blockRegistry.BlockTypes[server.Map.GetBlock(px, py, pz - 1)].IsFluid();
}