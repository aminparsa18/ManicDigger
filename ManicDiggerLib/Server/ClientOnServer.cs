using ManicDigger;
using OpenTK.Mathematics;

public class ClientOnServer
{
    public ClientOnServer()
    {
        float one = 1;
        entity = new ServerEntity
        {
            DrawName = new ServerEntityDrawName
            {
                ClientAutoComplete = true
            },
            Position = new ServerEntityPositionAndOrientation
            {
                Pitch = 2 * 255 / 4
            },
            DrawModel = new ServerEntityAnimatedModel
            {
                DownloadSkin = true
            }
        };
        Id = -1;
        state = ClientStateOnServer.Connecting;
        queryClient = true;
        received = new List<byte>();
        Ping = new Ping();
        playername = Server.invalidplayername;
        Model = "player.txt";
        chunksseenTime = new Dictionary<int, int>();
        heightmapchunksseen = new Dictionary<Vector2i, int>();
        IsInventoryDirty = true;
        IsPlayerStatsDirty = true;
        FillLimit = 500;
        privileges = new List<string>();
        displayColor = "&f";
        EyeHeight = one * 15 / 10;
        ModelHeight = one * 17 / 10;
        WindowSize = new int[] { 800, 600 };
        playersDirty = new bool[128];
        for (int i = 0; i < 128; i++)
        {
            playersDirty[i] = true;
        }
        spawnedEntities = new ServerEntityId[64];
        spawnedEntitiesCount = 64;
        updateEntity = new bool[spawnedEntitiesCount];
    }
    internal int Id;
    internal int state; // ClientStateOnServer
    internal bool queryClient;
    internal NetServer mainSocket;
    internal NetConnection socket;
    internal List<byte> received;
    internal Ping Ping;
    internal float LastPing;
    internal string playername { get { return entity.DrawName.Name; } set { entity.DrawName.Name = value; } }
    internal int PositionMul32GlX { get { return (int)(entity.Position.X * 32); } set { entity.Position.X = (float)value / 32; } }
    internal int PositionMul32GlY { get { return (int)(entity.Position.Y * 32); } set { entity.Position.Y = (float)value / 32; } }
    internal int PositionMul32GlZ { get { return (int)(entity.Position.Z * 32); } set { entity.Position.Z = (float)value / 32; } }
    internal int positionheading { get { return entity.Position.Heading; } set { entity.Position.Heading = (byte)value; } }
    internal int positionpitch { get { return entity.Position.Pitch; } set { entity.Position.Pitch = (byte)value; } }
    internal byte stance { get { return entity.Position.Stance; } set { entity.Position.Stance = value; } }
    internal string Model { get { return entity.DrawModel.Model; } set { entity.DrawModel.Model = value; } }
    internal string Texture { get { return entity.DrawModel.Texture; } set { entity.DrawModel.Texture = value; } }
    internal Dictionary<int, int> chunksseenTime;
    internal bool[] chunksseen;
    internal Dictionary<Vector2i, int> heightmapchunksseen;
    internal Timer notifyMapTimer;
    internal bool IsInventoryDirty;
    internal bool IsPlayerStatsDirty;
    internal int FillLimit;
    //internal List<byte[]> blobstosend = new List<byte[]>();
    internal ManicDigger.Group clientGroup;
    internal bool IsBot;
    public void AssignGroup(ManicDigger.Group newGroup)
    {
        this.clientGroup = newGroup;
        this.privileges.Clear();
        this.privileges.AddRange(newGroup.GroupPrivileges);
        this.color = newGroup.GroupColorString();
    }
    internal List<string> privileges;
    internal string color;
    internal string displayColor { get { return entity.DrawName.Color; } set { entity.DrawName.Color = value; } }
    public string ColoredPlayername(string subsequentColor)
    {
        return this.color + this.playername + subsequentColor;
    }
    internal Timer notifyMonstersTimer;
    internal IScriptInterpreter Interpreter;
    internal ScriptConsole Console;

    public override string ToString()
    {
        string ip = "";
        if (this.socket != null)
        {
            ip = this.socket.RemoteEndPoint().AddressToString();
        }
        // Format: Playername:Group:Privileges IP
        return string.Format("{0}:{1}:{2} {3}", this.playername, this.clientGroup.Name,
            ServerClientMisc.PrivilegesString(this.privileges), ip);
    }
    internal float EyeHeight { get { return entity.DrawModel.EyeHeight; } set { entity.DrawModel.EyeHeight = value; } }
    internal float ModelHeight { get { return entity.DrawModel.ModelHeight; } set { entity.DrawModel.ModelHeight = value; } }
    internal int ActiveMaterialSlot;
    internal bool IsSpectator;
    internal bool usingFill;
    internal int[] WindowSize;
    internal float notifyPlayerPositionsAccum;
    internal bool[] playersDirty;
    internal ServerEntity entity;
    internal ServerEntityPositionAndOrientation positionOverride;
    internal float notifyEntitiesAccum;
    internal ServerEntityId[] spawnedEntities;
    internal int spawnedEntitiesCount;
    internal ServerEntityId editingSign;
    internal bool[] updateEntity;
}
