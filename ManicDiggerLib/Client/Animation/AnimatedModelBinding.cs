/// <summary>
/// Binds an <see cref="AnimatedModel"/> to the <see cref="TableSerializer"/>,
/// mapping tab-separated section data to and from model fields.
/// </summary>
public class AnimatedModelBinding : ITableBinding
{
    /// <summary>Platform utilities for string and float conversion.</summary>
    internal GamePlatform p;

    /// <summary>The model being populated or read.</summary>
    internal AnimatedModel m;

    /// <inheritdoc/>
    public void Set(string table, int index, string column, string value)
    {
        if (table == "nodes")
        {
            if (index >= m.nodes.Count)
            {
                m.nodes.Add(new Node());
            }
            if (m.nodes[index] == null) { m.nodes[index] = new Node(); }
            Node k = m.nodes[index];
            if (column == "name") { k.Name = value; }
            if (column == "paren") { k.ParentName = value; }
            if (column == "x") { k.PosX = FloatParse(value); }
            if (column == "y") { k.PosY = FloatParse(value); }
            if (column == "z") { k.PosZ = FloatParse(value); }
            if (column == "rotx") { k.RotateX = FloatParse(value); }
            if (column == "roty") { k.RotateY = FloatParse(value); }
            if (column == "rotz") { k.RotateZ = FloatParse(value); }
            if (column == "sizex") { k.SizeX = FloatParse(value); }
            if (column == "sizey") { k.SizeY = FloatParse(value); }
            if (column == "sizez") { k.SizeZ = FloatParse(value); }
            if (column == "u") { k.U = FloatParse(value); }
            if (column == "v") { k.V = FloatParse(value); }
            if (column == "pivx") { k.PivotX = FloatParse(value); }
            if (column == "pivy") { k.PivotY = FloatParse(value); }
            if (column == "pivz") { k.PivotZ = FloatParse(value); }
            if (column == "scalx") { k.ScaleX = FloatParse(value); }
            if (column == "scaly") { k.ScaleY = FloatParse(value); }
            if (column == "scalz") { k.ScaleZ = FloatParse(value); }
            if (column == "head") { k.Head = FloatParse(value); }
        }
        if (table == "keyframes")
        {
            while (m.Keyframes.Count <= index) { m.Keyframes.Add(new Keyframe()); }
            Keyframe k = m.Keyframes[index];
            if (column == "anim") { k.AnimationName = value; }
            if (column == "node") { k.NodeName = value; }
            if (column == "frame") { k.Frame = IntParse(value); }
            if (column == "type") { k.Type = KeyframeTypeExtensions.FromSerializedName(value); }
            if (column == "x") { k.X = FloatParse(value); }
            if (column == "y") { k.Y = FloatParse(value); }
            if (column == "z") { k.Z = FloatParse(value); }
        }
        if (table == "animations")
        {
            while (m.Animations.Count <= index) { m.Animations.Add(new Animation()); }
            Animation k = m.Animations[index];
            if (column == "name") { k.Name = value; }
            if (column == "len") { k.Length = IntParse(value); }
        }
        if (table == "global")
        {
            if (column == "texw") { m.Global.TexW = IntParse(value); }
            if (column == "texh") { m.Global.TexH = IntParse(value); }
        }
    }

    /// <inheritdoc/>
    public void Get(string table, int index, Dictionary<string, string> items)
    {
        if (table == "nodes")
        {
            Node k = m.nodes[index];
            items["name"] = k.Name;
            items["paren"] = k.ParentName;
            items["x"] = p.FloatToString(k.PosX);
            items["y"] = p.FloatToString(k.PosY);
            items["z"] = p.FloatToString(k.PosZ);
            items["rotx"] = p.FloatToString(k.RotateX);
            items["roty"] = p.FloatToString(k.RotateY);
            items["rotz"] = p.FloatToString(k.RotateZ);
            items["sizex"] = p.FloatToString(k.SizeX);
            items["sizey"] = p.FloatToString(k.SizeY);
            items["sizez"] = p.FloatToString(k.SizeZ);
            items["u"] = p.FloatToString(k.U);
            items["v"] = p.FloatToString(k.V);
            items["pivx"] = p.FloatToString(k.PivotX);
            items["pivy"] = p.FloatToString(k.PivotY);
            items["pivz"] = p.FloatToString(k.PivotZ);
            items["scalx"] = p.FloatToString(k.ScaleX);
            items["scaly"] = p.FloatToString(k.ScaleY);
            items["scalz"] = p.FloatToString(k.ScaleZ);
            items["head"] = p.FloatToString(k.Head);
        }
        if (table == "keyframes")
        {
            Keyframe k = m.Keyframes[index];
            items["anim"] = k.AnimationName;
            items["node"] = k.NodeName;
            items["frame"] = p.IntToString(k.Frame);
            items["type"] = k.Type.ToSerializedName(); // was k.Type.ToString() — would break round-trip
            items["x"] = p.FloatToString(k.X);
            items["y"] = p.FloatToString(k.Y);
            items["z"] = p.FloatToString(k.Z);
        }
        if (table == "animations")
        {
            Animation k = m.Animations[index];
            items["name"] = k.Name;
            items["len"] = p.IntToString(k.Length);
        }
        if (table == "global")
        {
            items["texw"] = p.IntToString(m.Global.TexW);
            items["texh"] = p.IntToString(m.Global.TexH);
        }
    }

    /// <inheritdoc/>
    public void GetTables(string[] names, int[] counts)
    {
        names[0] = "nodes"; counts[0] = m.nodes.Count;
        names[1] = "keyframes"; counts[1] = m.Keyframes.Count;
        names[2] = "animations"; counts[2] = m.Animations.Count;
        names[3] = "global"; counts[3] = 1;
    }

    /// <summary>Parses a string to int via float intermediary, as required by <see cref="GamePlatform"/>.</summary>
    private int IntParse(string s) => p.FloatToInt(FloatParse(s));

    /// <summary>Parses a string to float using <see cref="GamePlatform"/>, returning 0 on failure.</summary>
    private float FloatParse(string s) { p.FloatTryParse(s, out float ret); return ret; }
}