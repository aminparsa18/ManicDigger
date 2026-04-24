using MemoryPack;
using System.Buffers;
using System.Runtime.InteropServices;

namespace ManicDigger.Mods;

public class ModNetworkProcess : ModBase
{
    private readonly IGameClient _game;
    private readonly IGamePlatform _platform;

    public ModNetworkProcess(IGameClient game, IGamePlatform gamePlatform)
    {
        _game = game;
        _platform = gamePlatform;
        CurrentChunk = new byte[1024 * 64];
        CurrentChunkCount = 0;
        receivedchunk = new int[32 * 32 * 32];
        decompressedchunk = new byte[32 * 32 * 32 * 2];
    }

    internal byte[] CurrentChunk;
    internal int CurrentChunkCount;
    private readonly int[] receivedchunk;
    private readonly byte[] decompressedchunk;

    // ── Scratch buffers ──────────────────────────────────────────────────────
    // Static so they are shared across all calls to the static ProcessPacket.
    // Each is only touched on the main thread, so there is no concurrency risk.

    /// <summary>
    /// Scratch buffer used when building the per-block-type texture ID list in
    /// <see cref="ProcessPacket"/> (BlockTypes case).
    /// Avoids allocating <c>new string[7]</c> for each of the 1,024 block types
    /// every time block-type data arrives from the server.
    /// </summary>
    private static readonly string[] s_textureIdScratch = new string[7];

    private static int Index3d(int x, int y, int h, int sizex, int sizey)
    {
        return (h * sizey + y) * sizex + x;
    }

    public override void OnReadOnlyBackgroundThread(float dt)
    {
        NetworkProcess();
    }

    public void NetworkProcess()
    {
        _game.CurrentTimeMilliseconds = _game.Platform.TimeMillisecondsFromStart;
        if (_game.NetClient == null)
        {
            return;
        }

        NetIncomingMessage msg;
        for (; ; )
        {
            if (_game.InvalidVersionPacketIdentification != null) { break; }

            msg = _game.NetClient.ReadMessage();
            if (msg == null) { break; }

            // ── Payload copy via ArrayPool ────────────────────────────────────
            // msg.Payload.ToArray() previously allocated a new byte[] on every
            // received message. We instead rent a buffer from the shared pool,
            // copy the payload into it, and return it immediately after
            // TryReadPacket completes (deserialization is synchronous).
            int payloadLength = msg.Payload.Length;
            byte[] rentedPayload = ArrayPool<byte>.Shared.Rent(payloadLength);
            try
            {
                // CopyTo works for both ArraySegment<byte> and byte[].
                msg.Payload.CopyTo(rentedPayload);
                TryReadPacket(rentedPayload, payloadLength);
            }
            finally
            {
                // Safe to return here: DeserializeBuffer in TryReadPacket is
                // synchronous and the Packet_Server it populates lives in the
                // closure — not in rentedPayload.
                ArrayPool<byte>.Shared.Return(rentedPayload);
            }
        }
    }

    public void TryReadPacket(byte[] data, int dataLength)
    {
        Packet_Server packet = MemoryPackSerializer.Deserialize<Packet_Server>(data.AsSpan(0, dataLength));

        ProcessInBackground(packet);

        _game.QueueActionCommit(() => ProcessPacket(packet));

        _game.LastReceivedMilliseconds = _game.CurrentTimeMilliseconds;
    }

