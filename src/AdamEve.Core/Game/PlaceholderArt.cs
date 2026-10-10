using AdamEve.Core.Rigs;
using AdamEve.Core.World;

namespace AdamEve.Core.Game;

/// <summary>How the ground of one tile kind is painted: between two flat colours, and a mark.</summary>
/// <param name="Colour">The colour of the tile, 0xRRGGBB.</param>
/// <param name="AlternateColour">The other colour the ground of the kind shades into.</param>
/// <param name="MarkColour">The colour of the mark; -1 for none.</param>
/// <param name="MarkSize">The side of the mark in logical pixels.</param>
public readonly record struct GroundStyle(int Colour, int AlternateColour, int MarkColour, int MarkSize);

/// <summary>
/// The art of the garden that is data, made by code: the colours of the ground, the colours of the drifts of
/// flowers, and for everything that stands on a tile a few flat shapes about its foot. The canvas renderer paints
/// exactly these; the Three.js renderer takes the colours and the kinds and builds its own solids. Nothing is
/// generated and there is no image file.
/// </summary>
public static class PlaceholderArt
{
    private const int Grass = 0x8DBF5F;
    private const int GrassDeep = 0x5FA058;
    private const int Water = 0x3F9BB5;
    private const int WaterLight = 0x67BCC6;
    private const int Bark = 0x7A5A3A;

    /// <summary>The colour behind the map.</summary>
    public const int Backdrop = 0x2A6034;

    /// <summary>The colours of the drifts of flowers, by their number less one (<see cref="GardenScenery.CoverAt"/>).</summary>
    public static IReadOnlyList<int> DriftColours { get; } = [0xFFF6DC, 0xF7C948, 0xF08A7B, 0xB78BE0, 0x8CC7F0];

    /// <summary>
    /// How the ground of a tile kind is painted: between two colours that change smoothly from place to place (no
    /// checker), with a mark for the stones of a crossing and for the resting place. A tree stands on grass.
    /// </summary>
    /// <param name="kind">The kind.</param>
    public static GroundStyle GroundOf(TileKind kind) => kind switch
    {
        TileKind.Water => new GroundStyle(Water, WaterLight, -1, 0),
        TileKind.Thicket => new GroundStyle(0x2F6B3A, 0x24573A, -1, 0),
        TileKind.Crossing => new GroundStyle(Water, WaterLight, 0xCBC2AE, 24),
        TileKind.RestingPlace => new GroundStyle(Grass, GrassDeep, 0xE4D29C, 26),
        _ => new GroundStyle(Grass, GrassDeep, -1, 0),
    };

