using OpenTK.Mathematics;
using System.Numerics;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using Vector3 = OpenTK.Mathematics.Vector3;

public partial class Game
{
    //internal int SelectedBlockPositionX;
    internal int SelectedBlockPositionY;
    internal int SelectedBlockPositionZ;
    internal int SelectedEntityId;

    public static bool IsEmptyForPhysics(Packet_BlockType block)
    {
        return (block.DrawType == Packet_DrawTypeEnum.Ladder)
            || (block.WalkableType != Packet_WalkableTypeEnum.Solid && block.WalkableType != Packet_WalkableTypeEnum.Fluid);
    }

    public static bool IsTransparentForLight(Packet_BlockType b)
    {
        return b.DrawType != Packet_DrawTypeEnum.Solid && b.DrawType != Packet_DrawTypeEnum.ClosedDoor;
    }

    public void MapLoadingStart()
    {
        guistate = GuiState.MapLoading;
        SetFreeMouse(true);
        maploadingprogress = new MapLoadingProgressEventArgs();
        fontMapLoading = FontCi.Create("Arial", 14, 0);
    }

    internal int Xcenter(float width)
    {
        return platform.GetCanvasWidth() / 2 - (int)width / 2;
    }

    internal int Ycenter(float height)
    {
        return platform.GetCanvasHeight() / 2 - (int)height / 2;
    }

    public int Width()
    {
        return platform.GetCanvasWidth();
    }

    public int Height()
    {
        return platform.GetCanvasHeight();
    }

    public void Set3dProjection(float zfar, float fov)
    {
        float aspect_ratio = 1f * Width() / Height();
        Matrix4.CreatePerspectiveFieldOfView(fov, aspect_ratio, znear, zfar, out Matrix4 projection);
        CameraMatrix.LastProjectionMatrix = projection;
        GLMatrixModeProjection();
        GLLoadMatrix(projection);
        SetMatrixUniformProjection();
    }

    internal float Zfar()
    {
        if (d_Config3d.viewdistance >= 256)
        {
            return d_Config3d.viewdistance * 2;
        }
        return ENABLE_ZFAR ? d_Config3d.viewdistance : 99999;
    }

    internal bool IsWater(int blockType)
    {
        string name = blocktypes[blockType].Name;
        if (name == null)
        {
            return false;
        }
        return name.Contains("Water"); // todo
    }

    internal float CurrentFov()
    {
        if (IronSights)
        {
            Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
            if (item != null && item.ItemClass == Packet_ItemClassEnum.Block)
            {
                if (DecodeFixedPoint(blocktypes[item.BlockId].IronSightsFovFloat) != 0)
                {
                    return this.fov * DecodeFixedPoint(blocktypes[item.BlockId].IronSightsFovFloat);
                }
            }
        }
        return this.fov;
    }

    internal float DecodeFixedPoint(int value)
    {
        return (one * value) / 32;
    }

    public static int EncodeFixedPoint(float p)
    {
        return (int)(p * 32);
    }

    internal int BlockUnderPlayer()
    {
        if (!map.IsValidPos((int)player.position.x,
            (int)player.position.z,
            (int)player.position.y - 1))
        {
            return -1;
        }
        int blockunderplayer = map.GetBlock((int)player.position.x,
            (int)(player.position.z),
            (int)(player.position.y) - 1);
        return blockunderplayer;
    }

    internal int? BlockInHand()
    {
        Packet_Item item = d_Inventory.RightHand[ActiveMaterial];

        if (item != null && item.ItemClass == Packet_ItemClassEnum.Block)
        {
            return item.BlockId;
        }

        return null;
    }

    internal float CurrentRecoil()
    {
        Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
        if (item == null || item.ItemClass != Packet_ItemClassEnum.Block)
        {
            return 0;
        }
        return DecodeFixedPoint(blocktypes[item.BlockId].RecoilFloat);
    }

    internal float CurrentAimRadius()
    {
        Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
        if (item == null || item.ItemClass != Packet_ItemClassEnum.Block)
        {
            return 0;
        }
        float radius = (DecodeFixedPoint(blocktypes[item.BlockId].AimRadiusFloat) / 800) * Width();
        if (IronSights)
        {
            radius = (DecodeFixedPoint(blocktypes[item.BlockId].IronSightsAimRadiusFloat) / 800) * Width();
        }
        return radius + RadiusWhenMoving * radius * (Math.Min(playervelocity.Length / movespeed, 1));
    }