    private void ProcessInBackground(Packet_Server packet)
    {
        switch (packet.Id)
        {
            case Packet_ServerIdEnum.ChunkPart:
                {
                    byte[] arr = packet.ChunkPart.CompressedChunkPart;

                    // ── Guard against CurrentChunk overflow ───────────────────────
                    // If accumulated parts would exceed the buffer, discard and reset.
                    // The matching Chunk_ packet will receive CurrentChunkCount == 0
                    // and zero-fill receivedchunk, which is safer than a corrupt decompress.
                    if (CurrentChunkCount + arr.Length > CurrentChunk.Length)
                    {
                        CurrentChunkCount = 0;
                        break;
                    }
                    Buffer.BlockCopy(arr, 0, CurrentChunk, CurrentChunkCount, arr.Length);
                    CurrentChunkCount += arr.Length;
                    break;
                }

            case Packet_ServerIdEnum.Chunk_:
                {
                    Packet_ServerChunk p = packet.Chunk_;

                    // ── Always reset CurrentChunkCount, even on decompression failure ──
                    // Previously, a ZLibException from GzipDecompress left CurrentChunkCount
                    // non-zero. The next Chunk_ packet would then try to decompress the same
                    // stale bytes → second ZLibException → cascade under memory pressure.
                    // We capture the count, reset immediately, then attempt decompression.
                    int compressedLength = CurrentChunkCount;
                    CurrentChunkCount = 0;

                    if (compressedLength != 0)
                    {
                        try
                        {
                            _platform.GzipDecompress(CurrentChunk, compressedLength, decompressedchunk);
                        }
                        catch (Exception ex)
                        {
                            // Decompression failed — log and skip this chunk.
                            // CurrentChunkCount is already 0 so the next Chunk_ is clean.
                            _game.ChatLog(string.Format("[NET] Chunk decompression failed: {0}", ex.Message));
                            break;
                        }

                        int i = 0;
                        for (int zz = 0; zz < p.SizeZ; zz++)
                        {
                            for (int yy = 0; yy < p.SizeY; yy++)
                            {
                                for (int xx = 0; xx < p.SizeX; xx++)
                                {
                                    int block = (decompressedchunk[i + 1] << 8) + decompressedchunk[i];
                                    if (block < GlobalVar.MAX_BLOCKTYPES)
                                    {
                                        receivedchunk[Index3d(xx, yy, zz, p.SizeX, p.SizeY)] = block;
                                    }
                                    i += 2;
                                }
                            }
                        }
                    }
                    else
                    {
                        int size = p.SizeX * p.SizeY * p.SizeZ;
                        for (int i = 0; i < size; i++) { receivedchunk[i] = 0; }
                    }

                    _game.VoxelMap.SetMapPortion(p.X, p.Y, p.Z, receivedchunk, p.SizeX, p.SizeY, p.SizeZ);
                    _game.ReceivedMapLength += compressedLength;
                    break;
                }

            case Packet_ServerIdEnum.HeightmapChunk:
                {
                    Packet_ServerHeightmapChunk p = packet.HeightmapChunk;
                    _platform.GzipDecompress(p.CompressedHeightmap, p.CompressedHeightmap.Length, decompressedchunk);
                    ReadOnlySpan<ushort> decompressedchunk1 = MemoryMarshal.Cast<byte, ushort>(
                        decompressedchunk.AsSpan(0, p.SizeX * p.SizeY * 2));
                    for (int xx = 0; xx < p.SizeX; xx++)
                    {
                        for (int yy = 0; yy < p.SizeY; yy++)
                        {
                            int height = decompressedchunk1[VectorIndexUtil.Index2d(xx, yy, p.SizeX)];
                            _game.Heightmap.SetBlock(p.X + xx, p.Y + yy, height);
                        }
                    }
                    break;
                }
        }
    }

