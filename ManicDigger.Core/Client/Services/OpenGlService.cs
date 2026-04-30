using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Drawing.Imaging;
using System.Numerics;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

public class OpenGlService : IOpenGlService
{
    public bool ALLOW_NON_POWER_OF_TWO = false;
    public bool ENABLE_MIPMAPS = true;
    public bool ENABLE_TRANSPARENCY = true;

    private Matrix4 _projectionMatrix;
    private Matrix4 _modelViewMatrix;
    private int _shaderProgram = -1; // will be set when we create shaders
    private int _uUseTexture;
    private int _uProjection;
    private int _uModelView;
    private int _uAmbientLight;
    private int _uFogEnabled;
    private int _uFogColor;
    private int _uFogDensity;

    private Vector3 _ambientLight = Vector3.One;
    private Vector4 _fogColor = Vector4.One;
    private float _fogDensity = 0.003f;

    public void BindTexture2d(int texture)
    {
        GL.BindTexture(TextureTarget.Texture2D, texture);
        if (_shaderProgram != -1)
            GL.Uniform1(_uUseTexture, texture != 0 ? 1 : 0);
    }

    public GeometryModel CreateModel(GeometryModel data)
    {
        int vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        // positions → attribute 0
        int vertexVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Xyz.Length * sizeof(float), data.Xyz, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
        GL.EnableVertexAttribArray(0);

        // colors → attribute 1
        int colorVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, colorVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Rgba.Length * sizeof(byte), data.Rgba, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.UnsignedByte, true, 0, 0);
        GL.EnableVertexAttribArray(1);