    public int GetLight(int x, int y, int z)
    {
        int light = map.MaybeGetLight(x, y, z);

        if (light == -1)
        {
            if ((x >= 0 && x < map.MapSizeX)
                && (y >= 0 && y < map.MapSizeY)
                && (z >= d_Heightmap.GetBlock(x, y)))
            {
                return sunlight_;
            }
            else
            {
                return minlight;
            }
        }
        else
        {
            return light;
        }
    }

    public void Draw2dBitmapFile(string filename, float x, float y, float w, float h)
    {
        Draw2dTexture(GetTexture(filename), x, y, w, h, null, 0, ColorFromArgb(255, 255, 255, 255), false);
    }

    public void ToggleFog()
    {
        int[] drawDistances = new int[10];
        int drawDistancesCount = 0;
        drawDistances[drawDistancesCount++] = 32;
        if (maxdrawdistance >= 64) { drawDistances[drawDistancesCount++] = 64; }
        if (maxdrawdistance >= 128) { drawDistances[drawDistancesCount++] = 128; }
        if (maxdrawdistance >= 256) { drawDistances[drawDistancesCount++] = 256; }
        if (maxdrawdistance >= 512) { drawDistances[drawDistancesCount++] = 512; }
        for (int i = 0; i < drawDistancesCount; i++)
        {
            if (d_Config3d.viewdistance == drawDistances[i])
            {
                d_Config3d.viewdistance = drawDistances[(i + 1) % drawDistancesCount];
                RedrawAllBlocks();
                return;
            }
        }
        d_Config3d.viewdistance = drawDistances[0];
        RedrawAllBlocks();
    }

    internal float GetCharacterEyesHeight()
    {
        return entities[LocalPlayerId].drawModel.eyeHeight;
    }

    internal void SetCharacterEyesHeight(float value)
    {
        entities[LocalPlayerId].drawModel.eyeHeight = value;
    }

    public float EyesPosX() { return player.position.x; }
    public float EyesPosY() { return player.position.y + GetCharacterEyesHeight(); }
    public float EyesPosZ() { return player.position.z; }

    internal string ValidFont(string family)
    {
        for (int i = 0; i < AllowedFontsCount; i++)
        {
            if (AllowedFonts[i] == family)
            {
                return family;
            }
        }
        return AllowedFonts[0];
    }

    public int MaterialSlots_(int i)
    {
        Packet_Item item = d_Inventory.RightHand[i];
        int m = d_Data.BlockIdDirt();
        if (item != null && item.ItemClass == Packet_ItemClassEnum.Block)
        {
            m = d_Inventory.RightHand[i].BlockId;
        }
        return m;
    }

    internal bool IsTileEmptyForPhysics(int x, int y, int z)
    {
        if (z >= map.MapSizeZ)
        {
            return true;
        }
        if (x < 0 || y < 0 || z < 0)// || z >= mapsizez)
        {
            return controls.freemove;
        }
        if (x >= map.MapSizeX || y >= map.MapSizeY)// || z >= mapsizez)
        {
            return controls.freemove;
        }
        int block = map.GetBlockValid(x, y, z);
        return block == SpecialBlockId.Empty
            || block == d_Data.BlockIdFillArea()
            || IsWater(block);
    }

    internal bool IsTileEmptyForPhysicsClose(int x, int y, int z)
    {
        return IsTileEmptyForPhysics(x, y, z)
            || (map.IsValidPos(x, y, z) && blocktypes[map.GetBlock(x, y, z)].DrawType == Packet_DrawTypeEnum.HalfHeight)
            || (map.IsValidPos(x, y, z) && IsEmptyForPhysics(blocktypes[map.GetBlock(x, y, z)]));
    }

    internal bool IsUsableBlock(int blocktype)
    {
        return d_Data.IsRailTile(blocktype) || blocktypes[blocktype].IsUsable;
    }

    internal bool IsWearingWeapon()
    {
        return d_Inventory.RightHand[ActiveMaterial] != null;
    }

