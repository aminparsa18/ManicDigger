//This class is a render batch manager — it groups 3D models by texture so the GPU doesn't have to switch textures 
//constantly (which is expensive). You register models with Add, they get a slot ID, and every frame Draw renders
//everything in two passes: solid geometry first (to fill the depth buffer), then transparent geometry 
//(water, glass, etc.) on top with back-face culling disabled so both sides of surfaces show.

public interface IMeshBatcher
{
    int Add(GeometryModel modelData, bool transparent, int texture, float centerX, float centerY, float centerZ, float radius);
    void Clear();
    void Draw(float playerPositionX, float playerPositionY, float playerPositionZ);
    void Remove(int id);
    int TotalTriangleCount();
}