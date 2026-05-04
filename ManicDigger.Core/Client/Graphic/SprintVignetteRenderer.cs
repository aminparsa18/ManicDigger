using OpenTK.Graphics.OpenGL4;

/// <summary>
/// Renders a radial vignette overlay directly via OpenGL.
/// Completely bypasses the game's 2D draw pipeline.
/// </summary>
public sealed class SprintVignetteRenderer : IDisposable
{
    private int _vao = -1;
    private int _vbo = -1;
    private int _shader = -1;
    private int _uIntensity;
    private bool _initialised;

    // Fullscreen quad in NDC — two triangles covering [-1,1] in XY, Z=0
    private static readonly float[] QuadVertices =
    {
        -1f, -1f,
         1f, -1f,
         1f,  1f,
        -1f, -1f,
         1f,  1f,
        -1f,  1f,
    };

    private const string VertSrc = @"
        #version 330 core
        layout(location = 0) in vec2 aPos;
        out vec2 vUv;
        void main()
        {
            vUv         = aPos;          // [-1,1] in both axes
            gl_Position = vec4(aPos, 0.0, 1.0);
        }
    ";

    private const string FragSrc = @"
        #version 330 core
        in vec2  vUv;
        out vec4 fragColor;
        uniform float uIntensity;   // 0.0 = invisible, 1.0 = full vignette

        void main()
        {
            float dist = length(vUv);                        // 0 = center, ~1.41 = corner
            float t    = smoothstep(0.3, 1.2, dist);         // fade band
            float alpha = t * t * uIntensity * 0.75;         // max 75% opacity
            fragColor  = vec4(0.0, 0.0, 0.0, alpha);
        }
    ";

    private void Init()
    {
        if (_initialised) return;
        _initialised = true;

        // ── Shader ────────────────────────────────────────────────────────────
        int vert = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vert, VertSrc);
        GL.CompileShader(vert);
        GL.GetShader(vert, ShaderParameter.CompileStatus, out int vs);
        if (vs == 0) throw new Exception("Vignette vert: " + GL.GetShaderInfoLog(vert));

        int frag = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(frag, FragSrc);
        GL.CompileShader(frag);
        GL.GetShader(frag, ShaderParameter.CompileStatus, out int fs);
        if (fs == 0) throw new Exception("Vignette frag: " + GL.GetShaderInfoLog(frag));

        _shader = GL.CreateProgram();
        GL.AttachShader(_shader, vert);
        GL.AttachShader(_shader, frag);
        GL.LinkProgram(_shader);
        GL.GetProgram(_shader, GetProgramParameterName.LinkStatus, out int ls);
        if (ls == 0) throw new Exception("Vignette link: " + GL.GetProgramInfoLog(_shader));

        GL.DetachShader(_shader, vert); GL.DeleteShader(vert);
        GL.DetachShader(_shader, frag); GL.DeleteShader(frag);

        _uIntensity = GL.GetUniformLocation(_shader, "uIntensity");

        // ── Geometry ──────────────────────────────────────────────────────────
        _vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);

        _vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer,
            QuadVertices.Length * sizeof(float), QuadVertices,
            BufferUsageHint.StaticDraw);

        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 0, 0);
        GL.EnableVertexAttribArray(0);

        GL.BindVertexArray(0);
    }

    /// <summary>
    /// Call once per frame from the render thread.
    /// <paramref name="intensity"/> is in [0,1] — driven by FovOffset / SprintFovBonus.
    /// </summary>
    public void Draw(float intensity)
    {
        if (intensity < 0.01f) return;

        Init();

        // Save relevant GL state
        GL.GetInteger(GetPName.CurrentProgram, out int prevProgram);

        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        GL.UseProgram(_shader);
        GL.Uniform1(_uIntensity, intensity);

        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        GL.BindVertexArray(0);

        // Restore
        GL.UseProgram(prevProgram);
        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
    }

    public void Dispose()
    {
        if (_vbo != -1) { GL.DeleteBuffer(_vbo); _vbo = -1; }
        if (_vao != -1) { GL.DeleteVertexArray(_vao); _vao = -1; }
        if (_shader != -1) { GL.DeleteProgram(_shader); _shader = -1; }
    }
}