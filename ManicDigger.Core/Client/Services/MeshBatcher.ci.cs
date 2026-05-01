//This class is a render batch manager — it groups 3D models by texture so the GPU doesn't have to switch textures 
//constantly (which is expensive). You register models with Add, they get a slot ID, and every frame Draw renders
//everything in two passes: solid geometry first (to fill the depth buffer), then transparent geometry 
//(water, glass, etc.) on top with back-face culling disabled so both sides of surfaces show.

/// <summary>
/// Manages a pool of renderable 3D models, batching draw calls by texture
/// and separating solid from transparent geometry for correct render ordering.
/// </summary>
/// <remarks>
/// Models are stored in a fixed-size slot array. Freed slots are tracked in a
/// <see cref="Stack{T}"/> free-list so new additions reuse slots before growing
/// the active count. Call <see cref="Add"/> to register a model and
/// <see cref="Remove"/> to release it. Call <see cref="Draw"/> once per frame.
/// </remarks>
public class MeshBatcher : IMeshBatcher
{
    /// <summary>Maximum number of model slots in the pool.</summary>
    private const int ModelsMax = 1024 * 16;

    /// <summary>Maximum number of distinct textures that can be batched.</summary>
    private const int MaxTextures = 10;

    private readonly IOpenGlService _platform;
    private readonly IMeshDrawer meshDrawer;

    /// <summary>
    /// When <c>true</c>, <see cref="Draw"/> will bind each texture before issuing
    /// draw calls. Set to <c>false</c> when the caller manages texture binding externally.
    /// </summary>
    private readonly bool BindTexture;

    /// <summary>Flat array of all model slots. Index == model ID.</summary>
    private readonly BatchEntry[] _models;

    /// <summary>One-past-the-last used slot index; grows on <see cref="Add"/> when free-list is empty.</summary>
    private int _modelsCount;

    /// <summary>Free-list of previously released slot indices available for reuse.</summary>
    private readonly Stack<int> _freeSlots;

    /// <summary>
    /// Maps OpenGL texture handles to their logical texture index.
    /// Replaces the linear <c>IndexOf</c> scan with an O(1) lookup and removes
    /// the "value 0 = unoccupied" convention that treated handle 0 as invalid.
    /// </summary>
    private readonly Dictionary<int, int> _textureIndexMap;

    /// <summary>Ordered list of registered texture handles, indexed by logical texture ID.</summary>
    private readonly List<int> _glTextures;

    private readonly List<GeometryModel>[] _tocallSolid;
    private readonly List<GeometryModel>[] _tocallTransparent;

    /// <summary>
    /// Initialises a new <see cref="MeshBatcher"/> with pre-allocated model slots.
    /// </summary>
    public MeshBatcher(IOpenGlService platform, IMeshDrawer meshDrawer)
    {
        _platform = platform;
        this.meshDrawer = meshDrawer;
        _models = new BatchEntry[ModelsMax];

        _modelsCount = 0;
        _freeSlots = new Stack<int>();
        _textureIndexMap = new Dictionary<int, int>(MaxTextures);
        _glTextures = new List<int>(MaxTextures);

        _tocallSolid = new List<GeometryModel>[MaxTextures];
        _tocallTransparent = new List<GeometryModel>[MaxTextures];
        for (int i = 0; i < MaxTextures; i++)
        {
            _tocallSolid[i] = [];
            _tocallTransparent[i] = [];
        }

        BindTexture = true;
    }

    /// <summary>
    /// Registers a model in the batch and returns its assigned slot ID.
    /// The ID is stable until <see cref="Remove"/> is called with it.
    /// </summary>
    /// <param name="modelData">The geometry data used to create the GPU model.</param>
    /// <param name="transparent">
    ///     <c>true</c> if this model should be rendered in the transparent pass
    ///     (after all solid geometry); <c>false</c> for the solid pass.
    /// </param>
    /// <param name="texture">OpenGL texture handle to associate with this model.</param>
    /// <param name="centerX">World-space X centre of the model's bounding sphere.</param>
    /// <param name="centerY">World-space Y centre of the model's bounding sphere.</param>
    /// <param name="centerZ">World-space Z centre of the model's bounding sphere.</param>
    /// <param name="radius">Radius of the model's bounding sphere, used for frustum culling.</param>
    /// <returns>The slot ID representing this model. Pass it to <see cref="Remove"/> later.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when more than <see cref="MaxTextures"/> distinct textures are registered.
    /// </exception>
    public int Add(GeometryModel modelData, bool transparent, int texture,
                   float centerX, float centerY, float centerZ, float radius)
    {
        int id = _freeSlots.Count > 0
            ? _freeSlots.Pop()
            : _modelsCount++;

        _models[id] = new BatchEntry
        {
            IndicesCount = modelData.IndicesCount,
            CenterX = centerX,
            CenterY = centerY,
            CenterZ = centerZ,
            Radius = radius,
            Transparent = transparent,
            Empty = false,
            Texture = GetTextureId(texture),
            Model = _platform.CreateModel(modelData),
        };

        return id;
    }

    /// <summary>
    /// Releases the model at the given slot ID, freeing its GPU resources
    /// and returning the slot to the free-list for reuse.
    /// </summary>
    /// <param name="id">The slot ID previously returned by <see cref="Add"/>.</param>
    public void Remove(int id)
    {
        _platform.DeleteModel(_models[id].Model);
        _models[id] = _models[id] with { Empty = true };
        _freeSlots.Push(id);
    }

