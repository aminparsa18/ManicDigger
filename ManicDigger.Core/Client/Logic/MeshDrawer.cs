using OpenTK.Mathematics;

public class MeshDrawer : IMeshDrawer
{
    public Stack<Matrix4> mvMatrix { get; set; }
    public Stack<Matrix4> pMatrix { get; set; }

    private bool currentMatrixModeProjection;
    private Stack<Matrix4> ActiveMatrix => currentMatrixModeProjection ? pMatrix : mvMatrix;
    private readonly IOpenGlService openGlService;

    public MeshDrawer(IOpenGlService openGlService)
    {
        this.openGlService = openGlService;
        mvMatrix = new Stack<Matrix4>();
        mvMatrix.Push(Matrix4.Identity);
        pMatrix = new();
        pMatrix.Push(Matrix4.Identity);
    }

    public void DrawModel(GeometryModel model)
    {
        SetMatrixUniformModelView();
        openGlService.DrawModel(model);
    }

    public void DrawModels(List<GeometryModel> models, int count)
    {
        SetMatrixUniformModelView();
        openGlService.DrawModels(models, count);
    }

    // -------------------------------------------------------------------------
    // Shader uniforms
    // -------------------------------------------------------------------------

    public void SetMatrixUniformProjection()
    {
        var p = pMatrix.Peek();
        openGlService.SetMatrixUniformProjection(ref p);
    }

    public void SetMatrixUniformModelView()
    {
        var mv = mvMatrix.Peek();
        openGlService.SetMatrixUniformModelView(ref mv);
    }

    public void DrawModelData(GeometryModel data)
    {
        SetMatrixUniformModelView();
        openGlService.DrawModelData(data);
    }

    public void GLPushMatrix()
    {
        ActiveMatrix.Push(ActiveMatrix.Peek());
    }

    public void GLPopMatrix()
    {
        if (ActiveMatrix.Count > 1)
            ActiveMatrix.Pop();
    }

    public void GLLoadIdentity()
    {
        if (ActiveMatrix.Count > 0)
            ActiveMatrix.Pop();
        ActiveMatrix.Push(Matrix4.Identity);
    }

    public void GLLoadMatrix(Matrix4 m)
    {
        if (ActiveMatrix.Count > 0)
            ActiveMatrix.Pop();
        ActiveMatrix.Push(m);
    }

    private void GLOrtho(float left, float right, float bottom, float top, float zNear, float zFar)
    {
        if (currentMatrixModeProjection)
        {
            Matrix4.CreateOrthographicOffCenter(left, right, bottom, top, zNear, zFar, out Matrix4 ortho);
            pMatrix.Pop();
            pMatrix.Push(ortho);
        }
        else
        {
            throw new ArgumentException("GLOrtho");
        }
    }

    // -------------------------------------------------------------------------
    // Matrix stack operations
    // -------------------------------------------------------------------------
    // -------------------------------------------------------------------------
    public void GLScale(float x, float y, float z)
    {
        Matrix4.CreateScale(x, y, z, out Matrix4 scale);
        MultiplyActiveMatrix(scale);
    }

    public void GLRotate(float angle, float x, float y, float z)
    {
        float radians = angle / 360 * 2 * MathF.PI;
        Matrix4.CreateFromAxisAngle(new Vector3(x, y, z), radians, out Matrix4 rotation);
        MultiplyActiveMatrix(rotation);
    }

    public void GLTranslate(float x, float y, float z)
    {
        Matrix4.CreateTranslation(x, y, z, out Matrix4 translation);
        MultiplyActiveMatrix(translation);
    }

    // -------------------------------------------------------------------------
    // Projection helpers
    // -------------------------------------------------------------------------

    public void OrthoMode(int width, int height)
    {
        GLMatrixModeProjection();
        GLPushMatrix();
        GLLoadIdentity();
        GLOrtho(0, width, height, 0, 0, 1);
        SetMatrixUniformProjection();

        GLMatrixModeModelView();
        GLPushMatrix();
        GLLoadIdentity();
        SetMatrixUniformModelView();
    }

    public void PerspectiveMode()
    {
        GLMatrixModeProjection();
        GLPopMatrix();
        SetMatrixUniformProjection();

        GLMatrixModeModelView();
        GLPopMatrix();
        SetMatrixUniformModelView();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void MultiplyActiveMatrix(Matrix4 transform)
    {
        var m = ActiveMatrix.Peek();
        m = transform * m;
        ActiveMatrix.Pop();
        ActiveMatrix.Push(m);
    }

    // -------------------------------------------------------------------------
    // Matrix mode
    // -------------------------------------------------------------------------

    public void GLMatrixModeModelView() => currentMatrixModeProjection = false;
    public void GLMatrixModeProjection() => currentMatrixModeProjection = true;
}