    internal void ApplyDamageToPlayer(int damage, int damageSource, int sourceId)
    {
        PlayerStats.CurrentHealth -= damage;
        if (PlayerStats.CurrentHealth <= 0)
        {
            PlayerStats.CurrentHealth = 0;
            PlayAudio("death.wav");
            SendPacketClient(ClientPackets.Death(damageSource, sourceId));

            //Respawn(); //Death is not respawn ;)
        }
        else
        {
            PlayAudio(rnd.Next() % 2 == 0 ? "grunt1.wav" : "grunt2.wav");
        }
        SendPacketClient(ClientPackets.Health(PlayerStats.CurrentHealth));
    }

    public int GetPlayerEyesBlockX()
    {
        return (int)(MathF.Floor(player.position.x));
    }

    public int GetPlayerEyesBlockY()
    {
        return (int)(MathF.Floor(player.position.z));
    }

    public int GetPlayerEyesBlockZ()
    {
        return (int)(MathF.Floor(player.position.y + entities[LocalPlayerId].drawModel.eyeHeight));
    }

    internal void UpdateColumnHeight(int x, int y)
    {
        //todo faster
        int height = map.MapSizeZ - 1;
        for (int i = map.MapSizeZ - 1; i >= 0; i--)
        {
            height = i;
            if (!IsTransparentForLight(blocktypes[map.GetBlock(x, y, i)]))
            {
                break;
            }
        }
        d_Heightmap.SetBlock(x, y, height);
    }

    internal void ShadowsOnSetBlock(int x, int y, int z)
    {
        int oldheight = d_Heightmap.GetBlock(x, y);
        UpdateColumnHeight(x, y);
        //update shadows in all chunks below
        int newheight = d_Heightmap.GetBlock(x, y);
        int min = Math.Min(oldheight, newheight);
        int max = Math.Max(oldheight, newheight);
        for (int i = min; i < max; i++)
        {
            if (i / chunksize != z / chunksize)
            {
                map.SetChunkDirty(x / chunksize, y / chunksize, i / chunksize, true, true);
            }
        }
        //Todo: too many redraws. Optimize.
        //Now placing a single block updates 27 chunks,
        //and each of those chunk updates calculates light from 27 chunks.
        //So placing a block is often 729x slower than it should be.
        for (int xx = 0; xx < 3; xx++)
        {
            for (int yy = 0; yy < 3; yy++)
            {
                for (int zz = 0; zz < 3; zz++)
                {
                    int cx = x / chunksize + xx - 1;
                    int cy = y / chunksize + yy - 1;
                    int cz = z / chunksize + zz - 1;
                    if (map.IsValidChunkPos(cx, cy, cz))
                    {
                        map.SetChunkDirty(cx, cy, cz, true, false);
                    }
                }
            }
        }
    }

    internal void SetBlock(int x, int y, int z, int tileType)
    {
        map.SetBlockRaw(x, y, z, tileType);
        map.SetChunkDirty(x / chunksize, y / chunksize, z / chunksize, true, true);
        //d_Shadows.OnSetBlock(x, y, z);
        ShadowsOnSetBlock(x, y, z);
        lastplacedblockX = x;
        lastplacedblockY = y;
        lastplacedblockZ = z;
    }

    internal int DialogsCount => dialogs.Count(d => d != null);

    internal int GetDialogId(string name)
    {
        for (int i = 0; i < dialogs.Length; i++)
        {
            if (dialogs[i] == null)
            {
                continue;
            }
            if (dialogs[i].key == name)
            {
                return i;
            }
        }
        return -1;
    }

    internal float GetCurrentBlockHealth(int x, int y, int z)
    {
        if (blockHealth.TryGetValue((x, y, z), out float health))
        {
            return health;
        }
        int blocktype = map.GetBlock(x, y, z);
        return d_Data.Strength()[blocktype];
    }

    internal static bool EnablePlayerUpdatePosition(int kKey)
    {
        return true;
    }

    internal static bool EnablePlayerUpdatePositionContainsKey(int kKey)
    {
        return false;
    }

    public float WeaponAttackStrength()
    {
        return rnd.Next(2, 4);
    }

