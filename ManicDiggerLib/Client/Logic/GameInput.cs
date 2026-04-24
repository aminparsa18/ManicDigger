using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public partial class Game
{
    // -------------------------------------------------------------------------
    // Mouse button events
    // -------------------------------------------------------------------------

    internal void MouseDown(MouseEventArgs args)
    {
        int btn = args.GetButton();

        if (btn == (int)MouseButton.Left) 
        { 
            mouseLeft = true; mouseleftclick = true;
        }
        if (btn == (int)MouseButton.Middle) 
            mouseMiddle = true;
        if (btn == (int)MouseButton.Right) 
        { 
            mouseRight = true; mouserightclick = true; 
        }

        for (int i = 0; i < clientmods.Count; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnMouseDown(this, args);
        }

        if (mousePointerLockShouldBe)
        {
            Platform.RequestMousePointerLock();
            mouseDeltaX = 0;
            mouseDeltaY = 0;
        }

        InvalidVersionAllow();
    }

    internal void MouseUp(MouseEventArgs args)
    {
        int btn = args.GetButton();

        if (btn == (int)MouseButton.Left) { mouseLeft = false; mouseleftdeclick = true; }
        if (btn == (int)MouseButton.Middle) mouseMiddle = false;
        if (btn == (int)MouseButton.Right) { mouseRight = false; mouserightdeclick = true; }

        for (int i = 0; i < clientmods.Count; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnMouseUp(this, args);
        }
    }

    // -------------------------------------------------------------------------
    // Mouse move / wheel
    // -------------------------------------------------------------------------

    public void MouseMove(MouseEventArgs e)
    {
        if (!e.GetEmulated() || e.GetForceUsage())
        {
            // Set position only for real MouseMove events.
            mouseCurrentX = e.GetX();
            mouseCurrentY = e.GetY();
        }
        if (e.GetEmulated() || e.GetForceUsage())
        {
            // Accumulate delta only from emulated events (actual events negate previous ones).
            mouseDeltaX += e.GetMovementX();
            mouseDeltaY += e.GetMovementY();
        }

        for (int i = 0; i < clientmods.Count; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnMouseMove(this, e);
        }
    }

    internal void MouseWheelChanged(MouseWheelEventArgs e)
    {
        float delta = e.OffsetY;

        if (keyboardState[GetKey(Keys.LeftShift)])
        {
            if (cameratype == CameraType.Overhead)
                overheadcameradistance = Math.Clamp(overheadcameradistance - delta, TPP_CAMERA_DISTANCE_MIN, TPP_CAMERA_DISTANCE_MAX);

            if (cameratype == CameraType.Tpp)
                tppcameradistance = Math.Clamp(tppcameradistance - delta, TPP_CAMERA_DISTANCE_MIN, TPP_CAMERA_DISTANCE_MAX);
        }

        for (int i = 0; i < clientmods.Count; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnMouseWheelChanged(this, e);
        }
    }

    internal void UpdateMouseViewportControl(float dt)
    {
        if (mouseSmoothing)
        {
            const float smoothing1 = 0.85f;
            const float smoothing2 = 0.8f;
            float scale = smoothing2 / (300f / 75);
            mouseSmoothingVelX = (mouseSmoothingVelX + mouseDeltaX * scale) * smoothing1;
            mouseSmoothingVelY = (mouseSmoothingVelY + mouseDeltaY * scale) * smoothing1;
        }
        else
        {
            mouseSmoothingVelX = mouseDeltaX;
            mouseSmoothingVelY = mouseDeltaY;
        }

        if (GuiState == GuiState.Normal && enableCameraControl && Platform.Focused())
        {
            if (!overheadcamera && Platform.IsMousePointerLocked())
            {
                float rotScale = rotationspeed / 75f;
                Player.position.roty += mouseSmoothingVelX * rotScale;
                Player.position.rotx += mouseSmoothingVelY * rotScale;
                Player.position.rotx = Math.Clamp(Player.position.rotx,
                    MathF.PI / 2 + (15 / 1000),
                    MathF.PI / 2 + MathF.PI - (15 / 1000));
            }

            if (!overheadcamera)
            {
                float touchScale = constRotationSpeed * (1f / 75);
                Player.position.rotx += touchOrientationDy * touchScale;
                Player.position.roty += touchOrientationDx * touchScale;
                touchOrientationDx = 0;
                touchOrientationDy = 0;
            }

            if (cameratype == CameraType.Overhead && (mouseMiddle || mouseRight))
            {
                overheadcameraK.TurnLeft(mouseDeltaX / 70);
                overheadcameraK.TurnUp(mouseDeltaY / 3);
            }
        }

        mouseDeltaX = 0;
        mouseDeltaY = 0;
    }

    // -------------------------------------------------------------------------
    // Free mouse / pointer lock
    // -------------------------------------------------------------------------

    public bool GetFreeMouse() => overheadcamera || !Platform.IsMousePointerLocked();

    public void SetFreeMouse(bool value)
    {
        mousePointerLockShouldBe = !value;
        if (value)
            Platform.ExitMousePointerLock();
        else
            Platform.RequestMousePointerLock();
    }

    // -------------------------------------------------------------------------
    // Touch events
    // -------------------------------------------------------------------------

    public void OnTouchStart(TouchEventArgs e)
    {
        InvalidVersionAllow();
        mouseCurrentX = e.GetX();
        mouseCurrentY = e.GetY();
        mouseleftclick = true;

        for (int i = 0; i < clientmods.Count; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnTouchStart(this, e);
            if (e.GetHandled()) return;
        }
    }

    public void OnTouchMove(TouchEventArgs e)
    {
        for (int i = 0; i < clientmods.Count; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnTouchMove(this, e);
            if (e.GetHandled()) return;
        }
    }

    public void OnTouchEnd(TouchEventArgs e)
    {
        mouseCurrentX = 0;
        mouseCurrentY = 0;

        for (int i = 0; i < clientmods.Count; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnTouchEnd(this, e);
            if (e.GetHandled()) return;
        }
    }

    public static void OnBackPressed() { }

    // -------------------------------------------------------------------------
    // Keyboard events
    // -------------------------------------------------------------------------

    internal void KeyUp(KeyEventArgs eKey)
    {
        keyboardStateRaw[eKey.KeyChar] = false;

        for (int i = 0; i < clientmods.Count; i++)
        {
            clientmods[i].OnKeyUp(this, eKey);
            if (eKey.Handled) return;
        }

        keyboardState[eKey.KeyChar] = false;

        if (eKey.KeyChar == GetKey(Keys.LeftShift) || eKey.KeyChar == GetKey(Keys.RightShift))
            IsShiftPressed = false;
    }

    internal void KeyPress(KeyPressEventArgs eKeyChar)
    {
        for (int i = 0; i < clientmods.Count; i++)
        {
            if (clientmods[i] == null) continue;
            clientmods[i].OnKeyPress(this, eKeyChar);
            if (eKeyChar.Handled) return;
        }
    }

    internal void KeyDown(KeyEventArgs eKey)
    {
        keyboardStateRaw[eKey.KeyChar] = true;

        if (GuiState != GuiState.MapLoading)
        {
            for (int i = 0; i < clientmods.Count; i++)
            {
                clientmods[i].OnKeyDown(this, eKey);
                if (eKey.Handled) return;
            }
        }

        keyboardState[eKey.KeyChar] = true;
        InvalidVersionAllow();

        if (eKey.KeyChar == GetKey(Keys.LeftShift) || eKey.KeyChar == GetKey(Keys.RightShift))
            IsShiftPressed = true;

        // F6 outside of Normal state: reconnect if lagging or map loading.
        if (eKey.KeyChar == GetKey(Keys.F6))
        {
            float lagSeconds = (Platform.TimeMillisecondsFromStart - LastReceivedMilliseconds) / 1000;
            if (lagSeconds >= DISCONNECTED_ICON_AFTER_SECONDS || GuiState == GuiState.MapLoading)
                Reconnect();
        }

        if (GuiState == GuiState.Normal)
            KeyDownNormal(eKey.KeyChar);

        else if (GuiState == GuiState.Inventory)
        {
            if (eKey.KeyChar == GetKey(Keys.B) || eKey.KeyChar == GetKey(Keys.Escape))
                GuiStateBackToGame();
            return;
        }

        else if (GuiState == GuiState.MapLoading)
        {
            if (eKey.KeyChar == GetKey(Keys.Escape))
                ExitToMainMenu();
        }

        else if (GuiState == GuiState.CraftingRecipes)
        {
            if (eKey.KeyChar == GetKey(Keys.Escape))
                GuiStateBackToGame();
        }

        if (GuiState == GuiState.Normal)
        {
            if (eKey.KeyChar == GetKey(Keys.Escape))
            {
                EscapeMenuStart();
                return;
            }
        }
    }

    private void KeyDownNormal(int eKey)
    {
        const string strFreemoveNotAllowed = "You are not allowed to enable freemove.";

        if (eKey == GetKey(Keys.F1))
        {
            if (!AllowFreeMove) { AddChatLine(strFreemoveNotAllowed); return; }
            MoveSpeed = Basemovespeed * 1;
            AddChatLine("Move speed: 1x.");
        }
        if (eKey == GetKey(Keys.F2))
        {
            if (!AllowFreeMove) { AddChatLine(strFreemoveNotAllowed); return; }
            MoveSpeed = Basemovespeed * 10;
            AddChatLine(string.Format(Language.MoveSpeed(), "10"));
        }
        if (eKey == GetKey(Keys.F3))
        {
            if (!AllowFreeMove) { AddChatLine(strFreemoveNotAllowed); return; }
            stopPlayerMove = true;
            if (!Controls.freemove)
            {
                Controls.freemove = true;
                AddChatLine(Language.MoveFree());
            }
            else if (Controls.freemove && !Controls.noclip)
            {
                Controls.noclip = true;
                AddChatLine(Language.MoveFreeNoclip());
            }
            else
            {
                Controls.freemove = false;
                Controls.noclip = false;
                AddChatLine(Language.MoveNormal());
            }
        }
        if (eKey == GetKey(Keys.I))
            drawblockinfo = !drawblockinfo;

        int playerx = (int)Player.position.x;
        int playery = (int)Player.position.z;
        if (playerx >= 0 && playerx < VoxelMap.MapSizeX && playery >= 0 && playery < VoxelMap.MapSizeY)
            performanceinfo["height"] = string.Format("height:{0}", Heightmap.GetBlock(playerx, playery).ToString());

        if (eKey == GetKey(Keys.F5))
            CameraChange();

        if (eKey == GetKey(Keys.Equal))
        {
            if (cameratype == CameraType.Overhead) overheadcameradistance -= 1;
            else if (cameratype == CameraType.Tpp) tppcameradistance -= 1;
        }
        if (eKey == GetKey(Keys.Minus) || eKey == GetKey(Keys.KeyPadSubtract))
        {
            if (cameratype == CameraType.Overhead) overheadcameradistance += 1;
            else if (cameratype == CameraType.Tpp) tppcameradistance += 1;
        }

        overheadcameradistance = Math.Clamp(overheadcameradistance, TPP_CAMERA_DISTANCE_MIN, TPP_CAMERA_DISTANCE_MAX);
        tppcameradistance = Math.Clamp(tppcameradistance, TPP_CAMERA_DISTANCE_MIN, TPP_CAMERA_DISTANCE_MAX);

        if (eKey == GetKey(Keys.F6))
            RedrawAllBlocks();

        if (eKey == (int)Keys.F8)
        {
            ToggleVsync();
            if (ENABLE_LAG == 0) AddChatLine(Language.FrameRateVsync());
            if (ENABLE_LAG == 1) AddChatLine(Language.FrameRateUnlimited());
            if (ENABLE_LAG == 2) AddChatLine(Language.FrameRateLagSimulation());
        }

        if (eKey == GetKey(Keys.Tab))
            SendPacketClient(ClientPackets.SpecialKeyTabPlayerList());

        if (eKey == GetKey(Keys.E))
            KeyDownUse();

        if (eKey == GetKey(Keys.O))
            Respawn();

        if (eKey == GetKey(Keys.L))
            SendPacketClient(ClientPackets.SpecialKeySelectTeam());

        if (eKey == GetKey(Keys.P))
        {
            SendPacketClient(ClientPackets.SpecialKeySetSpawn());
            PlayerPositionSpawnX = Player.position.x;
            PlayerPositionSpawnY = Player.position.y;
            PlayerPositionSpawnZ = Player.position.z;
            Player.position.x = (int)Player.position.x + 1f / 2;
            Player.position.z = (int)Player.position.z + 1f / 2;
        }

        if (eKey == GetKey(Keys.F))
        {
            ToggleFog();
            AddChatLine(string.Format(Language.FogDistance(), ((int)Config3d.ViewDistance).ToString()));
            OnResize();
        }

        if (eKey == GetKey(Keys.B))
        {
            ShowInventory();
            return;
        }

        HandleMaterialKeys(eKey);
    }

    private void KeyDownUse()
    {
        if (currentAttackedBlock != null)
        {
            int posX = currentAttackedBlock.Value.X;
            int posY = currentAttackedBlock.Value.Y;
            int posZ = currentAttackedBlock.Value.Z;
            int blocktype = VoxelMap.GetBlock(posX, posY, posZ);

            if (IsUsableBlock(blocktype))
            {
                if (BlockRegistry.IsRailTile(blocktype))
                {
                    Player.position.x = posX + (1f / 2);
                    Player.position.y = posZ + 1;
                    Player.position.z = posY + (1f / 2);
                    Controls.freemove = false;
                }
                else
                {
                    SendSetBlock(posX, posY, posZ, PacketBlockSetMode.Use, 0, ActiveMaterial);
                }
            }
        }

        if (currentlyAttackedEntity != -1 && Entities[currentlyAttackedEntity].usable)
        {
            OnUseEntityArgs args = new() { entityId = currentlyAttackedEntity };
            for (int i = 0; i < clientmods.Count; i++)
            {
                if (clientmods[i] == null) continue;
                clientmods[i].OnUseEntity(this, args);
            }
            SendPacketClient(ClientPackets.UseEntity(currentlyAttackedEntity));
        }
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
}