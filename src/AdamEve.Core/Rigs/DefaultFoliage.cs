namespace AdamEve.Core.Rigs;

/// <summary>One flat shape of placeholder art, placed about an anchor.</summary>
/// <param name="Shape">The outline.</param>
/// <param name="X">The centre, from the anchor.</param>
/// <param name="Y">The centre, from the anchor; y grows downward.</param>
/// <param name="Width">The width in logical pixels.</param>
/// <param name="Height">The height in logical pixels.</param>
/// <param name="Colour">The colour, 0xRRGGBB. A shape is opaque.</param>
public readonly record struct FlatShape(PartShape Shape, double X, double Y, double Width, double Height, int Colour);

/// <summary>
/// The full default foliage cluster (design, section 1, "fail closed"): what the renderer draws in front of a
/// character whose frame has no valid concealment record, or whose zones are not all covered. It is anchored at
/// the feet and hides the figure from the shoulders to the shins, whatever the rig says.
/// </summary>
public static class DefaultFoliage
{
    /// <summary>The shapes of the cluster, the farthest first, from the feet of the character.</summary>
    public static IReadOnlyList<FlatShape> Shapes { get; } =
    [
        new(PartShape.Ellipse, 0, -19, 38, 34, 0x2F7A44),
        new(PartShape.Ellipse, -9, -15, 24, 26, 0x3E8E4E),
        new(PartShape.Ellipse, 9, -15, 24, 26, 0x57A65B),
    ];
}