    public static byte HeadingByte(float orientationX, float orientationY, float orientationZ) =>
     (byte)(int)((orientationY % (2 * MathF.PI)) / (2 * MathF.PI) * 256);

    public static byte PitchByte(float orientationX, float orientationY, float orientationZ)
    {
        float xx = (orientationX + MathF.PI) % (2 * MathF.PI);
        return (byte)(int)(xx / (2 * MathF.PI) * 256);
    }

    internal void InvokeMapLoadingProgress(int progressPercent, int progressBytes, string status)
    {
        maploadingprogress = new MapLoadingProgressEventArgs
        {
            ProgressPercent = progressPercent,
            ProgressBytes = progressBytes,
            ProgressStatus = status
        };
    }

    internal void Log(string p)
    {
        AddChatline(p);
    }

    internal void SetTileAndUpdate(int x, int y, int z, int type)
    {
        SetBlock(x, y, z, type);
        RedrawBlock(x, y, z);
    }

    internal void RedrawBlock(int x, int y, int z)
    {
        map.SetBlockDirty(x, y, z);
    }

    internal bool IsFillBlock(int blocktype)
    {
        return blocktype == d_Data.BlockIdFillArea()
            || blocktype == d_Data.BlockIdFillStart()
            || blocktype == d_Data.BlockIdCuboid();
    }

    internal bool IsAnyPlayerInPos(int blockposX, int blockposY, int blockposZ)
    {
        for (int i = 0; i < entitiesCount; i++)
        {
            Entity e = entities[i];
            if (e == null)
            {
                continue;
            }
            if (e.drawModel == null)
            {
                continue;
            }
            if (e.networkPosition == null || (e.networkPosition != null && e.networkPosition.PositionLoaded))
            {
                if (IsPlayerInPos(e.position.x, e.position.y, e.position.z,
                    blockposX, blockposY, blockposZ, e.drawModel.ModelHeight))
                {
                    return true;
                }
            }
        }
        return IsPlayerInPos(player.position.x, player.position.y, player.position.z,
            blockposX, blockposY, blockposZ, player.drawModel.ModelHeight);
    }

    private bool IsPlayerInPos(float playerposX, float playerposY, float playerposZ,
                       int blockposX, int blockposY, int blockposZ, float playerHeight)
    {
        for (int i = 0; i < Math.Floor(playerHeight) + 1; i++)
        {
            if (ScriptCharacterPhysics.BoxPointDistance(blockposX, blockposZ, blockposY,
                blockposX + 1, blockposZ + 1, blockposY + 1,
                playerposX, playerposY + i + constWallDistance, playerposZ) < constWallDistance)
            {
                return true;
            }
        }
        return false;
    }
   
    internal int? FollowId()
    {
        if (Follow == null)
        {
            return null;
        }
        for (int i = 0; i < entitiesCount; i++)
        {
            if (entities[i] == null)
            {
                continue;
            }
            if (entities[i].drawName == null)
            {
                continue;
            }
            DrawName p = entities[i].drawName;
            if (p.Name == Follow)
            {
                return i;
            }
        }
        return null;
    }

    internal bool IsValid(int blocktype)
    {
        return blocktypes[blocktype].Name != null;
    }

    internal int TextSizeWidth(string s, int size)
    {
        platform.TextSize(s, size, out int width, out _);
        return width;
    }

    internal int TextSizeHeight(string s, int size)
    {
        platform.TextSize(s, size, out _, out int height);
        return height;
    }

    internal void EntityAddLocal(Entity entity)
    {
        for (int i = entityLocalIdStart; i < entitiesCount; i++)
        {
            if (entities[i] == null)
            {
                entities[i] = entity;
                return;
            }
        }
        entities[entitiesCount++] = entity;
    }

    internal static Entity CreateBulletEntity(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, float speed)
    {
        Entity entity = new();

        Bullet_ bullet = new()
        {
            fromX = fromX,
            fromY = fromY,
            fromZ = fromZ,
            toX = toX,
            toY = toY,
            toZ = toZ,
            speed = speed
        };
        entity.bullet = bullet;

        entity.sprite = new Sprite
        {
            image = "Sponge.png",
            size = 4,
            animationcount = 0
        };

        return entity;
    }

