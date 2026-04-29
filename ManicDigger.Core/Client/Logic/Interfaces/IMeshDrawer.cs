public interface IMeshDrawer
{
    /// <summary>Dispatches draw calls for a list of models in a single batch.</summary>
    void DrawModels(List<GeometryModel> models, int count);
}