/// <summary>
/// Represents a single node (bone) in the animated model hierarchy.
/// Nodes are arranged in a parent-child tree; each node's transform
/// is applied relative to its parent's transform.
/// Default values for all transform properties are used when no
/// keyframe data overrides them for a given animation frame.
/// </summary>
public class Node
{
    /// <summary>Unique name identifying this node within the model.</summary>
    internal string Name;

    /// <summary>
    /// Name of the parent node. Use <c>"root"</c> for top-level nodes
    /// that have no parent.
    /// </summary>
    internal string ParentName;

    /// <summary>Default local position X offset, in model units.</summary>
    internal float PosX;

    /// <summary>Default local position Y offset, in model units.</summary>
    internal float PosY;

    /// <summary>Default local position Z offset, in model units.</summary>
    internal float PosZ;

    /// <summary>Default rotation around the X axis, in degrees.</summary>
    internal float RotateX;

    /// <summary>Default rotation around the Y axis, in degrees.</summary>
    internal float RotateY;

    /// <summary>Default rotation around the Z axis, in degrees.</summary>
    internal float RotateZ;

    /// <summary>Default cuboid width (X dimension), in pixels on the texture net.</summary>
    internal float SizeX;

    /// <summary>Default cuboid height (Y dimension), in pixels on the texture net.</summary>
    internal float SizeY;

    /// <summary>Default cuboid depth (Z dimension), in pixels on the texture net.</summary>
    internal float SizeZ;

    /// <summary>Horizontal start position of this node's texture region, in pixels.</summary>
    internal float U;

    /// <summary>Vertical start position of this node's texture region, in pixels.</summary>
    internal float V;

    /// <summary>Pivot point X offset, used as the center of rotation, in model units.</summary>
    internal float PivotX;

    /// <summary>Pivot point Y offset, used as the center of rotation, in model units.</summary>
    internal float PivotY;

    /// <summary>Pivot point Z offset, used as the center of rotation, in model units.</summary>
    internal float PivotZ;

    /// <summary>Default scale multiplier along the X axis.</summary>
    internal float ScaleX;

    /// <summary>Default scale multiplier along the Y axis.</summary>
    internal float ScaleY;

    /// <summary>Default scale multiplier along the Z axis.</summary>
    internal float ScaleZ;

    /// <summary>
    /// When set to <c>1</c>, this node rotates with the player's head pitch,
    /// allowing it to look up and down independently of the body.
    /// </summary>
    internal float Head;
}