    internal bool SwimmingEyes()
    {
        int eyesBlock = GetPlayerEyesBlock();
        if (eyesBlock == -1) { return true; }
        return d_Data.WalkableType1()[eyesBlock] == Packet_WalkableTypeEnum.Fluid;
    }

    internal bool SwimmingBody()
    {
        int block = map.GetBlock((int)(player.position.x), (int)(player.position.z), (int)(player.position.y + 1));
        if (block == -1) { return true; }
        return d_Data.WalkableType1()[block] == Packet_WalkableTypeEnum.Fluid;
    }

    internal bool WaterSwimmingEyes()
    {
        if (GetPlayerEyesBlock() == -1) { return true; }
        return IsWater(GetPlayerEyesBlock());
    }

    internal int GetPlayerEyesBlock()
    {
        float pX = player.position.x;
        float pY = player.position.y;
        float pZ = player.position.z;
        pY += entities[LocalPlayerId].drawModel.eyeHeight;
        int bx = (int)MathF.Floor(pX);
        int by = (int)MathF.Floor(pZ);
        int bz = (int)MathF.Floor(pY);

        if (!map.IsValidPos(bx, by, bz))
        {
            if (pY < WaterLevel())
            {
                return -1;
            }
            return 0;
        }
        return map.GetBlockValid(bx, by, bz);
    }

    public float WaterLevel() { return map.MapSizeZ / 2; }

    internal bool IsLava(int blockType)
    {
        string name = blocktypes[blockType].Name;
        if (name == null)
        {
            return false;
        }
        return name.Contains("Lava"); // todo
    }

    internal int Terraincolor()
    {
        if (WaterSwimmingCamera())
        {
            return ColorFromArgb(255, 78, 95, 140);
        }
        else if (LavaSwimmingCamera())
        {
            return ColorFromArgb(255, 222, 101, 46);
        }
        else
        {
            return ColorFromArgb(255, 255, 255, 255);
        }
    }

    internal void SetAmbientLight(int color)
    {
        int r = ColorR(color);
        int g = ColorG(color);
        int b = ColorB(color);
        platform.GlLightModelAmbient(r, g, b);
    }

    internal float MoveSpeedNow()
    {
        float movespeednow = movespeed;
        {
            //walk faster on cobblestone
            int blockunderplayer = BlockUnderPlayer();
            if (blockunderplayer != -1)
            {
                float floorSpeed = d_Data.WalkSpeed()[blockunderplayer];
                if (floorSpeed != 0)
                {
                    movespeednow *= floorSpeed;
                }
            }
        }
        if (keyboardState[GetKey(Keys.LeftShift)])
        {
            //enable_acceleration = false;
            movespeednow *= one * 2 / 10;
        }
        Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
        if (item != null && item.ItemClass == Packet_ItemClassEnum.Block)
        {
            float itemSpeed = DecodeFixedPoint(blocktypes[item.BlockId].WalkSpeedWhenUsedFloat);
            if (itemSpeed != 0)
            {
                movespeednow *= itemSpeed;
            }
            if (IronSights)
            {
                float ironSightsSpeed = DecodeFixedPoint(blocktypes[item.BlockId].IronSightsMoveSpeedFloat);
                if (ironSightsSpeed != 0)
                {
                    movespeednow *= ironSightsSpeed;
                }
            }
        }
        return movespeednow;
    }

    internal void UseVsync()
    {
        platform.SetVSync((ENABLE_LAG == 1) ? false : true);
    }

    internal void ToggleVsync()
    {
        ENABLE_LAG++;
        ENABLE_LAG = ENABLE_LAG % 3;
        UseVsync();
    }

    internal void GuiStateBackToGame()
    {
        guistate = GuiState.Normal;
        SetFreeMouse(false);
    }
    

    internal void RedrawAllBlocks()
    {
        shouldRedrawAllBlocks = true;
    }

    public const int clearcolorR = 0;
    public const int clearcolorG = 0;
    public const int clearcolorB = 0;
    public const int clearcolorA = 255;

