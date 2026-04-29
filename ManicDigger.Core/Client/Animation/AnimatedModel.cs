/// <summary>
/// Represents a complete animated model, containing the node hierarchy,
/// keyframe data, animation definitions, and texture metadata.
/// </summary>
public class AnimatedModel
{
    /// <summary>
    /// Initializes a new empty <see cref="AnimatedModel"/> with
    /// pre-allocated lists ready for deserialization.
    /// </summary>
    public AnimatedModel()
    {
        nodes = [];
        Keyframes = [];
        Animations = [];
        Global = new AnimationGlobal();
    }

    /// <summary>The node (bone) hierarchy that makes up the model's skeleton.</summary>
    internal List<Node> nodes;

    /// <summary>All keyframes across all animations and nodes.</summary>
    internal List<Keyframe> Keyframes;

    /// <summary>The set of named animations defined for this model.</summary>
    internal List<Animation> Animations;

    /// <summary>Texture dimensions shared across all nodes in this model.</summary>
    internal AnimationGlobal Global;
}