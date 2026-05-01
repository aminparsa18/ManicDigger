using OpenTK.Mathematics;

public interface IMeshDrawer
{
    /// <summary>Model-view matrix stack.</summary>
    Stack<Matrix4> mvMatrix { get; set; }
    Stack<Matrix4> pMatrix { get; set; }


    /// <summary>Sets the model-view matrix uniform and draws <paramref name="model"/>.</summary>
    void DrawModel(GeometryModel model);

    /// <summary>Sets the model-view matrix uniform and draws raw geometry data.</summary>
    void DrawModelData(GeometryModel data);

    /// <summary>Dispatches draw calls for a list of models in a single batch.</summary>
    void DrawModels(List<GeometryModel> models, int count);

    void GLPushMatrix();
    void GLPopMatrix();

    void GLLoadMatrix(Matrix4 m);
    void GLLoadIdentity();

    void GLRotate(float angle, float x, float y, float z);
    void GLTranslate(float x, float y, float z);
    void GLScale(float x, float y, float z);

    void GLMatrixModeModelView();
    void PerspectiveMode();

    /// <summary>Switches GL to orthographic projection for 2-D rendering.</summary>
    void OrthoMode(int width, int height);

    void GLMatrixModeProjection();

    void SetMatrixUniformProjection();

}