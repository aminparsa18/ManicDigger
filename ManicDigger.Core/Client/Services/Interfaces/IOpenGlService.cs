using OpenTK.Mathematics;
// ─────────────────────────────────────────────────────────────────────────────
// OpenGL
// ─────────────────────────────────────────────────────────────────────────────

public interface IOpenGlService
{
    void GlViewport(int x, int y, int width, int height);
    void GlClearColorBufferAndDepthBuffer();
    void GlDisableDepthTest();
    void GlClearColorRgbaf(float r, float g, float b, float a);
    void GlEnableDepthTest();
    void GlDisableCullFace();
    void GlEnableCullFace();
    void GLLineWidth(int width);
    void GLDeleteTexture(int id);
    void GlClearDepthBuffer();
    void GlLightModelAmbient(int r, int g, int b);
    void GlEnableFog();
    void GlFogFogColor(int r, int g, int b, int a);
    void GlFogFogDensity(float density);
    int GlGetMaxTextureSize();
    void GlDepthMask(bool flag);
    void GlCullFaceBack();
    void GlDisableFog();
    void BindTexture2d(int texture);
    GeometryModel CreateModel(GeometryModel modelData);
    void UpdateModel(GeometryModel data);
    void DrawModel(GeometryModel model);
    void InitShaders();
    void SetMatrixUniformProjection(ref Matrix4 pMatrix);
    void SetMatrixUniformModelView(ref Matrix4 mvMatrix);
    void DrawModels(List<GeometryModel> model, int count);
    void DrawModelData(GeometryModel data);
    void DeleteModel(GeometryModel model);
    int LoadTextureFromBitmap(Bitmap bmp);
}