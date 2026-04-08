public partial class Game
{
    // -------------------------------------------------------------------------
    // Block geometry
    // -------------------------------------------------------------------------

    public int Blockheight(int x, int y, int z_)
    {
        for (int z = z_; z >= 0; z--)
        {
            if (map.GetBlock(x, y, z) != 0)
                return z + 1;
        }
        return 0;
    }

    public float Getblockheight(int x, int y, int z)
    {
        float RailHeight = one * 3 / 10;
        if (!map.IsValidPos(x, y, z))
            return 1;

        int block = map.GetBlock(x, y, z);
        if (blocktypes[block].Rail != 0)
            return RailHeight;
        if (blocktypes[block].DrawType == Packet_DrawTypeEnum.HalfHeight)
            return one / 2;
        if (blocktypes[block].DrawType == Packet_DrawTypeEnum.Flat)
            return one / 20;

        return 1;
    }

    // -------------------------------------------------------------------------
    // Color helpers
    // -------------------------------------------------------------------------

    public static int ColorFromArgb(int a, int r, int g, int b) =>
        (a << 24) | (r << 16) | (g << 8) | b;

    public static int ColorA(int color) => (color >> 24) & 0xFF;
    public static int ColorR(int color) => (color >> 16) & 0xFF;
    public static int ColorG(int color) => (color >> 8) & 0xFF;
    public static int ColorB(int color) => color & 0xFF;

    // -------------------------------------------------------------------------
    // 2D drawing
    // -------------------------------------------------------------------------

    private Model quadModel;

    public void Draw2dTexture(int textureid, float x1, float y1, float width, float height, int? inAtlasId, int atlastextures, int color, bool enabledepthtest)
    {
        if (color == ColorFromArgb(255, 255, 255, 255) && inAtlasId == null)
            Draw2dTextureSimple(textureid, x1, y1, width, height, enabledepthtest);
        else
            Draw2dTextureInAtlas(textureid, x1, y1, width, height, inAtlasId, atlastextures, color, enabledepthtest);
    }

    private void Draw2dTextureSimple(int textureid, float x1, float y1, float width, float height, bool enabledepthtest)
    {
        platform.GlDisableCullFace();
        platform.GlEnableTexture2d();
        platform.BindTexture2d(textureid);

        if (!enabledepthtest)
            platform.GlDisableDepthTest();

        quadModel ??= platform.CreateModel(QuadModelData.GetQuadModelData());

        GLPushMatrix();
        GLTranslate(x1, y1, 0);
        GLScale(width, height, 0);
        GLScale(one / 2, one / 2, 0);
        GLTranslate(one, one, 0);
        DrawModel(quadModel);
        GLPopMatrix();

        if (!enabledepthtest)
            platform.GlEnableDepthTest();

        platform.GlEnableCullFace();
        platform.GlEnableTexture2d();
    }

    private void Draw2dTextureInAtlas(int textureid, float x1, float y1, float width, float height, int? inAtlasId, int atlastextures, int color, bool enabledepthtest)
    {
        if (inAtlasId == null)
            return;

        RectangleF rect = new(0, 0, 1, 1);
        TextureAtlasCi.TextureCoords2d(inAtlasId ?? 0, atlastextures, rect);

        platform.GlDisableCullFace();
        platform.GlEnableTexture2d();
        platform.BindTexture2d(textureid);

        if (!enabledepthtest)
            platform.GlDisableDepthTest();

        ModelData data = QuadModelData.GetColoredQuadModelData(
            rect.X, rect.Y, rect.Width, rect.Height,
            x1, y1, width, height,
            (byte)ColorR(color), (byte)ColorG(color), (byte)ColorB(color), (byte)ColorA(color));
        DrawModelData(data);

        if (!enabledepthtest)
            platform.GlEnableDepthTest();

        platform.GlEnableCullFace();
        platform.GlEnableTexture2d();
    }

    public void Draw2dTexturePart(int textureid, float srcwidth, float srcheight, float dstx, float dsty, float dstwidth, float dstheight, int color, bool enabledepthtest)
    {
        RectangleF rect = new(0, 0, srcwidth, srcheight);

        platform.GlDisableCullFace();
        platform.GlEnableTexture2d();
        platform.BindTexture2d(textureid);

        if (!enabledepthtest)
            platform.GlDisableDepthTest();

        ModelData data = QuadModelData.GetColoredQuadModelData(
            rect.X, rect.Y, rect.Width, rect.Height,
            dstx, dsty, dstwidth, dstheight,
            (byte)ColorR(color), (byte)ColorG(color), (byte)ColorB(color), (byte)ColorA(color));
        DrawModelData(data);

        if (!enabledepthtest)
            platform.GlEnableDepthTest();

        platform.GlEnableCullFace();
        platform.GlEnableTexture2d();
    }

    public void Draw2dTextures(Draw2dData[] todraw, int todrawLength, int textureid)
    {
        ModelData[] modelDatas = new ModelData[512];
        int modelDatasCount = 0;

        for (int i = 0; i < todrawLength; i++)
        {
            Draw2dData d = todraw[i];
            RectangleF rect = new(0, 0, 1, 1);
            if (d.inAtlasId != null)
                TextureAtlasCi.TextureCoords2d(d.inAtlasId, texturesPacked(), rect);

            modelDatas[modelDatasCount++] = QuadModelData.GetColoredQuadModelData(
                rect.X, rect.Y, rect.Width, rect.Height,
                d.x1, d.y1, d.width, d.height,
                (byte)ColorR(d.color), (byte)ColorG(d.color), (byte)ColorB(d.color), (byte)ColorA(d.color));
        }

        ModelData combined = CombineModelData(modelDatas, modelDatasCount);

        platform.GlDisableCullFace();
        platform.GlEnableTexture2d();
        platform.BindTexture2d(textureid);
        platform.GlDisableDepthTest();
        DrawModelData(combined);
        platform.GlEnableDepthTest();
        platform.GlDisableCullFace();
        platform.GlEnableTexture2d();
    }

    public static ModelData CombineModelData(ModelData[] modelDatas, int count)
    {
        int totalIndices = 0;
        int totalVertices = 0;
        for (int i = 0; i < count; i++)
        {
            totalIndices += modelDatas[i].indicesCount;
            totalVertices += modelDatas[i].verticesCount;
        }

        ModelData ret = new()
        {
            indices = new int[totalIndices],
            xyz = new float[totalVertices * 3],
            uv = new float[totalVertices * 2],
            rgba = new byte[totalVertices * 4]
        };

        for (int i = 0; i < count; i++)
        {
            ModelData m = modelDatas[i];
            int baseVertex = ret.verticesCount;
            int baseIndex = ret.indicesCount;

            for (int k = 0; k < m.indicesCount; k++)
                ret.indices[baseIndex + k] = m.indices[k] + baseVertex;
            ret.indicesCount += m.indicesCount;

            for (int k = 0; k < m.verticesCount * 3; k++)
                ret.xyz[baseVertex * 3 + k] = m.xyz[k];

            for (int k = 0; k < m.verticesCount * 2; k++)
                ret.uv[baseVertex * 2 + k] = m.uv[k];

            for (int k = 0; k < m.verticesCount * 4; k++)
                ret.rgba[baseVertex * 4 + k] = m.rgba[k];

            ret.verticesCount += m.verticesCount;
        }

        return ret;
    }

    // -------------------------------------------------------------------------
    // 2D text
    // -------------------------------------------------------------------------

    public void Draw2dText(string text, FontCi font, float x, float y, int? color, bool enabledepthtest)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        color ??= ColorFromArgb(255, 255, 255, 255);
        Text_ t = new()
        {
            text = text,
            color = color.Value,
            fontsize = font.size,
            fontfamily = font.family,
            fontstyle = font.style
        };

        if (GetCachedTextTexture(t) == null)
        {
            CachedTexture ct = MakeTextTexture(t);
            if (ct == null)
                return;

            for (int i = 0; i < cachedTextTexturesMax; i++)
            {
                if (cachedTextTextures[i] == null)
                {
                    cachedTextTextures[i] = new CachedTextTexture { text = t, texture = ct };
                    break;
                }
            }
        }

        CachedTexture cached = GetCachedTextTexture(t);
        cached.lastuseMilliseconds = platform.TimeMillisecondsFromStart();
        platform.GLDisableAlphaTest();
        Draw2dTexture(cached.textureId, x, y, cached.sizeX, cached.sizeY, null, 0, ColorFromArgb(255, 255, 255, 255), enabledepthtest);
        platform.GLEnableAlphaTest();
        DeleteUnusedCachedTextTextures();
    }
}