    internal void SetFog()
    {
        if (d_Config3d.viewdistance >= 512)
        {
            return;
        }
        //Density for linear fog
        //float density = 0.3f;
        // use this density for exp2 fog (0.0045f was a bit too much at close ranges)
        float density = one * 25 / 10000; // 0.0025f;

        int fogR;
        int fogG;
        int fogB;
        int fogA;

        if (SkySphereNight && (!shadowssimple))
        {
            fogR = 0;
            fogG = 0;
            fogB = 0;
            fogA = 255;
        }
        else
        {
            fogR = clearcolorR;
            fogG = clearcolorG;
            fogB = clearcolorB;
            fogA = clearcolorA;
        }
        platform.GlEnableFog();
        platform.GlHintFogHintNicest();
        //old linear fog
        //GL.Fog(FogParameter.FogMode, (int)FogMode.Linear);
        // looks better
        platform.GlFogFogModeExp2();
        platform.GlFogFogColor(fogR, fogG, fogB, fogA);
        platform.GlFogFogDensity(density);
        //Unfortunately not used for exp/exp2 fog
        //float fogsize = 10;
        //if (d_Config3d.viewdistance <= 64)
        //{
        //    fogsize = 5;
        //}
        // //float fogstart = d_Config3d.viewdistance - fogsize + 200;
        //float fogstart = d_Config3d.viewdistance - fogsize;
        //GL.Fog(FogParameter.FogStart, fogstart);
        //GL.Fog(FogParameter.FogEnd, fogstart + fogsize);
    }

