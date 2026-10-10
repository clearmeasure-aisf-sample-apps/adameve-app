namespace AdamEve.Core.Rigs;

/// <summary>One flat shape of placeholder art, placed about an anchor.</summary>
/// <param name="Shape">The outline.</param>
/// <param name="X">The centre, from the anchor.</param>
/// <param name="Y">The centre, from the anchor; y grows downward.</param>
/// <param name="Width">The width in logical pixels.</param>
/// <param name="Height">The height in logical pixels.</param>
/// <param name="Colour">The colour, 0xRRGGBB. A shape is opaque.</param>
public readonly record struct FlatShape(PartShape Shape, double X, double Y, double Width, double Height, int Colour);
