using AdamEve.Core.Rigs;
using AdamEve.Core.World;

namespace AdamEve.Core.Game;

/// <summary>How the ground of one tile kind is painted: two flat colours in a checker, and a square mark.</summary>
/// <param name="Colour">The colour of the tile, 0xRRGGBB.</param>
/// <param name="AlternateColour">The colour of every other tile.</param>
/// <param name="MarkColour">The colour of the mark; -1 for none.</param>
/// <param name="MarkSize">The side of the mark in logical pixels.</param>
public readonly record struct GroundStyle(int Colour, int AlternateColour, int MarkColour, int MarkSize);

/// <summary>
/// The placeholder art of slice S2, made by code: flat colours and two shapes, nothing generated and no image
/// file. The ground is painted from <see cref="GroundOf"/>; a tree is a few shapes about the foot of its trunk.
/// The colours are placeholders until the style sheet and its palette exist (design, section 5.5).
/// </summary>
public static class PlaceholderArt
{
    private const int Grass = 0x7DB46C;
    private const int GrassAlternate = 0x76AD65;
    private const int Water = 0x4C9BC4;
    private const int WaterAlternate = 0x55A3CA;

    /// <summary>The colour behind the map.</summary>
    public const int Backdrop = 0x2A6034;

    /// <summary>How the ground of a tile kind is painted. A tree stands on grass.</summary>
    /// <param name="kind">The kind.</param>
    public static GroundStyle GroundOf(TileKind kind) => kind switch
    {
        TileKind.Water => new GroundStyle(Water, WaterAlternate, -1, 0),
        TileKind.Thicket => new GroundStyle(0x2F6B3A, Backdrop, -1, 0),
        TileKind.Crossing => new GroundStyle(Water, WaterAlternate, 0xB9B4A6, 24),
        TileKind.RestingPlace => new GroundStyle(Grass, GrassAlternate, 0xD8C48A, 26),
        TileKind.Flowers => new GroundStyle(Grass, GrassAlternate, 0xF4E9C8, 6),
        _ => new GroundStyle(Grass, GrassAlternate, -1, 0),
    };

    /// <summary>The shapes of a tree, the farthest first, from the foot of its trunk; empty for a kind that is not a tree.</summary>
    /// <param name="kind">The kind.</param>
    public static IReadOnlyList<FlatShape> SpriteOf(TileKind kind) => kind switch
    {
        TileKind.Tree =>
        [
            new(PartShape.Rectangle, 0, -9, 8, 18, 0x7A5A3A),
            new(PartShape.Ellipse, 0, -34, 42, 38, 0x3E8E4E),
            new(PartShape.Ellipse, -7, -40, 18, 14, 0x57A65B),
        ],
        TileKind.FigTree =>
        [
            new(PartShape.Rectangle, 0, -7, 8, 14, 0x7A5A3A),
            new(PartShape.Ellipse, 0, -26, 50, 30, 0x4E9A4A),
            new(PartShape.Ellipse, -10, -31, 20, 10, 0x6DB36A),
        ],
        // The tree of life: tall, white-gold bark, green-gold leaves, pale blossoms (design, section 5.2).
        TileKind.TreeOfLife =>
        [
            new(PartShape.Rectangle, 0, -14, 10, 28, 0xE9DDB0),
            new(PartShape.Ellipse, 0, -50, 54, 50, 0x9CC25A),
            new(PartShape.Ellipse, -12, -60, 8, 8, 0xFBF3D0),
            new(PartShape.Ellipse, 10, -52, 8, 8, 0xFBF3D0),
            new(PartShape.Ellipse, -4, -40, 8, 8, 0xFBF3D0),
        ],
        // The tree of the knowledge of good and evil: dark silver bark, leaves silver and nearly black. No fruit
        // is drawn in this slice.
        TileKind.TreeOfKnowledge =>
        [
            new(PartShape.Rectangle, 0, -13, 10, 26, 0x8E949B),
            new(PartShape.Ellipse, 0, -46, 50, 46, 0x23272E),
            new(PartShape.Ellipse, -9, -54, 16, 10, 0xC5CBD3),
            new(PartShape.Ellipse, 11, -42, 12, 8, 0xC5CBD3),
        ],
        _ => [],
    };
}
