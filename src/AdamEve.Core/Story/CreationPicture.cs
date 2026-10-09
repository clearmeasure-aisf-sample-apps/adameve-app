using AdamEve.Core.Rigs;

namespace AdamEve.Core.Story;

/// <summary>
/// A layer of the picture of the days of creation. Each is what a verse tells; none is a figure of God, who is never
/// shown as one (design, decision D5): his presence is <see cref="Presence"/>, a light.
/// </summary>
public enum SceneLayer
{
    /// <summary>Darkness (Genesis 1:2).</summary>
    Darkness,

    /// <summary>The waters (1:2, 1:6).</summary>
    Waters,

    /// <summary>The deep, in darkness (1:2).</summary>
    Deep,

    /// <summary>The firmament in the midst of the waters (1:6 to 1:8).</summary>
    Firmament,

    /// <summary>The light (1:3).</summary>
    Light,

    /// <summary>The light divided from the darkness (1:4, 1:5).</summary>
    Day,

    /// <summary>The stars (1:16).</summary>
    Stars,

    /// <summary>The greater light (1:16).</summary>
    Sun,

    /// <summary>The lesser light (1:16).</summary>
    Moon,

    /// <summary>The dry land (1:9, 1:10).</summary>
    Land,

    /// <summary>Grass and herbs (1:11, 1:12).</summary>
    Grass,

    /// <summary>Fruit trees (1:11, 1:12).</summary>
    Trees,

    /// <summary>Great whales (1:21).</summary>
    Whales,

    /// <summary>What the waters brought forth (1:20, 1:21).</summary>
    Fish,

    /// <summary>Fowl (1:20, 1:21).</summary>
    Birds,

    /// <summary>The living creatures of the earth (1:24, 1:25).</summary>
    Animals,

    /// <summary>Two figures of light, far away, drawn from the rigs under rule M1 (1:26 to 1:28).</summary>
    Figures,

    /// <summary>The presence of God: a warm, moving light, shown while his voice speaks. Never a figure.</summary>
    Presence,
}

/// <summary>
/// The picture of the days of creation, as placeholder art made by code: which layers a day shows at each of its
/// cards, and the flat shapes of each layer. A day's picture shows nothing of the day before the player's gesture,
/// and after it only what the verses shown so far tell.
/// </summary>
public static class CreationPicture
{
    /// <summary>The width of the picture in its own units.</summary>
    public const int Width = 320;

    /// <summary>The height of the picture in its own units.</summary>
    public const int Height = 200;

    /// <summary>The colour of the two figures of light.</summary>
    public const int FigureLight = 0xFFF6D8;

    private static readonly SceneLayer[] Sky = [SceneLayer.Waters, SceneLayer.Firmament];
    private static readonly SceneLayer[] Earth = [.. Sky, SceneLayer.Land, SceneLayer.Grass, SceneLayer.Trees];
    private static readonly SceneLayer[] Lights = [.. Earth, SceneLayer.Stars, SceneLayer.Sun, SceneLayer.Moon];
    private static readonly SceneLayer[] Life = [.. Lights, SceneLayer.Whales, SceneLayer.Fish, SceneLayer.Birds];

    /// <summary>The layers a beat shows at a card, the farthest first.</summary>
    /// <param name="beat">The beat: a day of creation.</param>
    /// <param name="card">The card shown, from 0; -1 before the first.</param>
    public static IReadOnlyList<SceneLayer> Layers(StoryBeat beat, int card)
    {
        SceneLayer[] layers = beat switch
        {
            StoryBeat.B1 when card <= 1 => [SceneLayer.Darkness, SceneLayer.Deep],
            StoryBeat.B1 when card == 2 => [SceneLayer.Darkness, SceneLayer.Deep, SceneLayer.Light],
            StoryBeat.B1 => [SceneLayer.Darkness, SceneLayer.Deep, SceneLayer.Day],
            StoryBeat.B2 when card < 0 => [SceneLayer.Waters],
            StoryBeat.B2 => Sky,
            StoryBeat.B3 when card < 0 => Sky,
            StoryBeat.B3 when card <= 1 => [.. Sky, SceneLayer.Land],
            StoryBeat.B3 => Earth,
            StoryBeat.B4 when card < 0 => Earth,
            StoryBeat.B4 when card == 0 => [.. Earth, SceneLayer.Sun],
            StoryBeat.B4 when card == 1 => [.. Earth, SceneLayer.Sun, SceneLayer.Moon],
            StoryBeat.B4 => Lights,
            StoryBeat.B5 when card < 0 => Lights,
            StoryBeat.B5 when card == 0 => [.. Lights, SceneLayer.Fish, SceneLayer.Birds],
            StoryBeat.B5 => Life,
            StoryBeat.B6 when card < 0 => Life,
            StoryBeat.B6 when card is >= 2 and <= 4 => [.. Life, SceneLayer.Animals, SceneLayer.Figures],
            StoryBeat.B6 => [.. Life, SceneLayer.Animals],
            StoryBeat.B7 => [.. Life, SceneLayer.Animals],
            _ => [],
        };

        var speaks = card >= 0 && CreationStory.DayOf(beat) is { } day && card < day.Cards.Count && day.Voice.Contains(day.Cards[card]);
        return [.. (speaks ? [.. layers, SceneLayer.Presence] : layers).Order()];
    }

