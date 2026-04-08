using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using System.Numerics;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using Vector3 = OpenTK.Mathematics.Vector3;

public partial class Game
{
    // Main game loop
    public void OnRenderFrame(float deltaTime)
    {
        taskScheduler.Update(this, deltaTime);
    }
    
    internal void MainThreadOnRenderFrame(float deltaTime)
    {
        UpdateResize();

        if (guistate == GuiState.MapLoading)
        {
            platform.GlClearColorRgbaf(0, 0, 0, 1);
        }
        else
        {
            platform.GlClearColorRgbaf(one * clearcolorR / 255, one * clearcolorG / 255, one * clearcolorB / 255, one * clearcolorA / 255);
        }

        mouseSmoothingAccum += deltaTime;
        float constMouseDt = 1f / 300;
        while (mouseSmoothingAccum > constMouseDt)
        {
            mouseSmoothingAccum -= constMouseDt;
            UpdateMouseViewportControl(constMouseDt);
        }

        //Sleep is required in Mono for running the terrain background thread.
        platform.ApplicationDoEvents();

        accumulator += deltaTime;
        if (accumulator > 1)
        {
            accumulator = 1;
        }
        float dt = one / 75;

        while (accumulator >= dt)
        {
            FrameTick(dt);
            accumulator -= dt;
        }

        if (guistate == GuiState.MapLoading)
        {
            GotoDraw2d(deltaTime);
            return;
        }

        if (ENABLE_LAG == 2)
        {
            platform.ThreadSpinWait(20 * 1000 * 1000);
        }

        SetAmbientLight(Terraincolor());
        platform.GlClearColorBufferAndDepthBuffer();
        platform.BindTexture2d(d_TerrainTextures.TerrainTexture);

        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].OnBeforeNewFrameDraw3d(this, deltaTime);
        }
        GLMatrixModeModelView();
        GLLoadMatrix(camera);
        CameraMatrix.LastModelViewMatrix = camera;

        d_FrustumCulling.CalcFrustumEquations();

        bool drawgame = guistate != GuiState.MapLoading;
        if (drawgame)
        {
            platform.GlEnableDepthTest();
            for (int i = 0; i < clientmodsCount; i++)
            {
                if (clientmods[i] == null) { continue; }
                clientmods[i].OnNewFrameDraw3d(this, deltaTime);
            }
        }
        GotoDraw2d(deltaTime);
    }
    
    public static bool IsRail(Packet_BlockType block)
    {
        return block.Rail > 0;	//Does not include Rail0, but this can't be placed.
    }

    public static bool IsEmptyForPhysics(Packet_BlockType block)
    {
        return (block.DrawType == Packet_DrawTypeEnum.Ladder)
            || (block.WalkableType != Packet_WalkableTypeEnum.Solid && block.WalkableType != Packet_WalkableTypeEnum.Fluid);
    }


    internal float FloorFloat(float a)
    {
        if (a >= 0)
        {
            return (int)(a);
        }
        else
        {
            return (int)(a) - 1;
        }
    }

    public static byte[] Serialize(Packet_Client packet, out int retLength)
    {
        CitoMemoryStream ms = new();
        Packet_ClientSerializer.Serialize(ms, packet);

        byte[] data = ms.ToArray();
        retLength = ms.Length();

        return data;
    }

    public void SendPacket(byte[] packet, int packetLength)
    {
        //try
        //{
        INetOutgoingMessage msg = new();
        msg.Write(packet, packetLength);
        main.SendMessage(msg, MyNetDeliveryMethod.ReliableOrdered);
        //}
        //catch
        //{
        //    game.p.ConsoleWriteLine("SendPacket error");
        //}
    }

    internal NetClient main;

    private int packetLen;
    public void SendPacketClient(Packet_Client packetClient)
    {
        byte[] packet = Serialize(packetClient, out packetLen);
        SendPacket(packet, packetLen);
    }

    internal bool IsTeamchat;
    internal void SendChat(string s)
    {
        SendPacketClient(ClientPackets.Chat(s, IsTeamchat ? 1 : 0));
    }

    internal void SendPingReply()
    {
        SendPacketClient(ClientPackets.PingReply());
    }

    internal void SendSetBlock(int x, int y, int z, int mode, int type, int materialslot)
    {
        SendPacketClient(ClientPackets.SetBlock(x, y, z, mode, type, materialslot));
    }
    internal int ActiveMaterial;

    internal void SendFillArea(int startx, int starty, int startz, int endx, int endy, int endz, int blockType)
    {
        SendPacketClient(ClientPackets.FillArea(startx, starty, startz, endx, endy, endz, blockType, ActiveMaterial));
    }

    internal void InventoryClick(Packet_InventoryPosition pos)
    {
        SendPacketClient(ClientPackets.InventoryClick(pos));
    }

    internal void WearItem(Packet_InventoryPosition from, Packet_InventoryPosition to)
    {
        SendPacketClient(ClientPackets.WearItem(from, to));
    }

    internal void MoveToInventory(Packet_InventoryPosition from)
    {
        SendPacketClient(ClientPackets.MoveToInventory(from));
    }

    internal int ChatLinesCount;
    internal string GuiTypingBuffer;
    internal bool IsTyping;

    public void AddChatline(string s)
    {
        Game game = this;
        if (string.IsNullOrEmpty(s))
        {
            return;
        }
        //Check for links in chatline
        bool containsLink = false;
        string linkTarget = "";
        //Normal HTTP links
        if (s.Contains("http://"))
        {
            containsLink = true;
            string[] temp = s.Split(' ');
            for (int i = 0; i < temp.Length; i++)
            {
                if (temp[i].Contains("http://", StringComparison.InvariantCultureIgnoreCase))
                {
                    linkTarget = temp[i];
                    break;
                }
            }
        }
        //Secure HTTPS links
        if (s.Contains("https://"))
        {
            containsLink = true;
            string[] temp = s.Split(' ');
            for (int i = 0; i < temp.Length; i++)
            {
                if (temp[i].Contains("https://", StringComparison.InvariantCultureIgnoreCase))
                {
                    linkTarget = temp[i];
                    break;
                }
            }
        }
        int now = game.platform.TimeMillisecondsFromStart();
        //Display message in multiple lines if it's longer than one line
        if (s.Length > ChatLineLength)
        {
            for (int i = 0; i <= s.Length / ChatLineLength; i++)
            {
                int displayLength = ChatLineLength;
                if (s.Length - (i * ChatLineLength) < ChatLineLength)
                {
                    displayLength = s.Length - (i * ChatLineLength);
                }
                string chunk = s.Substring(i * ChatLineLength, displayLength);
                if (containsLink)
                    ChatLinesAdd(Chatline.CreateClickable(chunk, now, linkTarget));
                else
                    ChatLinesAdd(Chatline.Create(chunk, now));
            }
        }
        else
        {
            if (containsLink)
                ChatLinesAdd(Chatline.CreateClickable(s, now, linkTarget));
            else
                ChatLinesAdd(Chatline.Create(s, now));
        }
    }

    private void ChatLinesAdd(Chatline chatline)
    {
        if (ChatLinesCount >= ChatLinesMax)
        {
            Chatline[] lines2 = new Chatline[ChatLinesMax * 2];
            for (int i = 0; i < ChatLinesMax; i++)
            {
                lines2[i] = ChatLines[i];
            }
            ChatLines = lines2;
            ChatLinesMax *= 2;
        }
        ChatLines[ChatLinesCount++] = chatline;
    }

    internal bool stopPlayerMove;

    internal void Respawn()
    {
        SendPacketClient(ClientPackets.SpecialKeyRespawn());
        stopPlayerMove = true;
    }

    public static bool IsTransparentForLight(Packet_BlockType b)
    {
        return b.DrawType != Packet_DrawTypeEnum.Solid && b.DrawType != Packet_DrawTypeEnum.ClosedDoor;
    }

    internal GuiState guistate;
    internal bool overheadcamera;
    public bool GetFreeMouse()
    {
        if (overheadcamera)
        {
            return true;
        }
        return !platform.IsMousePointerLocked();
    }
    private bool mousePointerLockShouldBe;
    public void SetFreeMouse(bool value)
    {
        mousePointerLockShouldBe = !value;
        if (value)
        {
            platform.ExitMousePointerLock();
        }
        else
        {
            platform.RequestMousePointerLock();
        }
    }
    internal MapLoadingProgressEventArgs maploadingprogress;

    public void MapLoadingStart()
    {
        guistate = GuiState.MapLoading;
        SetFreeMouse(true);
        maploadingprogress = new MapLoadingProgressEventArgs();
        fontMapLoading = FontCi.Create("Arial", 14, 0);
    }

    internal FontCi fontMapLoading;

    internal string invalidVersionDrawMessage;
    internal Packet_Server invalidVersionPacketIdentification;

    internal int xcenter(float width)
    {
        return platform.GetCanvasWidth() / 2 - (int)width / 2;
    }

    internal int ycenter(float height)
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

    internal float zfar()
    {
        if (d_Config3d.viewdistance >= 256)
        {
            return d_Config3d.viewdistance * 2;
        }
        return ENABLE_ZFAR ? d_Config3d.viewdistance : 99999;
    }


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

    internal int SelectedBlockPositionX;
    internal int SelectedBlockPositionY;
    internal int SelectedBlockPositionZ;
    internal int SelectedEntityId;

    internal bool IsWater(int blockType)
    {
        string name = blocktypes[blockType].Name;
        if (name == null)
        {
            return false;
        }
        return name.Contains("Water"); // todo
    }

    internal int mouseCurrentX;
    internal int mouseCurrentY;
    internal Packet_Inventory d_Inventory;


    internal float currentfov()
    {
        if (IronSights)
        {
            Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
            if (item != null && item.ItemClass == Packet_ItemClassEnum.Block)
            {
                if (DeserializeFloat(blocktypes[item.BlockId].IronSightsFovFloat) != 0)
                {
                    return this.fov * DeserializeFloat(blocktypes[item.BlockId].IronSightsFovFloat);
                }
            }
        }
        return this.fov;
    }

    internal bool IronSights;

    internal float DeserializeFloat(int value)
    {
        return (one * value) / 32;
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

    internal Vector3 playerdestination;
    internal void SetCamera(CameraType type)
    {
        if (type == CameraType.Fpp)
        {
            cameratype = CameraType.Fpp;
            SetFreeMouse(false);
            ENABLE_TPP_VIEW = false;
            overheadcamera = false;
        }
        else if (type == CameraType.Tpp)
        {
            cameratype = CameraType.Tpp;
            ENABLE_TPP_VIEW = true;
        }
        else
        {
            cameratype = CameraType.Overhead;
            overheadcamera = true;
            SetFreeMouse(true);
            ENABLE_TPP_VIEW = true;
            playerdestination = new Vector3(player.position.x, player.position.y, player.position.z);
        }
    }

    internal static Packet_InventoryPosition InventoryPositionMaterialSelector(int materialId)
    {
        Packet_InventoryPosition pos = new()
        {
            Type = Packet_InventoryPositionTypeEnum.MaterialSelector,
            MaterialId = materialId
        };
        return pos;
    }

    internal static Packet_InventoryPosition InventoryPositionMainArea(int x, int y)
    {
        Packet_InventoryPosition pos = new()
        {
            Type = Packet_InventoryPositionTypeEnum.MainArea,
            AreaX = x,
            AreaY = y
        };
        return pos;
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
        return DeserializeFloat(blocktypes[item.BlockId].RecoilFloat);
    }

    internal float CurrentAimRadius()
    {
        Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
        if (item == null || item.ItemClass != Packet_ItemClassEnum.Block)
        {
            return 0;
        }
        float radius = (DeserializeFloat(blocktypes[item.BlockId].AimRadiusFloat) / 800) * Width();
        if (IronSights)
        {
            radius = (DeserializeFloat(blocktypes[item.BlockId].IronSightsAimRadiusFloat) / 800) * Width();
        }
        return radius + RadiusWhenMoving * radius * (Math.Min(playervelocity.Length / movespeed, 1));
    }

    internal Random rnd;

    internal GameData d_Data;

    public const int minlight = 0;
    public const int maxlight = 15;

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
    internal int maxdrawdistance;
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

    public void AudioPlay(string file)
    {
        if (!AudioEnabled)
        {
            return;
        }
        AudioPlayAt(file, EyesPosX(), EyesPosY(), EyesPosZ());
    }

    public void AudioPlayAt(string file, float x, float y, float z)
    {
        if (file == null)
        {
            return;
        }
        if (!AudioEnabled)
        {
            return;
        }
        if (assetsLoadProgress != 1)
        {
            return;
        }
        string file_ = file.Replace(".wav", ".ogg");

        if (GetFileLength(file_) == 0)
        {
            platform.ConsoleWriteLine(string.Format("File not found: {0}", file));
            return;
        }

        Sound s = new()
        {
            name = file_,
            x = x,
            y = y,
            z = z
        };
        audio.Add(s);
    }

    public void AudioPlayLoop(string file, bool play, bool restart)
    {
        if ((!AudioEnabled) && play)
        {
            return;
        }
        if (assetsLoadProgress != 1)
        {
            return;
        }

        string file_ = file.Replace(".wav", ".ogg");

        if (GetFileLength(file_) == 0)
        {
            platform.ConsoleWriteLine(string.Format("File not found: {0}", file));
            return;
        }

        if (play)
        {
            Sound s = null;
            bool alreadyPlaying = false;
            for (int i = 0; i < audio.soundsCount; i++)
            {
                if (audio.sounds[i] == null) { continue; }
                if (audio.sounds[i].name == file_)
                {
                    alreadyPlaying = true;
                    s = audio.sounds[i];
                }
            }
            if (!alreadyPlaying)
            {
                s = new Sound
                {
                    name = file_,
                    loop = true
                };
                audio.Add(s);
            }
            s.x = EyesPosX();
            s.y = EyesPosY();
            s.z = EyesPosZ();
        }
        else
        {
            for (int i = 0; i < audio.soundsCount; i++)
            {
                if (audio.sounds[i] == null) { continue; }
                if (audio.sounds[i].name == file_)
                {
                    audio.sounds[i].stop = true;
                }
            }
        }
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
            AudioPlay("death.wav");
            SendPacketClient(ClientPackets.Death(damageSource, sourceId));

            //Respawn(); //Death is not respawn ;)
        }
        else
        {
            AudioPlay(rnd.Next() % 2 == 0 ? "grunt1.wav" : "grunt2.wav");
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

    internal int DialogsCount_()
    {
        int count = 0;
        for (int i = 0; i < dialogsCount; i++)
        {
            if (dialogs[i] != null)
            {
                count++;
            }
        }
        return count;
    }

    internal int GetDialogId(string name)
    {
        for (int i = 0; i < dialogsCount; i++)
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

    internal Vector3i? currentAttackedBlock;

    internal void SendRequestBlob(string[] required, int requiredCount)
    {
        SendPacketClient(ClientPackets.RequestBlob(this, required, requiredCount));
    }

    internal int currentTimeMilliseconds;
    internal GameDataMonsters d_DataMonsters;
    internal int ReceivedMapLength;

    private void InvalidPlayerWarning(int playerid)
    {
        platform.ConsoleWriteLine(string.Format("Position update of nonexistent player {0}.", playerid.ToString()));
    }

    internal static bool EnablePlayerUpdatePosition(int kKey)
    {
        return true;
    }

    internal static bool EnablePlayerUpdatePositionContainsKey(int kKey)
    {
        return false;
    }

 

    internal void SendLeave(int reason)
    {
        SendPacketClient(ClientPackets.Leave(reason));
    }

    public int SerializeFloat(float p)
    {
        return (int)(p * 32);
    }

    public float WeaponAttackStrength()
    {
        return NextFloat(2, 4);
    }

    public float NextFloat(float min, float max)
    {
        return rnd.Next() * (max - min) + min;
    }

    public byte HeadingByte(float orientationX, float orientationY, float orientationZ) =>
     (byte)(int)((orientationY % (2 * MathF.PI)) / (2 * MathF.PI) * 256);

    public byte PitchByte(float orientationX, float orientationY, float orientationZ)
    {
        float xx = (orientationX + MathF.PI) % (2 * MathF.PI);
        return (byte)(int)(xx / (2 * MathF.PI) * 256);
    }

    public void PlaySoundAt(string name, float x, float y, float z)
    {
        if (x == 0 && y == 0 && z == 0)
        {
            AudioPlay(name);
        }
        else
        {
            AudioPlayAt(name, x, z, y);
        }
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
        for (int i = 0; i < FloorFloat(playerHeight) + 1; i++)
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
    internal bool leftpressedpicking;
    internal int pistolcycle;
    internal int lastironsightschangeMilliseconds;
    internal int grenadecookingstartMilliseconds;
    internal int lastpositionsentMilliseconds;

    internal float mouseDeltaX;
    internal float mouseDeltaY;
    private float mouseSmoothingVelX;
    private float mouseSmoothingVelY;
    private float mouseSmoothingAccum;

    internal void UpdateMouseViewportControl(float dt)
    {
        if (mouseSmoothing)
        {
            float constMouseSmoothing1 = 0.85f;
            float constMouseSmoothing2 = 0.8f;
            mouseSmoothingVelX = mouseSmoothingVelX + mouseDeltaX / (300 / 75) * constMouseSmoothing2;
            mouseSmoothingVelY = mouseSmoothingVelY + mouseDeltaY / (300 / 75) * constMouseSmoothing2;
            mouseSmoothingVelX = mouseSmoothingVelX * constMouseSmoothing1;
            mouseSmoothingVelY = mouseSmoothingVelY * constMouseSmoothing1;
        }
        else
        {
            mouseSmoothingVelX = mouseDeltaX;
            mouseSmoothingVelY = mouseDeltaY;
        }

        if (guistate == GuiState.Normal && enableCameraControl && platform.Focused())
        {
            if (!overheadcamera)
            {
                if (platform.IsMousePointerLocked())
                {
                    player.position.roty += mouseSmoothingVelX * rotationspeed * 1f / 75;
                    player.position.rotx += mouseSmoothingVelY * rotationspeed * 1f / 75;
                    player.position.rotx = Math.Clamp(player.position.rotx,
                        MathF.PI / 2 + (one * 15 / 1000),
                        (MathF.PI / 2 + MathF.PI - (one * 15 / 1000)));
                }

                player.position.rotx += touchOrientationDy * constRotationSpeed * (one / 75);
                player.position.roty += touchOrientationDx * constRotationSpeed * (one / 75);
                touchOrientationDx = 0;
                touchOrientationDy = 0;
            }
            if (cameratype == CameraType.Overhead)
            {
                if (mouseMiddle || mouseRight)
                {
                    overheadcameraK.TurnLeft(mouseDeltaX / 70);
                    overheadcameraK.TurnUp(mouseDeltaY / 3);
                }
            }
        }

        mouseDeltaX = 0;
        mouseDeltaY = 0;
    }

    internal string Follow;
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

    public float Dist(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float dz = z2 - z1;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
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

    private ModelData circleModelData;
    public void Circle3i(float x, float y, float radius)
    {
        float angle;
        GLPushMatrix();
        GLLoadIdentity();

        int n = 32;
        if (circleModelData == null)
        {
            circleModelData = new ModelData();
            circleModelData.setMode(DrawModeEnum.Lines);
            circleModelData.indices = new int[n * 2];
            circleModelData.xyz = new float[3 * n];
            circleModelData.rgba = new byte[4 * n];
            circleModelData.uv = new float[2 * n];
            circleModelData.indicesCount = n * 2;
            circleModelData.verticesCount = n;
        }

        for (int i = 0; i < n; i++)
        {
            circleModelData.indices[i * 2] = i;
            circleModelData.indices[i * 2 + 1] = (i + 1) % (n);
        }
        for (int i = 0; i < n; i++)
        {
            angle = (i * 2 * MathF.PI / n);
            circleModelData.xyz[i * 3 + 0] = x + (MathF.Cos(angle) * radius);
            circleModelData.xyz[i * 3 + 1] = y + (MathF.Sin(angle) * radius);
            circleModelData.xyz[i * 3 + 2] = 0;
        }
        for (int i = 0; i < 4 * n; i++)
        {
            circleModelData.rgba[i] = 255;
        }
        for (int i = 0; i < 2 * n; i++)
        {
            circleModelData.uv[i] = 0;
        }

        DrawModelData(circleModelData);

        GLPopMatrix();
    }

    internal int totaltimeMilliseconds;

    public const int entityMonsterIdStart = 128;
    public const int entityMonsterIdCount = 128;
    public const int entityLocalIdStart = 256;

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

    public static bool Vec3Equal(float ax, float ay, float az, float bx, float by, float bz)
    {
        return ax == bx && ay == by && az == bz;
    }

    public const int KeyAltLeft = 5;
    public const int KeyAltRight = 6;

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

    internal bool WaterSwimmingCamera()
    {
        if (GetCameraBlock() == -1) { return true; }
        return IsWater(GetCameraBlock());
    }

    internal bool LavaSwimmingCamera()
    {
        return IsLava(GetCameraBlock());
    }

    private int GetCameraBlock()
    {
        int bx = (int)MathF.Floor(CameraEyeX);
        int by = (int)MathF.Floor(CameraEyeZ);
        int bz = (int)MathF.Floor(CameraEyeY);

        if (!map.IsValidPos(bx, by, bz))
        {
            return 0;
        }
        return map.GetBlockValid(bx, by, bz);
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

    internal int GetKey(Keys key)
    {
        if (options == null)
        {
            return (int)key;
        }
        if (options.Keys[(int)key] != 0)
        {
            return options.Keys[(int)key];
        }
        return (int)key;
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
            float itemSpeed = DeserializeFloat(blocktypes[item.BlockId].WalkSpeedWhenUsedFloat);
            if (itemSpeed != 0)
            {
                movespeednow *= itemSpeed;
            }
            if (IronSights)
            {
                float ironSightsSpeed = DeserializeFloat(blocktypes[item.BlockId].IronSightsMoveSpeedFloat);
                if (ironSightsSpeed != 0)
                {
                    movespeednow *= ironSightsSpeed;
                }
            }
        }
        return movespeednow;
    }

    internal float VectorAngleGet(float qX, float qY, float qZ)
    {
        return (MathF.Acos(qX / Length(qX, qY, qZ)) * Math.Sign(qZ));
    }

    internal float Length(float x, float y, float z)
    {
        return MathF.Sqrt(x * x + y * y + z * z);
    }

    internal void HandleMaterialKeys(int eKey)
    {
        if (eKey == GetKey(Keys.KeyPad1)) { ActiveMaterial = 0; }
        if (eKey == GetKey(Keys.KeyPad2)) { ActiveMaterial = 1; }
        if (eKey == GetKey(Keys.KeyPad3)) { ActiveMaterial = 2; }
        if (eKey == GetKey(Keys.KeyPad4)) { ActiveMaterial = 3; }
        if (eKey == GetKey(Keys.KeyPad5)) { ActiveMaterial = 4; }
        if (eKey == GetKey(Keys.KeyPad6)) { ActiveMaterial = 5; }
        if (eKey == GetKey(Keys.KeyPad7)) { ActiveMaterial = 6; }
        if (eKey == GetKey(Keys.KeyPad8)) { ActiveMaterial = 7; }
        if (eKey == GetKey(Keys.KeyPad9)) { ActiveMaterial = 8; }
        if (eKey == GetKey(Keys.KeyPad0)) { ActiveMaterial = 9; }
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

    internal void MouseWheelChanged(MouseWheelEventArgs e)
    {
        float eDeltaPrecise = e.OffsetY;
        if (keyboardState[GetKey(Keys.LeftShift)])
        {
            if (cameratype == CameraType.Overhead)
            {
                overheadcameradistance -= eDeltaPrecise;
                if (overheadcameradistance < TPP_CAMERA_DISTANCE_MIN) { overheadcameradistance = TPP_CAMERA_DISTANCE_MIN; }
                if (overheadcameradistance > TPP_CAMERA_DISTANCE_MAX) { overheadcameradistance = TPP_CAMERA_DISTANCE_MAX; }
            }
            if (cameratype == CameraType.Tpp)
            {
                tppcameradistance -= eDeltaPrecise;
                if (tppcameradistance < TPP_CAMERA_DISTANCE_MIN) { tppcameradistance = TPP_CAMERA_DISTANCE_MIN; }
                if (tppcameradistance > TPP_CAMERA_DISTANCE_MAX) { tppcameradistance = TPP_CAMERA_DISTANCE_MAX; }
            }
        }
        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].OnMouseWheelChanged(this, e);
        }
    }

    internal void Connect(string serverAddress, int port, string username, string auth)
    {
        main.Start();
        main.Connect(serverAddress, port);
        SendPacketClient(ClientPackets.CreateLoginPacket(platform, username, auth));
    }

    internal void Connect_(string serverAddress, int port, string username, string auth, string serverPassword)
    {
        main.Start();
        main.Connect(serverAddress, port);
        SendPacketClient(ClientPackets.CreateLoginPacket_(platform, username, auth, serverPassword));
    }

    internal bool shadowssimple;
    internal bool shouldRedrawAllBlocks;
    internal void RedrawAllBlocks()
    {
        shouldRedrawAllBlocks = true;
    }

    //public const int clearcolorR = 171;
    //public const int clearcolorG = 202;
    //public const int clearcolorB = 228;
    //public const int clearcolorA = 255;
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

    internal BlockPosSide Nearest(ArraySegment<BlockPosSide> pick2, int pick2Count, float x, float y, float z)
    {
        float minDist = 1000 * 1000;
        BlockPosSide nearest = null;
        for (int i = 0; i < pick2Count; i++)
        {
            float dist = Dist(pick2[i].blockPos[0], pick2[i].blockPos[1], pick2[i].blockPos[2], x, y, z);
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

    internal void KeyUp(int eKey)
    {
        keyboardStateRaw[eKey] = false;
        for (int i = 0; i < clientmodsCount; i++)
        {
            KeyEventArgs args_ = new();
            args_.SetKeyCode(eKey);
            clientmods[i].OnKeyUp(this, args_);
            if (args_.GetHandled())
            {
                return;
            }
        }
        keyboardState[eKey] = false;
        if (eKey == GetKey(Keys.LeftShift) || eKey == GetKey(Keys.RightShift))
        {
            IsShiftPressed = false;
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

    internal void Draw2dText1(string text, int x, int y, int fontsize, int? color, bool enabledepthtest)
    {
        FontCi font = new()
        {
            family = "Arial",
            size = fontsize
        };

        Draw2dText(text, font, x, y, color, enabledepthtest);
    }

    internal void UseInventory(Packet_Inventory packet_Inventory)
    {
        d_Inventory = packet_Inventory;
        d_InventoryUtil.UpdateInventory(packet_Inventory);
    }

    internal void KeyPress(int eKeyChar)
    {
        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) { continue; }
            KeyPressEventArgs args_ = new();
            args_.SetKeyChar(eKeyChar);
            clientmods[i].OnKeyPress(this, args_);
            if (args_.GetHandled())
            {
                return;
            }
        }
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
        Set3dProjection(zfar_, currentfov());
    }

    internal void Set3dProjection2()
    {
        Set3dProjection1(zfar());
    }

    internal void SendGameResolution()
    {
        SendPacketClient(ClientPackets.GameResolution(Width(), Height()));
    }

    private bool sendResize;
    internal void OnResize()
    {
        platform.GlViewport(0, 0, Width(), Height());
        this.Set3dProjection2();
        //Notify server of size change
        if (sendResize)
        {
            SendGameResolution();
        }
    }

    internal void Reconnect()
    {
        reconnect = true;
    }

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

    internal void ClientCommand(string s_)
    {
        if (s_ == "")
        {
            return;
        }

        string[] ss = s_.Split(' ');
        if (s_.StartsWith("."))
        {
            string strFreemoveNotAllowed = language.FreemoveNotAllowed();
            string cmd = ss[0][1..];
            string arguments;
            if (!s_.Contains(" ", StringComparison.InvariantCultureIgnoreCase))
            {
                arguments = "";
            }
            else
            {
                arguments = s_[s_.IndexOf(" ", StringComparison.InvariantCultureIgnoreCase)..];
            }
            arguments = arguments.Trim();

            // Command requiring no arguments
            if (cmd == "clients")
            {
                Log("Clients:");
                for (int i = 0; i < entitiesCount; i++)
                {
                    Entity entity = entities[i];
                    if (entity == null) { continue; }
                    if (entity.drawName == null) { continue; }
                    if (!entity.drawName.ClientAutoComplete) { continue; }
                    Log(string.Format("{0} {1}", i.ToString(), entities[i].drawName.Name));
                }
            }
            else if (cmd == "reconnect")
            {
                Reconnect();
            }
            else if (cmd == "m")
            {
                mouseSmoothing = !mouseSmoothing;
                if (mouseSmoothing) { Log("Mouse smoothing enabled."); }
                else { Log("Mouse smoothing disabled."); }
            }
            // Commands requiring boolean arguments
            else if (cmd == "pos")
            {
                ENABLE_DRAWPOSITION = BoolCommandArgument(arguments);
            }
            else if (cmd == "noclip")
            {
                controls.noclip = BoolCommandArgument(arguments);
            }
            else if (cmd == "freemove")
            {
                if (this.AllowFreemove)
                {
                    controls.freemove = BoolCommandArgument(arguments);
                }
                else
                {
                    Log(strFreemoveNotAllowed);
                    return;
                }
            }
            else if (cmd == "gui")
            {
                ENABLE_DRAW2D = BoolCommandArgument(arguments);
            }
            // Commands requiring numeric arguments
            else if (arguments != "")
            {
                if (cmd == "fog")
                {
                    int foglevel;
                    foglevel = int.Parse(arguments);
                    {
                        int foglevel2 = foglevel;
                        if (foglevel2 > 1024)
                        {
                            foglevel2 = 1024;
                        }
                        if (foglevel2 % 2 == 0)
                        {
                            foglevel2--;
                        }
                        d_Config3d.viewdistance = foglevel2;
                    }
                    OnResize();
                }
                else if (cmd == "fov")
                {
                    int arg = int.Parse(arguments);
                    int minfov = 1;
                    int maxfov = 179;
                    if (!issingleplayer)
                    {
                        minfov = 60;
                    }
                    if (arg < minfov || arg > maxfov)
                    {
                        Log(string.Format("Valid field of view: {0}-{1}", minfov.ToString(), maxfov.ToString()));
                    }
                    else
                    {
                        float fov_ = (2 * MathF.PI * (one * arg / 360));
                        this.fov = fov_;
                        OnResize();
                    }
                }
                else if (cmd == "movespeed")
                {
                    if (this.AllowFreemove)
                    {
                        if (float.Parse(arguments) <= 500)
                        {
                            movespeed = basemovespeed * float.Parse(arguments);
                            AddChatline(string.Format("Movespeed: {0}x", arguments));
                        }
                        else
                        {
                            AddChatline("Entered movespeed to high! max. 500x");
                        }
                    }
                    else
                    {
                        Log(strFreemoveNotAllowed);
                        return;
                    }
                }
                else if (cmd == "serverinfo")
                {
                    //Fetches server info from given adress
                    string[] split = arguments.Split(':');
                    if (split.Length == 2)
                    {
                        QueryClient qClient = new();
                        qClient.SetPlatform(platform);
                        qClient.PerformQuery(split[0], int.Parse(split[1]));
                        if (qClient.querySuccess)
                        {
                            //Received result
                            QueryResult r = qClient.GetResult();
                            AddChatline(r.GameMode);
                            AddChatline(r.MapSizeX.ToString());
                            AddChatline(r.MapSizeY.ToString());
                            AddChatline(r.MapSizeZ.ToString());
                            AddChatline(r.MaxPlayers.ToString());
                            AddChatline(r.MOTD);
                            AddChatline(r.Name);
                            AddChatline(r.PlayerCount.ToString());
                            AddChatline(r.PlayerList);
                            AddChatline(r.Port.ToString());
                            AddChatline(r.PublicHash);
                            AddChatline(r.ServerVersion);
                        }
                        AddChatline(qClient.GetServerMessage());
                    }
                }
            }
            else
            {
                //Send client command to server if none matches
                string chatline = GuiTypingBuffer[..Math.Min(GuiTypingBuffer.Length, 256)];
                SendChat(chatline);
            }
            //Process clientside mod commands anyway
            for (int i = 0; i < clientmodsCount; i++)
            {
                ClientCommandArgs args = new()
                {
                    arguments = arguments,
                    command = cmd
                };
                clientmods[i].OnClientCommand(this, args);
            }
        }
        else
        {
            //Regular chat message or server command. Send to server
            string chatline = GuiTypingBuffer[..Math.Min(GuiTypingBuffer.Length, 4096)];
            SendChat(chatline);
        }
    }
    public bool BoolCommandArgument(string arguments)
    {
        arguments = arguments.Trim();
        return (arguments == "" || arguments == "1" || arguments == "on" || arguments == "yes");
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

    private bool HasAsset(string md5, string name)
    {
        for (int i = 0; i < assets.count; i++)
        {
            if (assets.items[i].md5 == md5)
            {
                if (assets.items[i].name == name)
                {
                    //Check both MD5 and name as there might be files with same content
                    return true;
                }
            }
        }
        return false;
    }

  

    private void CacheAsset(Asset asset)
    {
        //Check if checksum is given (prevents crash on old servers)
        if (asset.md5 == null)
        {
            return;
        }
        //Check if given checksum is valid
        if (!platform.IsChecksum(asset.md5))
        {
            //Skip saving
            return;
        }
        //Only cache a file if it's not already cached
        if (!platform.IsCached(asset.md5))
        {
            platform.SaveAssetToCache(asset);
        }
    }

    public void SetFile(string name, string md5, byte[] downloaded, int downloadedLength)
    {
        string nameLowercase = name.ToLowerInvariant();

        // Update mouse cursor if changed
        if (nameLowercase == "mousecursor.png")
        {
            platform.SetWindowCursor(0, 0, 32, 32, downloaded, downloadedLength);
        }

        //Create new asset from given data
        Asset newAsset = new()
        {
            data = downloaded,
            dataLength = downloadedLength,
            name = nameLowercase,
            md5 = md5
        };

        for (int i = 0; i < assets.count; i++)
        {
            if (assets.items[i] == null)
            {
                continue;
            }
            if (assets.items[i].name == nameLowercase)
            {
                if (options.UseServerTextures)
                {
                    //If server textures are allowed, replace content of current asset
                    assets.items[i] = newAsset;
                }
                //Cache asset for later use
                CacheAsset(newAsset);
                return;
            }
        }
        //Add new asset to asset list
        assets.items[assets.count++] = newAsset;

        //Store new asset in cache
        CacheAsset(newAsset);
    }

   

    internal byte[] GetFile(string p)
    {
        string pLowercase = p.ToLowerInvariant();
        for (int i = 0; i < assets.count; i++)
        {
            if (assets.items[i].name == pLowercase)
            {
                return assets.items[i].data;
            }
        }
        return null;
    }

    internal int GetFileLength(string p)
    {
        string pLowercase = p.ToLowerInvariant();
        for (int i = 0; i < assets.count; i++)
        {
            if (assets.items[i].name == pLowercase)
            {
                return assets.items[i].dataLength;
            }
        }
        return 0;
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

    
    

    public static bool StringEquals(string strA, string strB)
    {
        if (strA == null && strB == null)
        {
            return true;
        }
        if (strA == null || strB == null)
        {
            return false;
        }
        return strA == strB;
    }

    internal void KeyDown(int eKey)
    {
        keyboardStateRaw[eKey] = true;
        if (guistate != GuiState.MapLoading)
        {
            // only handle keys once game has been loaded
            for (int i = 0; i < clientmodsCount; i++)
            {
                KeyEventArgs args_ = new();
                args_.SetKeyCode(eKey);
                clientmods[i].OnKeyDown(this, args_);
                if (args_.GetHandled())
                {
                    return;
                }
            }
        }
        keyboardState[eKey] = true;
        InvalidVersionAllow();
        if (eKey == GetKey(Keys.F6))
        {
            float lagSeconds = one * (platform.TimeMillisecondsFromStart() - LastReceivedMilliseconds) / 1000;
            if ((lagSeconds >= DISCONNECTED_ICON_AFTER_SECONDS) || guistate == GuiState.MapLoading)
            {
                Reconnect();
            }
        }
        if (eKey == GetKey(Keys.LeftShift) || eKey == GetKey(Keys.RightShift))
        {
            IsShiftPressed = true;
        }
        if (guistate == GuiState.Normal)
        {
            string strFreemoveNotAllowed = "You are not allowed to enable freemove.";

            if (eKey == GetKey(Keys.F1))
            {
                if (!this.AllowFreemove)
                {
                    Log(strFreemoveNotAllowed);
                    return;
                }
                movespeed = basemovespeed * 1;
                Log("Move speed: 1x.");
            }
            if (eKey == GetKey(Keys.F2))
            {
                if (!this.AllowFreemove)
                {
                    Log(strFreemoveNotAllowed);
                    return;
                }
                movespeed = basemovespeed * 10;
                Log(string.Format(language.MoveSpeed(), "10"));
            }
            if (eKey == GetKey(Keys.F3))
            {
                if (!this.AllowFreemove)
                {
                    Log(strFreemoveNotAllowed);
                    return;
                }
                stopPlayerMove = true;
                if (!controls.freemove)
                {
                    controls.freemove = true;
                    Log(language.MoveFree());
                }
                else if (controls.freemove && (!controls.noclip))
                {
                    controls.noclip = true;
                    Log(language.MoveFreeNoclip());
                }
                else if (controls.freemove && controls.noclip)
                {
                    controls.freemove = false;
                    controls.noclip = false;
                    Log(language.MoveNormal());
                }
            }
            if (eKey == GetKey(Keys.I))
            {
                drawblockinfo = !drawblockinfo;
            }
            int playerx = (int)(player.position.x);
            int playery = (int)(player.position.z);
            if ((playerx >= 0 && playerx < map.MapSizeX)
                && (playery >= 0 && playery < map.MapSizeY))
            {
                performanceinfo["height"] = string.Format("height:{0}", d_Heightmap.GetBlock(playerx, playery).ToString());
            }
            if (eKey == GetKey(Keys.F5))
            {
                CameraChange();
            }
            if (eKey == GetKey(Keys.Equal) || eKey == GetKey(Keys.Equal))
            {
                if (cameratype == CameraType.Overhead)
                {
                    overheadcameradistance -= 1;
                }
                else if (cameratype == CameraType.Tpp)
                {
                    tppcameradistance -= 1;
                }
            }
            if (eKey == GetKey(Keys.Minus) || eKey == GetKey(Keys.KeyPadSubtract))
            {
                if (cameratype == CameraType.Overhead)
                {
                    overheadcameradistance += 1;
                }
                else if (cameratype == CameraType.Tpp)
                {
                    tppcameradistance += 1;
                }
            }
            if (overheadcameradistance < TPP_CAMERA_DISTANCE_MIN) { overheadcameradistance = TPP_CAMERA_DISTANCE_MIN; }
            if (overheadcameradistance > TPP_CAMERA_DISTANCE_MAX) { overheadcameradistance = TPP_CAMERA_DISTANCE_MAX; }

            if (tppcameradistance < TPP_CAMERA_DISTANCE_MIN) { tppcameradistance = TPP_CAMERA_DISTANCE_MIN; }
            if (tppcameradistance > TPP_CAMERA_DISTANCE_MAX) { tppcameradistance = TPP_CAMERA_DISTANCE_MAX; }

            if (eKey == GetKey(Keys.F6))
            {
                RedrawAllBlocks();
            }
            if (eKey == (int)Keys.F8)
            {
                ToggleVsync();
                if (ENABLE_LAG == 0) { Log(language.FrameRateVsync()); }
                if (ENABLE_LAG == 1) { Log(language.FrameRateUnlimited()); }
                if (ENABLE_LAG == 2) { Log(language.FrameRateLagSimulation()); }
            }
            if (eKey == GetKey(Keys.Tab))
            {
                SendPacketClient(ClientPackets.SpecialKeyTabPlayerList());
            }
            if (eKey == GetKey(Keys.E))
            {
                if (currentAttackedBlock != null)
                {
                    int posX = currentAttackedBlock.Value.X;
                    int posY = currentAttackedBlock.Value.Y;
                    int posZ = currentAttackedBlock.Value.Z;
                    int blocktype = map.GetBlock(currentAttackedBlock.Value.X, currentAttackedBlock.Value.Y, currentAttackedBlock.Value.Z);
                    if (IsUsableBlock(blocktype))
                    {
                        if (d_Data.IsRailTile(blocktype))
                        {
                            player.position.x = posX + (one / 2);
                            player.position.y = posZ + 1;
                            player.position.z = posY + (one / 2);
                            controls.freemove = false;
                        }
                        else
                        {
                            SendSetBlock(posX, posY, posZ, Packet_BlockSetModeEnum.Use, 0, ActiveMaterial);
                        }
                    }
                }
                if (currentlyAttackedEntity != -1)
                {
                    if (entities[currentlyAttackedEntity].usable)
                    {
                        for (int i = 0; i < clientmodsCount; i++)
                        {
                            if (clientmods[i] == null) { continue; }
                            OnUseEntityArgs args = new()
                            {
                                entityId = currentlyAttackedEntity
                            };
                            clientmods[i].OnUseEntity(this, args);
                        }
                        SendPacketClient(ClientPackets.UseEntity(currentlyAttackedEntity));
                    }
                }
            }
            if (eKey == GetKey(Keys.O))
            {
                Respawn();
            }
            if (eKey == GetKey(Keys.L))
            {
                SendPacketClient(ClientPackets.SpecialKeySelectTeam());
            }
            if (eKey == GetKey(Keys.P))
            {
                SendPacketClient(ClientPackets.SpecialKeySetSpawn());

                playerPositionSpawnX = player.position.x;
                playerPositionSpawnY = player.position.y;
                playerPositionSpawnZ = player.position.z;

                player.position.x = (int)(player.position.x) + one / 2;
                //player.playerposition.Y = player.playerposition.Y;
                player.position.z = (int)(player.position.z) + one / 2;
            }
            if (eKey == GetKey(Keys.F))
            {
                ToggleFog();
                Log(string.Format(language.FogDistance(), ((int)d_Config3d.viewdistance).ToString()));
                OnResize();
            }
            if (eKey == GetKey(Keys.B))
            {
                ShowInventory();
                return;
            }
            HandleMaterialKeys(eKey);
        }
        if (guistate == GuiState.Inventory)
        {
            if (eKey == GetKey(Keys.B)
                || eKey == GetKey(Keys.Escape))
            {
                GuiStateBackToGame();
            }
            return;
        }
        if (guistate == GuiState.MapLoading)
        {
            //Return to main menu when ESC key is pressed while loading
            if (eKey == GetKey(Keys.Escape))
            {
                ExitToMainMenu_();
            }
        }
        if (guistate == GuiState.CraftingRecipes)
        {
            if (eKey == GetKey(Keys.Escape))
            {
                GuiStateBackToGame();
            }
        }
        if (guistate == GuiState.Normal)
        {
            if (eKey == GetKey(Keys.Escape))
            {
                EscapeMenuStart();
                return;
            }
        }
    }

    internal bool escapeMenuRestart;
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

    public void CameraChange()
    {
        if (Follow != null)
        {
            //Prevents switching camera mode when following
            return;
        }
        if (cameratype == CameraType.Fpp)
        {
            cameratype = CameraType.Tpp;
            ENABLE_TPP_VIEW = true;
        }
        else if (cameratype == CameraType.Tpp)
        {
            cameratype = CameraType.Overhead;
            overheadcamera = true;
            SetFreeMouse(true);
            ENABLE_TPP_VIEW = true;
            playerdestination = new Vector3(player.position.x, player.position.y, player.position.z);
        }
        else if (cameratype == CameraType.Overhead)
        {
            cameratype = CameraType.Fpp;
            SetFreeMouse(false);
            ENABLE_TPP_VIEW = false;
            overheadcamera = false;
        }
        else
        {
            platform.ThrowException("");
        }
    }
    internal bool drawblockinfo;

    internal void Draw2d(float dt)
    {
        if (!ENABLE_DRAW2D)
        {
            return;
        }

        OrthoMode(Width(), Height());

        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].OnNewFrameDraw2d(this, dt);
        }

        PerspectiveMode();
    }

    internal void FrameTick(float dt)
    {
        NewFrameEventArgs args_ = new();
        args_.SetDt(dt);
        for (int i = 0; i < clientmodsCount; i++)
        {
            clientmods[i].OnNewFrameFixed(this, args_);
        }
        for (int i = 0; i < entitiesCount; i++)
        {
            Entity e = entities[i];
            if (e == null) { continue; }
            for (int k = 0; k < e.scriptsCount; k++)
            {
                e.scripts[k].OnNewFrameFixed(this, i, dt);
            }
        }
        RevertSpeculative(dt);

        if (guistate == GuiState.MapLoading) { return; }

        float orientationX = MathF.Sin(player.position.roty);
        float orientationY = 0;
        float orientationZ = -MathF.Cos(player.position.roty);
        platform.AudioUpdateListener(EyesPosX(), EyesPosY(), EyesPosZ(), orientationX, orientationY, orientationZ);

        playervelocity.X = player.position.x - lastplayerpositionX;
        playervelocity.Y = player.position.y - lastplayerpositionY;
        playervelocity.Z = player.position.z - lastplayerpositionZ;
        playervelocity.X *= 75;
        playervelocity.Y *= 75;
        playervelocity.Z *= 75;
        lastplayerpositionX = player.position.x;
        lastplayerpositionY = player.position.y;
        lastplayerpositionZ = player.position.z;
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

        PickSort(pick2, retCount, line.Start[0], line.Start[1], line.Start[2]);

        return pick2;
    }


    private void PickSort(ArraySegment<BlockPosSide> pick, int pickCount, float x, float y, float z)
    {
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < pickCount - 1; i++)
            {
                float dist = Dist(pick[i].blockPos[0], pick[i].blockPos[1], pick[i].blockPos[2], x, y, z);
                float distNext = Dist(pick[i + 1].blockPos[0], pick[i + 1].blockPos[1], pick[i + 1].blockPos[2], x, y, z);
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

    internal void MouseDown(MouseEventArgs args)
    {
        if (args.GetButton() == MouseButtonEnum.Left) { mouseLeft = true; }
        if (args.GetButton() == MouseButtonEnum.Middle) { mouseMiddle = true; }
        if (args.GetButton() == MouseButtonEnum.Right) { mouseRight = true; }
        if (args.GetButton() == MouseButtonEnum.Left)
        {
            mouseleftclick = true;
        }
        if (args.GetButton() == MouseButtonEnum.Right)
        {
            mouserightclick = true;
        }
        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].OnMouseDown(this, args);
        }
        if (mousePointerLockShouldBe)
        {
            platform.RequestMousePointerLock();
            mouseDeltaX = 0;
            mouseDeltaY = 0;
        }
        InvalidVersionAllow();
    }

    internal void MouseUp(MouseEventArgs args)
    {
        if (args.GetButton() == MouseButtonEnum.Left) { mouseLeft = false; }
        if (args.GetButton() == MouseButtonEnum.Middle) { mouseMiddle = false; }
        if (args.GetButton() == MouseButtonEnum.Right) { mouseRight = false; }
        if (args.GetButton() == MouseButtonEnum.Left)
        {
            mouseleftdeclick = true;
        }
        if (args.GetButton() == MouseButtonEnum.Right)
        {
            mouserightdeclick = true;
        }
        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].OnMouseUp(this, args);
        }
    }

    public GamePlatform GetPlatform()
    {
        return platform;
    }

    public void SetPlatform(GamePlatform value)
    {
        platform = value;
    }

    internal void OnFocusChanged()
    {
        if (guistate == GuiState.Normal)
        {
            EscapeMenuStart();
        }
    }

    internal void Connect__()
    {
        if (connectdata.ServerPassword == null || connectdata.ServerPassword == "")
        {
            Connect(connectdata.Ip, connectdata.Port, connectdata.Username, connectdata.Auth);
        }
        else
        {
            Connect_(connectdata.Ip, connectdata.Port, connectdata.Username, connectdata.Auth, connectdata.ServerPassword);
        }
        MapLoadingStart();
    }

   
    private void UpdateResize()
    {
        if (lastWidth != platform.GetCanvasWidth()
            || lastHeight != platform.GetCanvasHeight())
        {
            lastWidth = platform.GetCanvasWidth();
            lastHeight = platform.GetCanvasHeight();
            OnResize();
        }
    }

    private bool startedconnecting;
    internal void GotoDraw2d(float dt)
    {
        SetAmbientLight(ColorFromArgb(255, 255, 255, 255));
        Draw2d(dt);

        NewFrameEventArgs args_ = new();
        args_.SetDt(dt);
        for (int i = 0; i < clientmodsCount; i++)
        {
            clientmods[i].OnNewFrame(this, args_);
        }

        mouseleftclick = mouserightclick = false;
        mouseleftdeclick = mouserightdeclick = false;
        if ((!issingleplayer)
            || (issingleplayer && platform.SinglePlayerServerLoaded())
            || (!platform.SinglePlayerServerAvailable()))
        {
            if (!startedconnecting)
            {
                startedconnecting = true;
                Connect__();
            }
        }
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

    public void OnTouchStart(TouchEventArgs e)
    {
        InvalidVersionAllow();
        mouseCurrentX = e.GetX();
        mouseCurrentY = e.GetY();
        mouseleftclick = true;

        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].OnTouchStart(this, e);
            if (e.GetHandled())
            {
                return;
            }
        }
    }

    public void OnTouchMove(TouchEventArgs e)
    {
        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].OnTouchMove(this, e);
            if (e.GetHandled())
            {
                return;
            }
        }
    }

    public void OnTouchEnd(TouchEventArgs e)
    {
        mouseCurrentX = 0;
        mouseCurrentY = 0;
        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].OnTouchEnd(this, e);
            if (e.GetHandled())
            {
                return;
            }
        }
    }

    public static void OnBackPressed()
    {
    }

    public void MouseMove(MouseEventArgs e)
    {
        if (!e.GetEmulated() || e.GetForceUsage())
        {
            // Set x and y only for real MouseMove events
            mouseCurrentX = e.GetX();
            mouseCurrentY = e.GetY();
        }
        if (e.GetEmulated() || e.GetForceUsage())
        {
            // Get delta only from emulated events (actual events negate previous ones)
            mouseDeltaX += e.GetMovementX();
            mouseDeltaY += e.GetMovementY();
        }
        for (int i = 0; i < clientmodsCount; i++)
        {
            if (clientmods[i] == null) { continue; }
            clientmods[i].OnMouseMove(this, e);
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

    public void StartTyping()
    {
        GuiTyping = TypingState.Typing;
        IsTyping = true;
        GuiTypingBuffer = "";
        IsTeamchat = false;
    }

    public void StopTyping()
    {
        GuiTyping = TypingState.None;
    }

   
    internal static float Angle256ToRad(int value)
    {
        float one_ = 1;
        return ((one_ * value) / 255) * MathF.PI * 2;
    }

    internal static float RadToAngle256(float value)
    {
        return (value / (2 * MathF.PI)) * 255;
    }

   
}
