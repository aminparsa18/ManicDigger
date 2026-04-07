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
public class MeshBatcher
{
    /// <summary>Maximum number of model slots in the pool.</summary>
    private const int ModelsMax = 1024 * 16;

    /// <summary>Maximum number of distinct textures that can be batched.</summary>
    private const int MaxTextures = 10;

    /// <summary>Core game reference used for platform calls and draw dispatch.</summary>
    internal Game game;

    /// <summary>Frustum culler used to skip models outside the camera's view.</summary>
    internal FrustumCulling d_FrustumCulling;

    /// <summary>
    /// When <c>true</c>, <see cref="Draw"/> will bind each texture before issuing
    /// draw calls. Set to <c>false</c> when the caller manages texture binding externally.
    /// </summary>
    private bool BindTexture;

    /// <summary>Flat array of all model slots. Index == model ID.</summary>
    private readonly ListInfo[] _models;

    /// <summary>One-past-the-last used slot index; grows on <see cref="Add"/> when free-list is empty.</summary>
    private int _modelsCount;

    /// <summary>
    /// Free-list of previously released slot indices available for reuse.
    /// Using <see cref="Stack{T}"/> replaces the hand-rolled parallel array + counter.
    /// </summary>
    private readonly Stack<int> _freeSlots;

    /// <summary>
    /// Tracks OpenGL texture handles. Using <see cref="List{T}"/> replaces the
    /// manual grow-and-copy resize pattern and the separate length counter.
    /// </summary>
    private readonly List<int> _glTextures;

    private List<Model>[] tocallSolid;
    private List<Model>[] tocallTransparent;

