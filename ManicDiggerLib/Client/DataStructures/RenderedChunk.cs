
/// <summary>
/// Holds the GPU-side rendering state for a single chunk.
/// A chunk is marked <see cref="Dirty"/> on creation and whenever its visual state needs rebuilding.
/// </summary>
public class RenderedChunk
{
    internal bool Dirty;
    internal int[] Ids;
    internal int IdsCount;
    internal byte[] Light;

    /// <summary>
    /// True when <see cref="Light"/> was rented from <see cref="System.Buffers.ArrayPool{T}.Shared"/>
    /// by <c>ModDrawTerrain.CalculateShadows</c> and must be returned to the pool
    /// rather than simply nulled when this chunk is unloaded.
    /// </summary>
    internal bool LightRented;

    /// <summary>
    /// Returns <see cref="Light"/> to <see cref="System.Buffers.ArrayPool{T}.Shared"/>
    /// if it was rented, then nulls the reference. Safe to call when Light is null.
    /// </summary>
    internal void ReleaseLight()
    {
        if (Light == null) return;
        if (LightRented)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(Light);
            LightRented = false;
        }
        Light = null;
    }
}