        // UVs → attribute 2
        int uvVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, uvVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Uv.Length * sizeof(float), data.Uv, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 0, 0);
        GL.EnableVertexAttribArray(2);

        // indices
        int indexVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexVbo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, data.Indices.Length * sizeof(int), data.Indices, BufferUsageHint.StaticDraw);

        GL.BindVertexArray(0);

        data.VaoId = vao;
        data.VertexVboId = vertexVbo;
        data.ColorVboId = colorVbo;
        data.UvVboId = uvVbo;
        data.IndexVboId = indexVbo;
        return data;
    }

    public void DeleteModel(GeometryModel model)
    {
        GeometryModel m = model;
        GL.DeleteVertexArray(m.VaoId);
        GL.DeleteBuffer(m.VertexVboId);
        GL.DeleteBuffer(m.ColorVboId);
        GL.DeleteBuffer(m.UvVboId);
        GL.DeleteBuffer(m.IndexVboId);
    }

    public void DrawModel(GeometryModel model)
    {
        DrawModelData(model);
    }

    public void DrawModelData(GeometryModel data)
    {
        GL.UseProgram(_shaderProgram);
        GL.UniformMatrix4(_uProjection, false, ref _projectionMatrix);
        GL.UniformMatrix4(_uModelView, false, ref _modelViewMatrix);
        GL.BindVertexArray(data.VaoId);
        PrimitiveType primitiveType = data.Mode == (int)DrawMode.Triangles
            ? PrimitiveType.Triangles
            : PrimitiveType.Lines;
        GL.DrawElements(primitiveType, data.IndicesCount, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    public void DrawModels(List<GeometryModel> models, int count)
    {
        for (int i = 0; i < count; i++)
        {
            DrawModelData(models[i]);
        }
    }

    public void GlClearColorBufferAndDepthBuffer()
    {
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public void GlClearColorRgbaf(float r, float g, float b, float a)
    {
        GL.ClearColor(r, g, b, a);
    }

    public void GlClearDepthBuffer()
    {
        GL.Clear(ClearBufferMask.DepthBufferBit);
    }

    public void GlCullFaceBack()
    {
        GL.CullFace(TriangleFace.Back);
    }

    public void GLDeleteTexture(int id)
    {
        GL.DeleteTexture(id);
    }

    public void GlDepthMask(bool flag)
    {
        GL.DepthMask(flag);
    }

    public void GlDisableCullFace()
    {
        GL.Disable(EnableCap.CullFace);
    }

    public void GlDisableDepthTest()
    {
        GL.Disable(EnableCap.DepthTest);
    }

    public void GlDisableFog()
    {
        if (_shaderProgram != -1)
            GL.Uniform1(_uFogEnabled, 0);
    }

    public void GlEnableCullFace()
    {
        GL.Enable(EnableCap.CullFace);
    }

    public void GlEnableDepthTest()
    {
        GL.Enable(EnableCap.DepthTest);
    }

    public void GlEnableFog()
    {
        if (_shaderProgram != -1)
            GL.Uniform1(_uFogEnabled, 1);
    }

    public void GlFogFogColor(int r, int g, int b, int a)
    {
        _fogColor = new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
        if (_shaderProgram != -1)
            GL.Uniform4(_uFogColor, _fogColor);
    }

    public void GlFogFogDensity(float density)
    {
        _fogDensity = density;
        if (_shaderProgram != -1)
            GL.Uniform1(_uFogDensity, _fogDensity);
    }

    public int GlGetMaxTextureSize()
    {
        GL.GetInteger(GetPName.MaxTextureSize, out int size);
        return size;
    }

    public void GlLightModelAmbient(int r, int g, int b)
    {
        _ambientLight = new Vector3(r / 255f, g / 255f, b / 255f);
        if (_shaderProgram != -1)
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "uAmbientLight"),
                _ambientLight.X, _ambientLight.Y, _ambientLight.Z);
    }

    public void GLLineWidth(int width)
    {
        GL.LineWidth(width);
    }

    public void GlViewport(int x, int y, int width, int height)
    {
        GL.Viewport(x, y, width, height);
    }

    public void InitShaders()
    {
        string vertexSource = @"
        #version 330 core

        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec4 aColor;
        layout(location = 2) in vec2 aUv;

        uniform mat4 uProjection;
        uniform mat4 uModelView;

        out vec4 vColor;
        out vec2 vUv;
        out float vFogDepth;

        void main()
        {
            vec4 viewPos = uModelView * vec4(aPosition, 1.0);
            gl_Position = uProjection * viewPos;
            vColor = aColor;
            vUv = aUv;
            vFogDepth = abs(viewPos.z);
        }
    ";

        string fragmentSource = @"
        #version 330 core

        in vec4 vColor;
        in vec2 vUv;
        in float vFogDepth;

        uniform sampler2D uTexture;
        uniform vec3 uAmbientLight;
        uniform vec4 uFogColor;
        uniform float uFogDensity;
        uniform bool uFogEnabled;
        uniform bool uUseTexture;
        
        out vec4 fragColor;

        void main()
        {
            if (uUseTexture)
                fragColor = texture(uTexture, vUv) * vColor;
            else
                fragColor = vColor; // sky sphere, hand tint, etc.
            
            // only discard when texturing — alpha test doesn't apply to vertex-colored geometry
            if (uUseTexture && fragColor.a < 0.5)
                discard;

            fragColor.rgb *= uAmbientLight;

            if (uFogEnabled)
            {
                float fogFactor = exp(-uFogDensity * uFogDensity * vFogDepth * vFogDepth);
                fogFactor = clamp(fogFactor, 0.0, 1.0);
                fragColor.rgb = mix(uFogColor.rgb, fragColor.rgb, fogFactor);
            }
        }
    ";

        // compile vertex shader
        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);
        GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int vertStatus);
        if (vertStatus == 0)
            throw new Exception($"Vertex shader error: {GL.GetShaderInfoLog(vertexShader)}");

        // compile fragment shader
        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);
        GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int fragStatus);
        if (fragStatus == 0)
            throw new Exception($"Fragment shader error: {GL.GetShaderInfoLog(fragmentShader)}");

        // link program
        _shaderProgram = GL.CreateProgram();
        GL.AttachShader(_shaderProgram, vertexShader);
        GL.AttachShader(_shaderProgram, fragmentShader);
        GL.LinkProgram(_shaderProgram);
        GL.GetProgram(_shaderProgram, GetProgramParameterName.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
            throw new Exception($"Shader link error: {GL.GetProgramInfoLog(_shaderProgram)}");

        // cleanup - shaders are linked into program, no longer needed
        GL.DetachShader(_shaderProgram, vertexShader);
        GL.DetachShader(_shaderProgram, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        // set initial uniform values
        GL.UseProgram(_shaderProgram);
        GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "uTexture"), 0);
        GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "uAmbientLight"), 1f, 1f, 1f);
        GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "uFogEnabled"), 0);
        GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "uFogDensity"), _fogDensity);
        GL.Uniform4(GL.GetUniformLocation(_shaderProgram, "uFogColor"), _fogColor);
        _uUseTexture = GL.GetUniformLocation(_shaderProgram, "uUseTexture");
        _uProjection = GL.GetUniformLocation(_shaderProgram, "uProjection");
        _uModelView = GL.GetUniformLocation(_shaderProgram, "uModelView");
        _uAmbientLight = GL.GetUniformLocation(_shaderProgram, "uAmbientLight");
        _uFogEnabled = GL.GetUniformLocation(_shaderProgram, "uFogEnabled");
        _uFogColor = GL.GetUniformLocation(_shaderProgram, "uFogColor");
        _uFogDensity = GL.GetUniformLocation(_shaderProgram, "uFogDensity");

        // set initial values using cached locations
        GL.Uniform3(_uAmbientLight, 1f, 1f, 1f);
        GL.Uniform1(_uFogEnabled, 0);
        GL.Uniform1(_uFogDensity, _fogDensity);
        GL.Uniform4(_uFogColor, _fogColor);

        GL.UseProgram(_shaderProgram);
    }

    public int LoadTextureFromBitmap(Bitmap bmp)
    {
        return LoadTexture(bmp, false);
    }

    public void SetMatrixUniformModelView(ref Matrix4 mvMatrix)
    {
        _modelViewMatrix = mvMatrix;
        if (_shaderProgram != -1)
            GL.UniformMatrix4(_uModelView, false, ref _modelViewMatrix);
    }

    public void SetMatrixUniformProjection(ref Matrix4 pMatrix)
    {
        _projectionMatrix = pMatrix;
        if (_shaderProgram != -1)
            GL.UniformMatrix4(_uProjection, false, ref _projectionMatrix);
    }

    public void UpdateModel(GeometryModel data)
    {
        if (data.VaoId == 0)
        {
            CreateModel(data);
            return;
        }

        GL.BindBuffer(BufferTarget.ArrayBuffer, data.VertexVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.Xyz.Length * sizeof(float), data.Xyz);

        GL.BindBuffer(BufferTarget.ArrayBuffer, data.ColorVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.Rgba.Length * sizeof(byte), data.Rgba);

        GL.BindBuffer(BufferTarget.ArrayBuffer, data.UvVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.Uv.Length * sizeof(float), data.Uv);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, data.IndexVboId);
        GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero,
            data.Indices.Length * sizeof(int), data.Indices);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    private int LoadTexture(Bitmap bmpArg, bool linearMag)
    {
        Bitmap bmp = bmpArg;
        bool convertedbitmap = false;
        if (!ALLOW_NON_POWER_OF_TWO &&
            !(BitOperations.IsPow2((uint)bmp.Width) && BitOperations.IsPow2((uint)bmp.Height)))
        {
            Bitmap bmp2 = new(
                (int)BitOperations.RoundUpToPowerOf2((uint)bmp.Width),
                (int)BitOperations.RoundUpToPowerOf2((uint)bmp.Height)
            );
            using (Graphics g = Graphics.FromImage(bmp2))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(bmp, 0, 0, bmp2.Width, bmp2.Height);
            }
            convertedbitmap = true;
            bmp = bmp2;
        }
        // GL.Enable(EnableCap.Texture2D);
        int id = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, id);
        if (!ENABLE_MIPMAPS)
        {
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        }
        else
        {
            //GL.GenerateMipmap(GenerateMipmapTarget.Texture2D); //DOES NOT WORK ON ATI GRAPHIC CARDS
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.GenerateMipmap, 1); //DOES NOT WORK ON ???
            int[] MipMapCount = new int[1];
            GL.GetTexParameter(TextureTarget.Texture2D, GetTextureParameter.TextureMaxLevel, out MipMapCount[0]);
            if (MipMapCount[0] == 0)
            {
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            }
            else
            {
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapLinear);
            }
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, linearMag ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4);
        }
        BitmapData bmp_data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp_data.Width, bmp_data.Height, 0,
            OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, bmp_data.Scan0);

        bmp.UnlockBits(bmp_data);

        GL.Enable(EnableCap.DepthTest);

        if (ENABLE_TRANSPARENCY)
        {
            // TODO: alpha test moved to fragment shader
            // GL.Enable(EnableCap.AlphaTest);
            // GL.AlphaFunc(AlphaFunction.Greater, 0.5f);
        }


        if (ENABLE_TRANSPARENCY)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            //GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, (int)TextureEnvMode.Blend);
            //GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvColor, new Color4(0, 0, 0, byte.MaxValue));
        }

        if (convertedbitmap)
        {
            bmp.Dispose();
        }
        return id;
    }
}