    /// <summary>
    /// Initialises a new <see cref="MeshBatcher"/> with pre-allocated model slots.
    /// </summary>
    public MeshBatcher()
    {
        _models = new ListInfo[ModelsMax];
        for (int i = 0; i < ModelsMax; i++)
            _models[i] = new ListInfo();

        _modelsCount = 0;
        _freeSlots = new Stack<int>();
        _glTextures = new List<int>(MaxTextures);
        tocallSolid = new List<Model>[MaxTextures];
        tocallTransparent = new List<Model>[MaxTextures];
        for (int i = 0; i < MaxTextures; i++)
        {
            tocallSolid[i] = new List<Model>();
            tocallTransparent[i] = new List<Model>();
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
    /// <param name="texture">Logical texture index (mapped via <c>GetTextureId</c>).</param>
    /// <param name="centerX">World-space X centre of the model's bounding sphere.</param>
    /// <param name="centerY">World-space Y centre of the model's bounding sphere.</param>
    /// <param name="centerZ">World-space Z centre of the model's bounding sphere.</param>
    /// <param name="radius">Radius of the model's bounding sphere, used for frustum culling.</param>
    /// <returns>The slot ID representing this model. Pass it to <see cref="Remove"/> later.</returns>
    public int Add(ModelData modelData, bool transparent, int texture, float centerX, float centerY, float centerZ, float radius)
    {
        int id = _freeSlots.Count > 0
            ? _freeSlots.Pop()
            : _modelsCount++;

        Model model = game.platform.CreateModel(modelData);

        ListInfo slot = _models[id];
        slot.indicescount = modelData.GetIndicesCount();
        slot.centerX = centerX;
        slot.centerY = centerY;
        slot.centerZ = centerZ;
        slot.radius = radius;
        slot.transparent = transparent;
        slot.empty = false;
        slot.texture = GetTextureId(texture);
        slot.model = model;
        slot.render = true;

        return id;
    }

    /// <summary>
    /// Releases the model at the given slot ID, freeing its GPU resources
    /// and returning the slot to the free-list for reuse.
    /// </summary>
    /// <param name="id">The slot ID previously returned by <see cref="Add"/>.</param>
    public void Remove(int id)
    {
        game.platform.DeleteModel(_models[id].model);
        _models[id].empty = true;
        _freeSlots.Push(id);
    }

    /// <summary>
    /// Renders all registered models for the current frame.
    /// Solid models are drawn first (to populate the depth buffer), followed by
    /// transparent models (with back-face culling disabled) to ensure correct blending.
    /// </summary>
    /// <param name="playerPositionX">Player world-space X, passed to culling logic.</param>
    /// <param name="playerPositionY">Player world-space Y, passed to culling logic.</param>
    /// <param name="playerPositionZ">Player world-space Z, passed to culling logic.</param>
    public void Draw(float playerPositionX, float playerPositionY, float playerPositionZ)
    {

        SortListsByTexture();

        // --- Solid pass: fills the depth buffer before any transparency is drawn.
        for (int i = 0; i < MaxTextures; i++)
        {
            if (tocallSolid[i].Count == 0) continue;

            if (BindTexture)
                game.platform.BindTexture2d(_glTextures[i]);

            game.DrawModels(tocallSolid[i], tocallSolid[i].Count);
        }

        // --- Transparent pass: back-face culling disabled so water surfaces etc. render correctly.
        game.platform.GlDisableCullFace();
        for (int i = 0; i < MaxTextures; i++)
        {
            if (tocallTransparent[i].Count == 0) continue;

            if (BindTexture)
                game.platform.BindTexture2d(_glTextures[i]);

            game.DrawModels(tocallTransparent[i], tocallTransparent[i].Count);
        }
        game.platform.GlEnableCullFace();
    }

    /// <summary>
    /// Returns the index of <paramref name="glTexture"/> in the texture slot list,
    /// registering it in a new slot if not already present.
    /// The list grows automatically if all current slots are occupied.
    /// </summary>
    /// <param name="glTexture">The OpenGL texture handle to look up or register.</param>
    /// <returns>The index of <paramref name="glTexture"/> in <c>_glTextures</c>.</returns>
    private int GetTextureId(int glTexture)
    {
        // Already registered — return existing slot.
        int id = _glTextures.IndexOf(glTexture);
        if (id != -1)
            return id;

        // Find an empty slot (value 0 == unoccupied).
        id = _glTextures.IndexOf(0);
        if (id != -1)
        {
            _glTextures[id] = glTexture;
            return id;
        }

        // No free slot — append a new one.
        _glTextures.Add(glTexture);
        return _glTextures.Count - 1;
    }

    /// <summary>
    /// Clears and repopulates the per-texture solid and transparent draw lists
    /// by iterating active, visible model slots and bucketing them by texture index.
    /// </summary>
    /// <remarks>
    /// <see cref="tocallSolid"/> and <see cref="tocallTransparent"/> are initialised
    /// once in the constructor. Each call resets all counts then re-buckets models,
    /// growing per-texture lists automatically via <see cref="List{T}"/>.
    /// </remarks>
    private void SortListsByTexture()
    {
        // Reset counts for this frame.
        for (int i = 0; i < MaxTextures; i++)
        {
            tocallSolid[i].Clear();
            tocallTransparent[i].Clear();
        }

        // Bucket each active, visible model into the correct texture + pass list.
        for (int i = 0; i < _modelsCount; i++)
        {
            ListInfo li = _models[i];

            if (li.empty || !li.render)
                continue;

            List<Model> bucket = li.transparent
                ? tocallTransparent[li.texture]
                : tocallSolid[li.texture];

            bucket.Add(li.model);
        }
    }

    /// <summary>
    /// Returns the total number of triangles currently active in the batch.
    /// Each triangle is represented by 3 indices.
    /// </summary>
    /// <returns>The sum of all active model index counts divided by 3.</returns>
    public int TotalTriangleCount()
    {
        int sum = 0;
        for (int i = 0; i < _modelsCount; i++)
        {
            ListInfo li = _models[i];
            if (!li.empty)
                sum += li.indicescount;
        }
        return sum / 3;
    }

    /// <summary>
    /// Removes all active models from the batch, releasing their GPU resources
    /// and resetting the free-list.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _modelsCount; i++)
        {
            if (!_models[i].empty)
                Remove(i);
        }
    }
}

public class ListInfo
{
    public ListInfo()
    {
        render = true;
    }
    internal bool empty;
    internal int indicescount;
    internal float centerX;
    internal float centerY;
    internal float centerZ;
    internal float radius;
    internal bool transparent;
    internal bool render;
    internal int texture;
    internal Model model;
}