    /// <summary>The flat shapes of a layer, the farthest first, in the units of the picture. The figures have none: they are rigs.</summary>
    /// <param name="layer">The layer.</param>
    public static IReadOnlyList<FlatShape> ShapesOf(SceneLayer layer) => layer switch
    {
        SceneLayer.Darkness => [new(PartShape.Rectangle, 160, 100, 320, 200, 0x0B0E1A)],
        SceneLayer.Waters => [new(PartShape.Rectangle, 160, 100, 320, 200, 0x2E6C9E)],
        SceneLayer.Deep => [new(PartShape.Rectangle, 160, 165, 320, 70, 0x111C36)],
        SceneLayer.Firmament => [new(PartShape.Rectangle, 160, 90, 320, 100, 0xBFE3F5)],
        SceneLayer.Light => [new(PartShape.Ellipse, 160, 95, 280, 170, 0xFFF4C8)],
        SceneLayer.Day => [new(PartShape.Rectangle, 80, 100, 160, 200, 0xFFF4C8)],
        SceneLayer.Stars =>
        [
            new(PartShape.Ellipse, 30, 52, 4, 4, 0xFFFFFF),
            new(PartShape.Ellipse, 112, 50, 4, 4, 0xFFFFFF),
            new(PartShape.Ellipse, 150, 72, 4, 4, 0xFFFFFF),
            new(PartShape.Ellipse, 200, 48, 4, 4, 0xFFFFFF),
            new(PartShape.Ellipse, 292, 56, 4, 4, 0xFFFFFF),
            new(PartShape.Ellipse, 232, 104, 4, 4, 0xFFFFFF),
        ],
        SceneLayer.Sun => [new(PartShape.Ellipse, 255, 72, 34, 34, 0xF6C445)],
        SceneLayer.Moon => [new(PartShape.Ellipse, 70, 64, 22, 22, 0xEEF1F4)],
        SceneLayer.Land => [new(PartShape.Ellipse, 105, 178, 250, 96, 0xB08A55)],
        SceneLayer.Grass =>
        [
            new(PartShape.Ellipse, 105, 172, 228, 70, 0x7DB46C),
            new(PartShape.Ellipse, 40, 150, 10, 8, 0x57A65B),
            new(PartShape.Ellipse, 150, 158, 10, 8, 0x57A65B),
        ],
        SceneLayer.Trees =>
        [
            new(PartShape.Rectangle, 58, 140, 6, 18, 0x7A5A3A),
            new(PartShape.Ellipse, 58, 124, 30, 26, 0x3E8E4E),
            new(PartShape.Rectangle, 118, 136, 6, 18, 0x7A5A3A),
            new(PartShape.Ellipse, 118, 120, 30, 26, 0x4E9A4A),
            new(PartShape.Rectangle, 22, 150, 5, 14, 0x7A5A3A),
            new(PartShape.Ellipse, 22, 138, 22, 18, 0x3E8E4E),
        ],
        SceneLayer.Whales =>
        [
            new(PartShape.Ellipse, 268, 186, 56, 20, 0x3B4F6B),
            new(PartShape.Ellipse, 298, 180, 10, 16, 0x3B4F6B),
        ],
        SceneLayer.Fish =>
        [
            new(PartShape.Ellipse, 252, 162, 14, 6, 0xE8963A),
            new(PartShape.Ellipse, 284, 168, 14, 6, 0xE8963A),
            new(PartShape.Ellipse, 304, 156, 12, 5, 0xE8963A),
        ],
        SceneLayer.Birds =>
        [
            new(PartShape.Ellipse, 180, 62, 12, 4, 0x3A3A3A),
            new(PartShape.Ellipse, 206, 54, 12, 4, 0x3A3A3A),
            new(PartShape.Ellipse, 226, 68, 12, 4, 0x3A3A3A),
        ],
        SceneLayer.Animals =>
        [
            new(PartShape.Ellipse, 70, 166, 30, 18, 0xB9A27C),
            new(PartShape.Ellipse, 86, 157, 12, 12, 0xB9A27C),
            new(PartShape.Ellipse, 142, 172, 26, 16, 0x9A9A9A),
            new(PartShape.Ellipse, 128, 165, 11, 11, 0x9A9A9A),
            new(PartShape.Ellipse, 104, 154, 16, 10, 0xD8C7A3),
        ],
        SceneLayer.Presence =>
        [
            new(PartShape.Ellipse, 160, 34, 170, 64, 0xFFD98A),
            new(PartShape.Ellipse, 160, 34, 110, 42, 0xFFE9B0),
            new(PartShape.Ellipse, 160, 34, 56, 22, 0xFFF6D8),
        ],
        _ => [],
    };
}
