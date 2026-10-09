namespace AdamEve.Core.Story;

/// <summary>One day of creation: a beat of the story.</summary>
/// <param name="Beat">The beat.</param>
/// <param name="Cards">The verses of the day, one Scripture card each, in order.</param>
/// <param name="RevealAt">
/// The card the one gesture of the day uncovers: the first of the day on which God speaks. -1 for the seventh day,
/// which has no gesture.
/// </param>
/// <param name="Voice">The verses of the day in which God speaks.</param>
public sealed record CreationDay(StoryBeat Beat, IReadOnlyList<ScriptureRef> Cards, int RevealAt, IReadOnlyList<ScriptureRef> Voice)
{
    /// <summary>The card shown when the day begins: none when the day begins with its gesture.</summary>
    public int FirstCard => RevealAt == 0 ? -1 : 0;
}

/// <summary>
/// The days of creation as the design maps them to the text (section 2.2, beats B1 to B7): which verses each day
/// shows, and where the player's one gesture of the day falls. References only: the text of a verse comes from the
/// canonical file through the parser.
/// </summary>
public static class CreationStory
{
    /// <summary>The seven days, in order.</summary>
    public static IReadOnlyList<CreationDay> Days { get; } =
    [
        Day(StoryBeat.B1, 1, 1, 5, revealAt: 3, voice: [3]),
        Day(StoryBeat.B2, 1, 6, 8, revealAt: 6, voice: [6]),
        Day(StoryBeat.B3, 1, 9, 13, revealAt: 9, voice: [9, 11]),
        Day(StoryBeat.B4, 1, 14, 19, revealAt: 14, voice: [14, 15]),
        Day(StoryBeat.B5, 1, 20, 23, revealAt: 20, voice: [20, 22]),
        Day(StoryBeat.B6, 1, 24, 31, revealAt: 24, voice: [24, 26, 28, 29, 30]),
        Day(StoryBeat.B7, 2, 1, 3, revealAt: null, voice: []),
    ];

    /// <summary>Every verse the days show, in order: Genesis 1:1 to 2:3.</summary>
    public static IReadOnlyList<ScriptureRef> Cards { get; } = [.. Days.SelectMany(day => day.Cards)];

    /// <summary>The day of a beat, or null for a beat that is not a day of creation.</summary>
    /// <param name="beat">The beat.</param>
    public static CreationDay? DayOf(StoryBeat beat) => Days.FirstOrDefault(day => day.Beat == beat);

    private static CreationDay Day(StoryBeat beat, int chapter, int first, int last, int? revealAt, int[] voice) => new(
        beat,
        [.. Enumerable.Range(first, last - first + 1).Select(verse => new ScriptureRef(chapter, verse))],
        revealAt is null ? -1 : revealAt.Value - first,
        [.. voice.Select(verse => new ScriptureRef(chapter, verse))]);
}