    /// <summary>
    /// Renders all registered models for the current frame.
    /// Solid models are drawn first (to populate the depth buffer), followed by
    /// transparent models (with back-face culling disabled) to ensure correct blending.
    /// </summary>
    /// <param name="playerPositionX">Player world-space X coordinate.</param>
    /// <param name="playerPositionY">Player world-space Y coordinate.</param>
    /// <param name="playerPositionZ">Player world-space Z coordinate.</param>
    public void Draw(float playerPositionX, float playerPositionY, float playerPositionZ)
    {
        SortListsByTexture();

        // Solid pass: fills the depth buffer before any transparency is drawn.
        for (int i = 0; i < _glTextures.Count; i++)
        {
            if (_tocallSolid[i].Count == 0) continue;

            if (BindTexture)
                _platform.BindTexture2d(_glTextures[i]);

            meshDrawer.DrawModels(_tocallSolid[i], _tocallSolid[i].Count);
        }

        // Transparent pass: back-face culling disabled so water surfaces etc. render correctly.
        _platform.GlDisableCullFace();
        for (int i = 0; i < _glTextures.Count; i++)
        {
            if (_tocallTransparent[i].Count == 0) continue;

            if (BindTexture)
                _platform.BindTexture2d(_glTextures[i]);

            meshDrawer.DrawModels(_tocallTransparent[i], _tocallTransparent[i].Count);
        }
        _platform.GlEnableCullFace();
    }

    /// <summary>
    /// Returns the logical texture index for <paramref name="glTexture"/>,
    /// registering it if not already present.
    /// </summary>
    /// <param name="glTexture">The OpenGL texture handle to look up or register.</param>
    /// <returns>The logical index of <paramref name="glTexture"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when adding a new texture would exceed <see cref="MaxTextures"/>.
    /// </exception>
    private int GetTextureId(int glTexture)
    {
        if (_textureIndexMap.TryGetValue(glTexture, out int id))
            return id;

        if (_glTextures.Count >= MaxTextures)
            throw new InvalidOperationException(
                $"MeshBatcher exceeded the maximum of {MaxTextures} distinct textures.");

        id = _glTextures.Count;
        _glTextures.Add(glTexture);
        _textureIndexMap[glTexture] = id;
        return id;
    }

    /// <summary>
    /// Clears and repopulates the per-texture solid and transparent draw lists
    /// by iterating active model slots and bucketing them by texture index.
    /// </summary>
    private void SortListsByTexture()
    {
        for (int i = 0; i < _glTextures.Count; i++)
        {
            _tocallSolid[i].Clear();
            _tocallTransparent[i].Clear();
        }

        for (int i = 0; i < _modelsCount; i++)
        {
            ref readonly BatchEntry entry = ref _models[i];

            if (entry.Empty) continue;

            List<GeometryModel> bucket = entry.Transparent
                ? _tocallTransparent[entry.Texture]
                : _tocallSolid[entry.Texture];

            bucket.Add(entry.Model);
        }
    }

    /// <summary>
    /// Returns the total number of triangles currently active in the batch.
    /// Each triangle is represented by 3 indices.
    /// </summary>
    public int TotalTriangleCount()
    {
        int sum = 0;
        for (int i = 0; i < _modelsCount; i++)
        {
            ref readonly BatchEntry entry = ref _models[i];
            if (!entry.Empty)
                sum += entry.IndicesCount;
        }
        return sum / 3;
    }

    /// <summary>
    /// Removes all active models from the batch, releasing their GPU resources
    /// and resetting the free-list. This also frees all GPU memory — it is not
    /// a lightweight reset.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _modelsCount; i++)
        {
            if (!_models[i].Empty)
                Remove(i);
        }
        _glTextures.Clear();
        _textureIndexMap.Clear();
    }
}

/// <summary>
/// Represents a single occupied slot in a <see cref="MeshBatcher"/> pool,
/// storing all data needed to cull and draw one model.
/// Stored as a <c>readonly record struct</c> so all slots are embedded inline
/// in the <c>_models[]</c> array — no per-slot heap allocation.
/// </summary>
public readonly record struct BatchEntry
{
    /// <summary>
    /// Whether this slot is free and available for reuse.
    /// <c>true</c> after <see cref="MeshBatcher.Remove"/> is called.
    /// </summary>
    public bool Empty { get; init; }

    /// <summary>Total index count of the model's geometry (triangle count × 3).</summary>
    public int IndicesCount { get; init; }

    /// <summary>World-space X coordinate of the model's bounding sphere centre.</summary>
    public float CenterX { get; init; }

    /// <summary>World-space Y coordinate of the model's bounding sphere centre.</summary>
    public float CenterY { get; init; }

    /// <summary>World-space Z coordinate of the model's bounding sphere centre.</summary>
    public float CenterZ { get; init; }

    /// <summary>Radius of the model's bounding sphere, used for frustum culling.</summary>
    public float Radius { get; init; }

    /// <summary>
    /// Whether this model belongs to the transparent render pass.
    /// <c>false</c> = solid pass (drawn first); <c>true</c> = transparent pass.
    /// </summary>
    public bool Transparent { get; init; }

    /// <summary>Logical index into the batcher's texture slot list.</summary>
    public int Texture { get; init; }

    /// <summary>The GPU model handle issued by the platform layer.</summary>
    public GeometryModel Model { get; init; }
}