    internal BlockPosSide Nearest(ArraySegment<BlockPosSide> pick2, int pick2Count, Vector3 target)
    {
        float minDist = 1000 * 1000;
        BlockPosSide nearest = null;
        for (int i = 0; i < pick2Count; i++)
        {
            float dist = Vector3.Distance(pick2[i].blockPos, target);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = pick2[i];
            }
        }
        return nearest;
    }

    internal BlockOctreeSearcher s;

    internal void ChatLog(string p)
    {
        if (!platform.ChatLog(this.ServerInfo.ServerName, p))
        {
            platform.ConsoleWriteLine(string.Format(language.CannotWriteChatLog(), this.ServerInfo.ServerName));
        }
    }

    internal void MapLoaded()
    {
        RedrawAllBlocks();
        materialSlots = d_Data.DefaultMaterialSlots();
        GuiStateBackToGame();

        playerPositionSpawnX = player.position.x;
        playerPositionSpawnY = player.position.y;
        playerPositionSpawnZ = player.position.z;
    }

    internal void UseInventory(Packet_Inventory packet_Inventory)
    {
        d_Inventory = packet_Inventory;
        d_InventoryUtil.UpdateInventory(packet_Inventory);
    }

    internal void SendSetBlockAndUpdateSpeculative(int material, int x, int y, int z, int mode)
    {
        SendSetBlock(x, y, z, mode, material, ActiveMaterial);

        Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
        if (item != null && item.ItemClass == Packet_ItemClassEnum.Block)
        {
            //int blockid = d_Inventory.RightHand[d_Viewport.ActiveMaterial].BlockId;
            int blockid = material;
            if (mode == Packet_BlockSetModeEnum.Destroy)
            {
                blockid = SpecialBlockId.Empty;
            }
            Speculative s_ = new()
            {
                x = x,
                y = y,
                z = z,
                blocktype = map.GetBlock(x, y, z),
                timeMilliseconds = platform.TimeMillisecondsFromStart()
            };
            AddSpeculative(s_);
            SetBlock(x, y, z, blockid);
            RedrawBlock(x, y, z);
        }
        else
        {
            //TODO
        }
    }

    private void AddSpeculative(Speculative s_)
    {
        for (int i = 0; i < speculativeCount; i++)
        {
            if (speculative[i] == null)
            {
                speculative[i] = s_;
                return;
            }
        }
        speculative[speculativeCount++] = s_;
    }

    internal void RevertSpeculative(float dt)
    {
        for (int i = 0; i < speculativeCount; i++)
        {
            Speculative s_ = speculative[i];
            if (s_ == null)
            {
                continue;
            }
            if ((one * (platform.TimeMillisecondsFromStart() - s_.timeMilliseconds) / 1000) > 2)
            {
                RedrawBlock(s_.x, s_.y, s_.z);
                speculative[i] = null;
            }
        }
    }

    internal void Set3dProjection1(float zfar_)
    {
        Set3dProjection(zfar_, CurrentFov());
    }

    internal void Set3dProjection2()
    {
        Set3dProjection1(Zfar());
    }

    private bool sendResize;

    internal Packet_ServerRedirect redirectTo;
    internal void ExitAndSwitchServer(Packet_ServerRedirect newServer)
    {
        if (issingleplayer)
        {
            platform.SinglePlayerServerExit();
        }
        redirectTo = newServer;
        exitToMainMenu = true;
    }

    internal Packet_ServerRedirect GetRedirect()
    {
        return redirectTo;
    }

    internal void ExitToMainMenu_()
    {
        if (issingleplayer)
        {
            platform.SinglePlayerServerExit();
        }
        redirectTo = null;
        exitToMainMenu = true;
    }

    internal void ProcessServerIdentification(Packet_Server packet)
    {
        this.LocalPlayerId = packet.Identification.AssignedClientId;
        this.ServerInfo.connectdata = this.connectdata;
        this.ServerInfo.ServerName = packet.Identification.ServerName;
        this.ServerInfo.ServerMotd = packet.Identification.ServerMotd;
        this.d_TerrainChunkTesselator.ENABLE_TEXTURE_TILING = packet.Identification.RenderHint_ == RenderHintEnum.Fast;
        Packet_StringList requiredMd5 = packet.Identification.RequiredBlobMd5;
        Packet_StringList requiredName = packet.Identification.RequiredBlobName;
        ChatLog("[GAME] Processed server identification");
        int getCount = 0;
        if (requiredMd5 != null)
        {
            ChatLog(string.Format("[GAME] Server has {0} assets", requiredMd5.ItemsCount.ToString()));
            for (int i = 0; i < requiredMd5.ItemsCount; i++)
            {
                string md5 = requiredMd5.Items[i];

                //check if file with that content is already in cache
                if (platform.IsCached(md5))
                {
                    //File has been cached. load cached version.
                    Asset cachedAsset = platform.LoadAssetFromCache(md5);
                    string name;
                    if (requiredName != null)
                    {
                        name = requiredName.Items[i];
                    }
                    else // server older than 2014-07-13.
                    {
                        name = cachedAsset.name;
                    }
                    SetFile(name, cachedAsset.md5, cachedAsset.data, cachedAsset.dataLength);
                }
                else
                {
                    //Asset not present in cache
                    if (requiredName != null)
                    {
                        //If list of names is given (server > 2014-07-13) lookup if asset is already loaded
                        if (!HasAsset(md5, requiredName.Items[i]))
                        {
                            //Request asset from server if not already loaded
                            getAsset[getCount++] = md5;
                        }
                    }
                    else
                    {
                        //Server didn't send list of required asset names
                        getAsset[getCount++] = md5;
                    }
                }
            }
            ChatLog(string.Format("[GAME] Will download {0} missing assets", getCount.ToString()));
        }
        SendGameResolution();
        ChatLog("[GAME] Sent window resolution to server");
        sendResize = true;
        SendRequestBlob(getAsset, getCount);
        ChatLog("[GAME] Sent BLOB request");
        if (packet.Identification.MapSizeX != map.MapSizeX
            || packet.Identification.MapSizeY != map.MapSizeY
            || packet.Identification.MapSizeZ != map.MapSizeZ)
        {
            map.Reset(packet.Identification.MapSizeX,
                packet.Identification.MapSizeY,
                packet.Identification.MapSizeZ);
            d_Heightmap.Restart();
        }
        shadowssimple = packet.Identification.DisableShadows == 1 ? true : false;
        //maxdrawdistance = packet.Identification.PlayerAreaSize / 2;
        //if (maxdrawdistance == 0)
        //{
        //    maxdrawdistance = 128;
        //}
        maxdrawdistance = 256;
        ChatLog("[GAME] Map initialized");
    }

    internal void InvalidVersionAllow()
    {
        if (invalidVersionDrawMessage != null)
        {
            invalidVersionDrawMessage = null;
            ProcessServerIdentification(invalidVersionPacketIdentification);
            invalidVersionPacketIdentification = null;
        }
    }
   
    public void EscapeMenuStart()
    {
        guistate = GuiState.EscapeMenu;
        menustate = new MenuState();
        platform.ExitMousePointerLock();
        escapeMenuRestart = true;
    }

    public void ShowEscapeMenu()
    {
        guistate = GuiState.EscapeMenu;
        menustate = new MenuState();
        SetFreeMouse(true);
    }

    public void ShowInventory()
    {
        guistate = GuiState.Inventory;
        menustate = new MenuState();
        SetFreeMouse(true);
    }

    public void Update(float dt)
    {
        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].OnNewFrameReadOnlyMainThread(this, dt);
        }
    }

    public ArraySegment<BlockPosSide> Pick(BlockOctreeSearcher s_, Line3D line, out int retCount)
    {
        int minX = (int)(Math.Min(line.Start[0], line.End[0]));
        int minY = (int)(Math.Min(line.Start[1], line.End[1]));
        int minZ = (int)(Math.Min(line.Start[2], line.End[2]));

        if (minX < 0) { minX = 0; }
        if (minY < 0) { minY = 0; }
        if (minZ < 0) { minZ = 0; }

        int maxX = (int)(Math.Max(line.Start[0], line.End[0]));
        int maxY = (int)(Math.Max(line.Start[1], line.End[1]));
        int maxZ = (int)(Math.Max(line.Start[2], line.End[2]));

        if (maxX > map.MapSizeX) { maxX = map.MapSizeX; }
        if (maxY > map.MapSizeZ) { maxY = map.MapSizeZ; }
        if (maxZ > map.MapSizeY) { maxZ = map.MapSizeY; }

        int sizex = maxX - minX + 1;
        int sizey = maxY - minY + 1;
        int sizez = maxZ - minZ + 1;

        int size = (int)BitOperations.RoundUpToPowerOf2(
            (uint)Math.Max(sizex, Math.Max(sizey, sizez))
        );

        s_.StartBox = new Box3(new Vector3(minX, minY, minZ), new Vector3(minX + size, minY + size, minZ + size));

        var pick2 = s_.LineIntersection(
            IsTileEmptyForPhysics,
            Getblockheight,
            line,
            out retCount
        );

        PickSort(pick2, retCount, line.Start);

        return pick2;
    }

    private void PickSort(ArraySegment<BlockPosSide> pick, int pickCount, Vector3 start)
    {
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < pickCount - 1; i++)
            {
                float dist = Vector3.Distance(pick[i].blockPos, start);
                float distNext = Vector3.Distance(pick[i + 1].blockPos, start);
                if (dist > distNext)
                {
                    changed = true;

                    BlockPosSide swapTemp = pick[i];
                    pick[i] = pick[i + 1];
                    pick[i + 1] = swapTemp;
                }
            }
        }
        while (changed);
    }

    public GamePlatform GetPlatform()
    {
        return platform;
    }

    public void SetPlatform(GamePlatform value)
    {
        platform = value;
    }

    public float Scale()
    {
        //Only scale things on mobile devices
        if (platform.IsSmallScreen())
        {
            float scale = one * Width() / 1280;
            return scale;
        }
        else
        {
            return one;
        }
    }
  
    public void QueueActionCommit(Action action)
    {
        commitActions.Add(action);
    }

    public void DrawModel(Model model)
    {
        SetMatrixUniformModelView();
        platform.DrawModel(model);
    }

    public void DrawModels(List<Model> model, int count)
    {
        SetMatrixUniformModelView();
        platform.DrawModels(model, count);
    }

    public void DrawModelData(ModelData data)
    {
        SetMatrixUniformModelView();
        platform.DrawModelData(data);
    }

    public void Dispose()
    {
        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].Dispose(this);
        }
        foreach (int id in textures.Values)
        {
            platform.GLDeleteTexture(id);
        }
        for (int i = 0; i < cachedTextTexturesMax; i++)
        {
            if (cachedTextTextures[i] == null)
            {
                continue;
            }
            if (cachedTextTextures[i].texture == null)
            {
                continue;
            }
            platform.GLDeleteTexture(cachedTextTextures[i].texture.textureId);
        }
    }
}