    /// <summary>The flat shapes of what stands on a tile, the farthest first, from its foot; empty for nothing.</summary>
    /// <param name="kind">The kind.</param>
    public static IReadOnlyList<FlatShape> SpriteOf(SceneryKind kind) => kind switch
    {
        SceneryKind.BroadTree =>
        [
            new(PartShape.Rectangle, 0, -11, 8, 22, Bark),
            new(PartShape.Ellipse, 0, -44, 58, 46, 0x3F8F4F),
            new(PartShape.Ellipse, -10, -52, 26, 18, 0x63AC58),
            new(PartShape.Ellipse, 12, -40, 20, 14, 0x55A257),
        ],
        SceneryKind.TallTree =>
        [
            new(PartShape.Rectangle, 0, -7, 6, 14, Bark),
            new(PartShape.Ellipse, 0, -48, 26, 76, 0x2F7B4F),
            new(PartShape.Ellipse, -3, -58, 12, 40, 0x3F9058),
        ],
        SceneryKind.FruitTree =>
        [
            new(PartShape.Rectangle, 0, -9, 7, 18, Bark),
            new(PartShape.Ellipse, 0, -34, 46, 38, 0x4E9F4C),
            new(PartShape.Ellipse, -8, -41, 20, 14, 0x74B95C),
            new(PartShape.Ellipse, -12, -30, 5, 5, 0xF29A3E),
            new(PartShape.Ellipse, 4, -40, 5, 5, 0xF29A3E),
            new(PartShape.Ellipse, 13, -28, 5, 5, 0xF29A3E),
            new(PartShape.Ellipse, -1, -24, 5, 5, 0xF29A3E),
        ],
        SceneryKind.PalmTree =>
        [
            new(PartShape.Rectangle, 0, -30, 5, 60, 0x9A7B55),
            new(PartShape.Ellipse, -15, -60, 34, 10, 0x4F9A52),
            new(PartShape.Ellipse, 15, -60, 34, 10, 0x4F9A52),
            new(PartShape.Ellipse, 0, -67, 14, 22, 0x6DB85F),
            new(PartShape.Ellipse, -10, -52, 22, 9, 0x3F8A4D),
            new(PartShape.Ellipse, 10, -52, 22, 9, 0x3F8A4D),
        ],
        SceneryKind.FigTree =>
        [
            new(PartShape.Rectangle, 0, -7, 8, 14, Bark),
            new(PartShape.Ellipse, 0, -27, 56, 32, 0x4E9A4A),
            new(PartShape.Ellipse, -11, -33, 22, 11, 0x6DB36A),
        ],
        // The tree of life: tall, white-gold bark, green-gold leaves, pale blossoms (design, section 5.2).
        SceneryKind.TreeOfLife =>
        [
            new(PartShape.Rectangle, 0, -24, 12, 48, 0xE9DDB0),
            new(PartShape.Ellipse, 0, -80, 78, 74, 0x9CC25A),
            new(PartShape.Ellipse, -10, -96, 36, 26, 0xB9D772),
            new(PartShape.Ellipse, -20, -92, 8, 8, 0xFBF3D0),
            new(PartShape.Ellipse, 16, -84, 8, 8, 0xFBF3D0),
            new(PartShape.Ellipse, -6, -66, 8, 8, 0xFBF3D0),
            new(PartShape.Ellipse, 24, -64, 8, 8, 0xFBF3D0),
        ],
        // The tree of the knowledge of good and evil: dark silver bark, leaves silver and nearly black. No fruit
        // is drawn in this slice.
        SceneryKind.TreeOfKnowledge =>
        [
            new(PartShape.Rectangle, 0, -18, 12, 36, 0x8E949B),
            new(PartShape.Ellipse, 0, -62, 80, 54, 0x23272E),
            new(PartShape.Ellipse, -16, -72, 26, 12, 0xC5CBD3),
            new(PartShape.Ellipse, 18, -56, 20, 10, 0xC5CBD3),
            new(PartShape.Ellipse, 2, -80, 16, 8, 0x9BA3AE),
        ],
        SceneryKind.ForestTree =>
        [
            new(PartShape.Rectangle, 0, -8, 7, 16, 0x5E4630),
            new(PartShape.Ellipse, 0, -40, 44, 56, 0x2C6B40),
        ],
        SceneryKind.Shrub =>
        [
            new(PartShape.Ellipse, 0, -9, 30, 18, 0x3F8A47),
            new(PartShape.Ellipse, -7, -13, 5, 5, 0xF6B7C9),
            new(PartShape.Ellipse, 5, -11, 5, 5, 0xF6B7C9),
            new(PartShape.Ellipse, 0, -5, 5, 5, 0xFFF6DC),
        ],
        SceneryKind.Rock =>
        [
            new(PartShape.Ellipse, 0, -6, 24, 13, 0x9C978A),
            new(PartShape.Ellipse, -2, -10, 14, 5, 0x6E9C55),
        ],
        SceneryKind.Reeds =>
        [
            new(PartShape.Rectangle, -5, -9, 2, 18, 0x6F9A4E),
            new(PartShape.Rectangle, 0, -11, 2, 22, 0x86AE58),
            new(PartShape.Rectangle, 5, -8, 2, 16, 0x6F9A4E),
        ],
        _ => [],
    };
}