    internal void ProcessPacket(Packet_Server packet)
    {
        _game.PacketHandlers[(int)packet.Id]?.Handle(_game, packet);
        switch (packet.Id)
        {
            case Packet_ServerIdEnum.ServerIdentification:
                {
                    string invalidversionstr = _game.Language.InvalidVersionConnectAnyway();
                    _game.ServerGameVersion = packet.Identification.MdProtocolVersion;
                    if (_game.ServerGameVersion != _game.Platform.GetGameVersion())
                    {
                        _game.ChatLog("[GAME] Different game versions");
                        string q = string.Format(invalidversionstr, _game.Platform.GetGameVersion(), _game.ServerGameVersion);
                        _game.InvalidVersionDrawMessage = q;
                        _game.InvalidVersionPacketIdentification = packet;
                    }
                    else
                    {
                        _game.ProcessServerIdentification(packet);
                    }
                    _game.ReceivedMapLength = 0;
                    break;
                }
            case Packet_ServerIdEnum.Ping:
                {
                    _game.SendPingReply();
                    _game.ServerInfo.ServerPing.Send(_game.Platform.TimeMillisecondsFromStart);
                    break;
                }
            case Packet_ServerIdEnum.PlayerPing:
                {
                    _game.ServerInfo.ServerPing.Receive(_game.Platform);
                    break;
                }
            case Packet_ServerIdEnum.LevelInitialize:
                {
                    _game.ChatLog("[GAME] Initialized map loading");
                    _game.ReceivedMapLength = 0;
                    _game.InvokeMapLoadingProgress(0, 0, _game.Language.Connecting());
                    break;
                }
            case Packet_ServerIdEnum.LevelDataChunk:
                {
                    _game.InvokeMapLoadingProgress(
                        packet.LevelDataChunk.PercentComplete,
                        _game.ReceivedMapLength,
                        packet.LevelDataChunk.Status);
                    break;
                }
            case Packet_ServerIdEnum.LevelFinalize:
                {
                    _game.ChatLog("[GAME] Finished map loading");
                    break;
                }
            case Packet_ServerIdEnum.SetBlock:
                {
                    _game.SetTileAndUpdate(packet.SetBlock.X, packet.SetBlock.Y, packet.SetBlock.Z, packet.SetBlock.BlockType);
                    break;
                }
            case Packet_ServerIdEnum.FillArea:
                {
                    int ax = packet.FillArea.X1;
                    int ay = packet.FillArea.Y1;
                    int az = packet.FillArea.Z1;
                    int bx = packet.FillArea.X2;
                    int by = packet.FillArea.Y2;
                    int bz = packet.FillArea.Z2;

                    int startx = Math.Min(ax, bx);
                    int endx = Math.Max(ax, bx);
                    int starty = Math.Min(ay, by);
                    int endy = Math.Max(ay, by);
                    int startz = Math.Min(az, bz);
                    int endz = Math.Max(az, bz);

                    int blockCount = packet.FillArea.BlockCount;
                    for (int x = startx; x <= endx; x++)
                    {
                        for (int y = starty; y <= endy; y++)
                        {
                            for (int z = startz; z <= endz; z++)
                            {
                                if (blockCount == 0) { return; }
                                _game.SetTileAndUpdate(x, y, z, packet.FillArea.BlockType);
                                blockCount--;
                            }
                        }
                    }
                    break;
                }
            case Packet_ServerIdEnum.FillAreaLimit:
                {
                    _game.FillAreaLimit = Math.Min(packet.FillAreaLimit.Limit, 100000);
                    break;
                }
            case Packet_ServerIdEnum.Freemove:
                {
                    _game.AllowFreeMove = packet.Freemove.IsEnabled != 0;
                    if (!_game.AllowFreeMove)
                    {
                        _game.Controls.freemove = false;
                        _game.Controls.noclip = false;
                        _game.MoveSpeed = _game.Basemovespeed;
                        _game.AddChatLine(_game.Language.MoveNormal());
                    }
                    break;
                }
            case Packet_ServerIdEnum.PlayerSpawnPosition:
                {
                    int x = packet.PlayerSpawnPosition.X;
                    int y = packet.PlayerSpawnPosition.Y;
                    int z = packet.PlayerSpawnPosition.Z;
                    _game.PlayerPositionSpawnX = x;
                    _game.PlayerPositionSpawnY = z;
                    _game.PlayerPositionSpawnZ = y;
                    _game.AddChatLine(string.Format(_game.Language.SpawnPositionSetTo(),
                        string.Format("{0},{1},{2}", x, y, z)));
                    break;
                }
            case Packet_ServerIdEnum.Message:
                {
                    _game.AddChatLine(packet.Message.Message);
                    _game.ChatLog(packet.Message.Message);
                    break;
                }
            case Packet_ServerIdEnum.DisconnectPlayer:
                {
                    _game.ChatLog(string.Format("[GAME] Disconnected by the server ({0})", packet.DisconnectPlayer.DisconnectReason));
                    if (_game.Platform.IsMousePointerLocked())
                    {
                        _game.Platform.ExitMousePointerLock();
                    }
                    _game.Platform.MessageBoxShowError(packet.DisconnectPlayer.DisconnectReason, "Disconnected from server");
                    _game.ExitToMainMenu();
                    break;
                }
            case Packet_ServerIdEnum.PlayerStats:
                {
                    _game.PlayerStats = packet.PlayerStats;
                    break;
                }
            case Packet_ServerIdEnum.FiniteInventory:
                {
                    if (packet.Inventory.Inventory != null)
                    {
                        _game.UseInventory(packet.Inventory.Inventory);
                    }
                    break;
                }
            case Packet_ServerIdEnum.Season:
                {
                    packet.Season.Hour -= 1;
                    if (packet.Season.Hour < 0) { packet.Season.Hour = 12 * Game.HourDetail; }
                    if (_game.NightLevels == null) { break; }
                    if (packet.Season.Hour >= _game.NightLevels.Length) { break; }
                    int sunlight = _game.NightLevels[packet.Season.Hour];
                    _game.SkySphereNight = sunlight < 8;
                    _game.SunMoonRenderer.day_length_in_seconds = 60 * 60 * 24 / packet.Season.DayNightCycleSpeedup;
                    int hour = packet.Season.Hour / Game.HourDetail;
                    if (_game.SunMoonRenderer.GetHour() != hour)
                    {
                        _game.SunMoonRenderer.SetHour(hour);
                    }
                    if (_game.Sunlight != sunlight)
                    {
                        _game.Sunlight = sunlight;
                        _game.RedrawAllBlocks();
                    }
                    break;
                }
            case Packet_ServerIdEnum.BlobInitialize:
                {
                    _game.BlobDownload = new MemoryStream();
                    _game.BlobDownloadName = packet.BlobInitialize.Name;
                    _game.BlobDownloadMd5 = packet.BlobInitialize.Md5;
                    break;
                }
            case Packet_ServerIdEnum.BlobPart:
                {
                    int length = packet.BlobPart.Data.Length;
                    _game.BlobDownload.Write(packet.BlobPart.Data, 0, length);
                    _game.ReceivedMapLength += length;
                    break;
                }
            case Packet_ServerIdEnum.BlobFinalize:
                {
                    byte[] downloaded = _game.BlobDownload.ToArray();
                    if (_game.BlobDownloadName != null)
                    {
                        _game.SetFile(_game.BlobDownloadName, _game.BlobDownloadMd5, downloaded, (int)_game.BlobDownload.Length);
                    }
                    _game.BlobDownload = null;
                    break;
                }
            case Packet_ServerIdEnum.Sound:
                {
                    _game.PlayAudio(packet.Sound.Name, packet.Sound.X, packet.Sound.Y, packet.Sound.Z);
                    break;
                }
            case Packet_ServerIdEnum.RemoveMonsters:
                {
                    for (int i = Game.entityMonsterIdStart; i < Game.entityMonsterIdStart + Game.entityMonsterIdCount; i++)
                    {
                        _game.Entities[i] = null;
                    }
                    break;
                }
            case Packet_ServerIdEnum.Translation:
                _game.Language.Override(packet.Translation.Lang, packet.Translation.Id, packet.Translation.Translation);
                break;
            case Packet_ServerIdEnum.BlockType:
                _game.NewBlockTypes[packet.BlockType.Id] = packet.BlockType.Blocktype;
                break;
            case Packet_ServerIdEnum.SunLevels:
                _game.NightLevels = packet.SunLevels.Sunlevels;
                break;
            case Packet_ServerIdEnum.LightLevels:
                for (int i = 0; i < packet.LightLevels.Lightlevels.Length; i++)
                {
                    _game.LightLevels[i] = _game.DecodeFixedPoint(packet.LightLevels.Lightlevels[i]);
                }
                break;
            case Packet_ServerIdEnum.Follow:
                _game.Follow = packet.Follow.Client;
                if (packet.Follow.Tpp != 0)
                {
                    _game.SetCamera(CameraType.Overhead);
                    _game.LocalOrientationX = MathF.PI;
                    _game.GuiStateBackToGame();
                }
                else
                {
                    _game.SetCamera(CameraType.Fpp);
                }
                break;
            case Packet_ServerIdEnum.Bullet:
                _game.EntityAddLocal(Game.CreateBulletEntity(
                    _game.DecodeFixedPoint(packet.Bullet.FromXFloat),
                    _game.DecodeFixedPoint(packet.Bullet.FromYFloat),
                    _game.DecodeFixedPoint(packet.Bullet.FromZFloat),
                    _game.DecodeFixedPoint(packet.Bullet.ToXFloat),
                    _game.DecodeFixedPoint(packet.Bullet.ToYFloat),
                    _game.DecodeFixedPoint(packet.Bullet.ToZFloat),
                    _game.DecodeFixedPoint(packet.Bullet.SpeedFloat)));
                break;
            case Packet_ServerIdEnum.Ammo:
                {
                    if (!_game.AmmoStarted)
                    {
                        _game.AmmoStarted = true;
                        for (int i = 0; i < packet.Ammo.TotalAmmo.Length; i++)
                        {
                            Packet_IntInt k = packet.Ammo.TotalAmmo[i];
                            _game.LoadedAmmo[k.Key_] = Math.Min(k.Value_, _game.BlockTypes[k.Key_].AmmoMagazine);
                        }
                    }

                    // ── Reuse TotalAmmo array instead of allocating a new one ────
                    // The old code did `game.TotalAmmo = new int[MAX_BLOCKTYPES]` every
                    // Ammo packet. We allocate once and clear-in-place thereafter.
                    if (_game.TotalAmmo == null)
                           _game.TotalAmmo = new int[GlobalVar.MAX_BLOCKTYPES];
                    else
                        Array.Clear(_game.TotalAmmo, 0, GlobalVar.MAX_BLOCKTYPES);

                    for (int i = 0; i < packet.Ammo.TotalAmmo.Length; i++)
                    {
                        _game.TotalAmmo[packet.Ammo.TotalAmmo[i].Key_] = packet.Ammo.TotalAmmo[i].Value_;
                    }
                    break;
                }
            case Packet_ServerIdEnum.Explosion:
                {
                    Entity entity = new()
                    {
                        expires = new Expires { timeLeft = _game.DecodeFixedPoint(packet.Explosion.TimeFloat) },
                        push = packet.Explosion
                    };
                    _game.EntityAddLocal(entity);
                    break;
                }
            case Packet_ServerIdEnum.Projectile:
                {
                    Entity entity = new();
                    Sprite sprite = new()
                    {
                        image = "ChemicalGreen.png",
                        size = 14,
                        animationcount = 0,
                        positionX = _game.DecodeFixedPoint(packet.Projectile.FromXFloat),
                        positionY = _game.DecodeFixedPoint(packet.Projectile.FromYFloat),
                        positionZ = _game.DecodeFixedPoint(packet.Projectile.FromZFloat)
                    };
                    entity.sprite = sprite;
                    Grenade grenade = new()
                    {
                        velocityX = _game.DecodeFixedPoint(packet.Projectile.VelocityXFloat),
                        velocityY = _game.DecodeFixedPoint(packet.Projectile.VelocityYFloat),
                        velocityZ = _game.DecodeFixedPoint(packet.Projectile.VelocityZFloat),
                        block = packet.Projectile.BlockId,
                        sourcePlayer = packet.Projectile.SourcePlayerID
                    };
                    entity.grenade = grenade;
                    entity.expires = Expires.Create(_game.DecodeFixedPoint(packet.Projectile.ExplodesAfterFloat));
                    _game.EntityAddLocal(entity);
                    break;
                }
            case Packet_ServerIdEnum.BlockTypes:
                {
                    _game.BlockTypes = _game.NewBlockTypes;
                    _game.NewBlockTypes = new Packet_BlockType[GlobalVar.MAX_BLOCKTYPES];

                    string[] textureInAtlasIds = new string[1024];
                    int textureInAtlasIdsCount = 1024;
                    int lastTextureId = 0;

                    // ── Use static scratch buffer instead of new string[7] per block ──
                    // The old code allocated a fresh string[7] inside this loop for
                    // every block type — up to 1,024 temporary arrays from one packet.
                    // s_textureIdScratch is a static field reused each iteration.
                    string[] scratch = s_textureIdScratch;

                    for (int i = 0; i < GlobalVar.MAX_BLOCKTYPES; i++)
                    {
                        if (_game.BlockTypes[i] != null)
                        {
                            scratch[0] = _game.BlockTypes[i].TextureIdLeft;
                            scratch[1] = _game.BlockTypes[i].TextureIdRight;
                            scratch[2] = _game.BlockTypes[i].TextureIdFront;
                            scratch[3] = _game.BlockTypes[i].TextureIdBack;
                            scratch[4] = _game.BlockTypes[i].TextureIdTop;
                            scratch[5] = _game.BlockTypes[i].TextureIdBottom;
                            scratch[6] = _game.BlockTypes[i].TextureIdForInventory;

                            for (int k = 0; k < 7; k++)
                            {
                                if (!Contains(textureInAtlasIds, textureInAtlasIdsCount, scratch[k]))
                                {
                                    textureInAtlasIds[lastTextureId++] = scratch[k];
                                }
                            }
                        }
                    }

                    _game.BlockRegistry.UseBlockTypes(_game.Platform, _game.BlockTypes, GlobalVar.MAX_BLOCKTYPES);
                    for (int i = 0; i < GlobalVar.MAX_BLOCKTYPES; i++)
                    {
                        Packet_BlockType b = _game.BlockTypes[i];
                        if (b == null) { continue; }
                        if (textureInAtlasIds != null)
                        {
                            _game.TextureId[i][0] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdTop);
                            _game.TextureId[i][1] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdBottom);
                            _game.TextureId[i][2] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdFront);
                            _game.TextureId[i][3] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdBack);
                            _game.TextureId[i][4] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdLeft);
                            _game.TextureId[i][5] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdRight);
                            _game.TextureIdForInventory[i] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdForInventory);
                        }
                    }
                    _game.UseTerrainTextures(textureInAtlasIds, textureInAtlasIdsCount);
                    _game.HandRedraw = true;
                    _game.RedrawAllBlocks();
                    break;
                }
            case Packet_ServerIdEnum.ServerRedirect:
                _game.ChatLog("[GAME] Received server redirect");
                _game.SendLeave(PacketLeaveReason.Leave);
                _game.ExitAndSwitchServer(packet.Redirect);
                break;
        }
    }

    private static bool Contains(string[] arr, int arrLength, string value)
        => IndexOf(arr, arrLength, value) != -1;

    private static int IndexOf(string[] arr, int arrLength, string value)
    {
        for (int i = 0; i < arrLength; i++)
        {
            if (string.Equals(arr[i], value))
                return i;
        }
        return -1;
    }
}