public class CuboidRenderer
{
    //Maps description of position of 6 faces
    //of a single cuboid in texture file to UV coordinates (in pixels)
    //(one RectangleF in texture file for each 3d face of cuboid).
    //Arguments:
    // Size (in pixels) in 2d cuboid net.
    // Start position of 2d cuboid net in texture file.
    public static RectangleF[] CuboidNet(float tsizex, float tsizey, float tsizez, float tstartx, float tstarty)
    {
        RectangleF[] coords = new RectangleF[6];
        {
            coords[0] = new RectangleF(tsizez + tstartx, tsizez + tstarty, tsizex, tsizey);//front
            coords[1] = new RectangleF(2 * tsizez + tsizex + tstartx, tsizez + tstarty, tsizex, tsizey);//back
            coords[2] = new RectangleF(tstartx, tsizez + tstarty, tsizez, tsizey);//right
            coords[3] = new RectangleF(tsizez + tsizex + tstartx, tsizez + tstarty, tsizez, tsizey);//left
            coords[4] = new RectangleF(tsizez + tstartx, tstarty, tsizex, tsizez);//top
            coords[5] = new RectangleF(tsizez + tsizex + tstartx, tstarty, tsizex, tsizez);//bottom
        }
        return coords;
    }

    //Divides CuboidNet() result by texture size, to get relative coordinates. (0-1, not 0-32 pixels).
    public static void CuboidNetNormalize(RectangleF[] coords, float texturewidth, float textureheight)
    {
        float AtiArtifactFix = 0.15f;
        for (int i = 0; i < 6; i++)
        {
            float x = ((coords[i].X + AtiArtifactFix) / texturewidth);
            float y = ((coords[i].Y + AtiArtifactFix) / textureheight);
            float w = ((coords[i].X + coords[i].Width - AtiArtifactFix) / texturewidth) - x;
            float h = ((coords[i].Y + coords[i].Height - AtiArtifactFix) / textureheight) - y;
            coords[i] = new RectangleF(x, y, w, h);
        }
    }
    public static void DrawCuboid(Game game, float posX, float posY, float posZ,
        float sizeX, float sizeY, float sizeZ,
        RectangleF[] texturecoords, float light)
    {
        ModelData data = new()
        {
            xyz = new float[4 * 6 * 3],
            uv = new float[4 * 6 * 2],
            rgba = new byte[4 * 6 * 4]
        };
        int light255 = game.platform.FloatToInt(light * 255);
        int color = Game.ColorFromArgb(255, light255, light255, light255);

        RectangleF rect;

        //front
        rect = texturecoords[0];
        AddVertex(data, posX, posY, posZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX, posY + sizeY, posZ, rect.X, rect.Y, color);

        //back
        rect = texturecoords[1];
        AddVertex(data, posX + sizeX, posY, posZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X, rect.Y, color);

        //left
        rect = texturecoords[2];
        AddVertex(data, posX + sizeX, posY, posZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY, posZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X, rect.Y, color);

        //right
        rect = texturecoords[3];
        AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX, posY, posZ + sizeZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);

        //top
        rect = texturecoords[4];
        AddVertex(data, posX, posY + sizeY, posZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X, rect.Y, color);

        //bottom
        rect = texturecoords[5];
        AddVertex(data, posX, posY, posZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX + sizeX, posY, posZ, rect.X, rect.Y, color);

        data.indices = new int[6 * 6];
        for (int i = 0; i < 6; i++)
        {
            data.indices[i * 6 + 0] = i * 4 + 3;
            data.indices[i * 6 + 1] = i * 4 + 2;
            data.indices[i * 6 + 2] = i * 4 + 0;
            data.indices[i * 6 + 3] = i * 4 + 2;
            data.indices[i * 6 + 4] = i * 4 + 1;
            data.indices[i * 6 + 5] = i * 4 + 0;
        }
        data.indicesCount = 36;

        game.platform.GlDisableCullFace();
        game.DrawModelData(data);
        game.platform.GlEnableCullFace();
    }

    public static void AddVertex(ModelData model, float x, float y, float z, float u, float v, int color)
    {
        model.xyz[model.GetXyzCount() + 0] = x;
        model.xyz[model.GetXyzCount() + 1] = y;
        model.xyz[model.GetXyzCount() + 2] = z;
        model.uv[model.GetUvCount() + 0] = u;
        model.uv[model.GetUvCount() + 1] = v;
        model.rgba[model.GetRgbaCount() + 0] = Game.IntToByte(Game.ColorR(color));
        model.rgba[model.GetRgbaCount() + 1] = Game.IntToByte(Game.ColorG(color));
        model.rgba[model.GetRgbaCount() + 2] = Game.IntToByte(Game.ColorB(color));
        model.rgba[model.GetRgbaCount() + 3] = Game.IntToByte(Game.ColorA(color));
        model.verticesCount++;
    }

    public static void DrawCuboid2(Game game, float posX, float posY, float posZ,
        float sizeX, float sizeY, float sizeZ,
        RectangleF[] texturecoords, float light)
    {
        ModelData data = new()
        {
            xyz = new float[4 * 6 * 3],
            uv = new float[4 * 6 * 2],
            rgba = new byte[4 * 6 * 4]
        };
        int light255 = game.platform.FloatToInt(light * 255);
        int color = Game.ColorFromArgb(255, light255, light255, light255);

        RectangleF rect;

        //right
        rect = texturecoords[2];
        AddVertex(data, posX, posY, posZ, rect.X, rect.Bottom , color);
        AddVertex(data, posX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX, posY + sizeY, posZ, rect.X, rect.Y, color);

        //left
        rect = texturecoords[3];
        AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY, posZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X, rect.Y, color);

        //back
        rect = texturecoords[1];
        AddVertex(data, posX + sizeX, posY, posZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY, posZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X, rect.Y, color);

        //front
        rect = texturecoords[0];
        AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX, posY, posZ + sizeZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);

        //top
        rect = texturecoords[4];
        AddVertex(data, posX, posY + sizeY, posZ, rect.X, rect.Y, color);
        AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X + rect.Width, rect.Y, color);

        //bottom
        rect = texturecoords[5];
        AddVertex(data, posX, posY, posZ, rect.X, rect.Y, color);
        AddVertex(data, posX, posY, posZ + sizeZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY, posZ, rect.X + rect.Width, rect.Y, color);

        data.indices = new int[6 * 6];
        for (int i = 0; i < 6; i++)
        {
            data.indices[i * 6 + 0] = i * 4 + 3;
            data.indices[i * 6 + 1] = i * 4 + 2;
            data.indices[i * 6 + 2] = i * 4 + 0;
            data.indices[i * 6 + 3] = i * 4 + 2;
            data.indices[i * 6 + 4] = i * 4 + 1;
            data.indices[i * 6 + 5] = i * 4 + 0;
        }
        data.indicesCount = 36;



        game.platform.GlDisableCullFace();
        game.DrawModelData(data);
        game.platform.GlEnableCullFace();
    }
}
