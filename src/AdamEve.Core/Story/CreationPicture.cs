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
/// The picture of the days of creation, as art made by code: which layers a day shows at each of its cards, and
/// the shapes of each layer. A day's picture shows nothing of the day before the player's gesture,
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

    /// <summary>
    /// The shapes of a layer, the farthest first, in the units of the picture: outlines and fills made by code
    /// (<see cref="PictureShape"/>). The figures have none: they are rigs.
    /// </summary>
    /// <param name="layer">The layer.</param>
    public static IReadOnlyList<PictureShape> ShapesOf(SceneLayer layer) => CreationArt.Of(layer);
}
