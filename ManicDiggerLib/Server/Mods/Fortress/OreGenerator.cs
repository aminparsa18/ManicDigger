using static ManicDigger.Mods.ModNetworkProcess;

namespace ManicDigger.Mods;

public class OreGenerator : IMod
{
    private IModManager m;
    private readonly Random _rnd = new();

    // Chunk bounds written in PopulateChunk and read in MakeCuboid.
    // MakeCuboid must never write outside these bounds — doing so loads
    // neighbouring chunks which re-enters PopulateChunk and causes a hang.
    private int _chunkX, _chunkY, _chunkZ, _chunkSize;

    private int TileIdStone;
    private int TileIdGravel;
    private int TileIdDirt;
    private int TileIdGrass;
    private int TileIdGoldOre;
    private int TileIdIronOre;
    private int TileIdSilverOre;
    private int TileIdSand;
    private int TileIdCoalOre;

    public void PreStart(IModManager m) => m.RequireMod("CoreBlocks");

    public void Start(IModManager manager)
    {
        m = manager;
        TileIdStone = m.GetBlockId("Stone");
        TileIdGravel = m.GetBlockId("Gravel");
        TileIdDirt = m.GetBlockId("Dirt");
        TileIdGrass = m.GetBlockId("Grass");
        TileIdGoldOre = m.GetBlockId("GoldOre");
        TileIdIronOre = m.GetBlockId("IronOre");
        TileIdSilverOre = m.GetBlockId("SilverOre");
        TileIdSand = m.GetBlockId("Sand");
        TileIdCoalOre = m.GetBlockId("CoalOre");
        m.RegisterPopulateChunk(PopulateChunk);
    }

    public bool EnableCaves = false;
    public int goldorelength = 50;
    public int ironorelength = 50;
    public int coalorelength = 50;
    public int gravellength = 50;
    public int silverlength = 50;
    public int dirtlength = 40;

    private void PopulateChunk(int x, int y, int z)
    {
        _chunkSize = m.GetChunkSize();
        _chunkX = x * _chunkSize;
        _chunkY = y * _chunkSize;
        _chunkZ = z * _chunkSize;
        MakeCaves(_chunkX, _chunkY, _chunkZ, _chunkSize,
            EnableCaves, gravellength, goldorelength,
            ironorelength, coalorelength, dirtlength, silverlength);
    }

    private void MakeCaves(int x, int y, int z, int chunksize,
        bool enableCaves, int gravelLength, int goldOreLength,
        int ironOreLength, int coalOreLength, int dirtOreLength, int silverOreLength)
    {
        // Find a stone start position within the chunk.
        double curx = 0, cury = 0, curz = 0;
        bool found = false;
        for (int i = 0; i < 2; i++)
        {
            curx = x + _rnd.Next(chunksize);
            cury = y + _rnd.Next(chunksize);
            curz = z + _rnd.Next(chunksize);
            if (m.GetBlock((int)curx, (int)cury, (int)curz) == TileIdStone)
            {
                found = true;
                break;
            }
        }

        if (!found) return;

        int blocktype = 0;
        int length = 200;
        if (_rnd.NextDouble() < 0.85)
        {
            int oretype = _rnd.Next(6);
            length = oretype switch
            {
                0 => gravelLength,
                1 => goldOreLength,
                2 => ironOreLength,
                3 => coalOreLength,
                4 => dirtOreLength,
                5 => silverOreLength,
                _ => length,
            };
            length = _rnd.Next(Math.Max(1, length));
            blocktype = oretype < 4 ? TileIdGravel + oretype
                      : oretype > 4 ? TileIdGravel + oretype + 115
                      : TileIdDirt;
        }
        if (blocktype == 0 && !enableCaves) return;

        int dirx = _rnd.NextDouble() < 0.5 ? -1 : 1;
        int dirz = _rnd.NextDouble() < 0.5 ? -1 : 1;
        double speedx = _rnd.NextDouble() * dirx;
        double speedy = _rnd.NextDouble();
        double speedz = _rnd.NextDouble() * 0.5 * dirz;

        for (int i = 0; i < length; i++)
        {
            if (_rnd.NextDouble() < 0.06) speedx = _rnd.NextDouble() * dirx;
            if (_rnd.NextDouble() < 0.06) speedy = _rnd.NextDouble() * dirx;
            if (_rnd.NextDouble() < 0.02) speedz = _rnd.NextDouble() * 0.5 * dirz;

            curx += speedx;
            cury += speedy;
            curz += speedz;

            if (!m.IsValidPos((int)curx, (int)cury, (int)curz)) continue;

            for (int ii = 0; ii < 3; ii++)
            {
                int sizex = _rnd.Next(3, 6);
                int sizey = _rnd.Next(3, 6);
                int sizez = _rnd.Next(2, 3);
                int dx = _rnd.Next(-sizex / 2, sizex / 2);
                int dy = _rnd.Next(-sizey / 2, sizey / 2);
                int dz = _rnd.Next(-sizez, sizez);

                double density = blocktype == 0 ? 1.0 : _rnd.NextDouble() * 0.90;

                int[] allowin = blocktype == 0
                    ? [TileIdStone, TileIdDirt, TileIdGrass,
                       TileIdGoldOre, TileIdIronOre, TileIdCoalOre, TileIdSilverOre]
                    : blocktype == TileIdGravel
                        ? [TileIdDirt, TileIdStone, TileIdSand,
                           TileIdGoldOre, TileIdIronOre, TileIdCoalOre, TileIdSilverOre]
                        : [TileIdStone];

                if (blocktype == TileIdGravel) density = 1.0;

                MakeCuboid(
                    (int)curx - sizex / 2 + dx,
                    (int)cury - sizey / 2 + dy,
                    (int)curz - sizez / 2 + dz,
                    sizex, sizey, sizez, blocktype, allowin, density);
            }
        }
    }

    private void MakeCuboid(int x, int y, int z,
        int sizex, int sizey, int sizez,
        int blocktype, int[] allowin, double chance)
    {
        for (int xx = 0; xx < sizex; xx++)
            for (int yy = 0; yy < sizey; yy++)
                for (int zz = 0; zz < sizez; zz++)
                {
                    int px = x + xx;
                    int py = y + yy;
                    int pz = z + zz;

                    // Stay within the current chunk — accessing blocks outside loads
                    // neighbouring chunks which re-enters PopulateChunk and causes a hang.
                    if (px < _chunkX || px >= _chunkX + _chunkSize) continue;
                    if (py < _chunkY || py >= _chunkY + _chunkSize) continue;
                    if (pz < _chunkZ || pz >= _chunkZ + _chunkSize) continue;

                    if (!m.IsValidPos(px, py, pz)) continue;
                    if (pz == 0) continue; // preserve bedrock layer

                    int existing = m.GetBlock(px, py, pz);

                    if (allowin != null)
                    {
                        bool allowed = false;
                        foreach (int id in allowin)
                            if (id == existing) { allowed = true; break; }
                        if (!allowed) continue;
                    }

                    if (_rnd.NextDouble() < chance)
                        m.SetBlock(px, py, pz, blocktype);
                }
    }
}

