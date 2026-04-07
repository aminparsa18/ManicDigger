/// <summary>
/// Stores the value of one animated property for a specific node
/// at a specific point in time within a named animation.
/// </summary>
public class Keyframe
{
    /// <summary>The animation this keyframe belongs to.</summary>
    internal string AnimationName;

    /// <summary>The node (bone) this keyframe affects.</summary>
    internal string NodeName;

    /// <summary>Time position within the animation, measured in frames.</summary>
    internal int Frame;

    /// <summary>The property being animated. See <see cref="KeyframeType"/>.</summary>
    internal KeyframeType Type;

    /// <summary>X component of the keyed value.</summary>
    internal float X;

    /// <summary>Y component of the keyed value.</summary>
    internal float Y;

    /// <summary>Z component of the keyed value.</summary>
    internal float Z;
}