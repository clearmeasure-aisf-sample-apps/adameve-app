namespace AdamEve.Core.Story;

/// <summary>
/// The story machine (design, section 3.1): a pure function from a state and an event to the next state and what
/// the game does. This slice holds beats B0 to B7: the title, and the seven days of creation.
/// <para>
/// In a day of creation the player does two things only: turn the page to the next verse, and, once a day from the
/// first to the sixth, make the gesture that uncovers what the next verse tells. That verse is always one in which
/// God speaks. No event makes anything: the player reveals what God made.
/// </para>
/// </summary>
public static class StoryMachine
{
    private static readonly IReadOnlyList<StoryEffect> Nothing = [];

    /// <summary>Applies an event. An event that does not fit the state is refused: the same state, no effect.</summary>
    /// <param name="state">The state.</param>
    /// <param name="storyEvent">The event.</param>
    public static StoryStep Apply(StoryState state, StoryEvent storyEvent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(storyEvent);
        return (state.Chapter, storyEvent) switch
        {
            (StoryChapter.Title, CharacterChosen chosen) when Enum.IsDefined(chosen.Character) =>
                Begin(state with { Character = chosen.Character, Chapter = StoryChapter.Creation }, CreationStory.Days[0]),
            (StoryChapter.Creation, DialogueAdvanced) => TurnThePage(state),
            (StoryChapter.Creation, Revealed) when AwaitsReveal(state) => Shown(state with { Card = state.Card + 1 }, save: false),
            (StoryChapter.Creation, ChoiceMade { Choice: StoryChoice.SkipCreation }) when state.CreationWatched => LeaveCreation(state),
            _ => new StoryStep(state, Nothing),
        };
    }

    /// <summary>What shows a state again, as after a reload: a beat is resumed from its beginning, and nothing is saved.</summary>
    /// <param name="state">The state.</param>
    public static StoryStep Resume(StoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Chapter == StoryChapter.Creation && CreationStory.DayOf(state.Beat) is { } day
            ? Shown(state with { Card = day.FirstCard }, save: false)
            : new StoryStep(state with { Card = -1 }, Nothing);
    }

    /// <summary>Whether the story waits for the one gesture of the day.</summary>
    /// <param name="state">The state.</param>
    public static bool AwaitsReveal(StoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Chapter == StoryChapter.Creation && CreationStory.DayOf(state.Beat) is { } day && day.RevealAt == state.Card + 1;
    }

    /// <summary>Whether a beat belongs to a chapter in which a game may be saved.</summary>
    /// <param name="chapter">The chapter.</param>
    /// <param name="beat">The beat.</param>
    public static bool IsBeatOf(StoryChapter chapter, StoryBeat beat) => chapter switch
    {
        StoryChapter.Creation => CreationStory.DayOf(beat) is not null,
        StoryChapter.Formation => beat == StoryBeat.B8,
        _ => false,
    };

    private static StoryStep TurnThePage(StoryState state)
    {
        if (CreationStory.DayOf(state.Beat) is not { } day || state.Card < 0 || AwaitsReveal(state))
        {
            return new StoryStep(state, Nothing);
        }

        if (state.Card < day.Cards.Count - 1)
        {
            return Shown(state with { Card = state.Card + 1 }, save: false);
        }

        var next = CreationStory.DayOf(state.Beat + 1);
        return next is null ? LeaveCreation(state) : Begin(state, next);
    }

    private static StoryStep Begin(StoryState state, CreationDay day) => Shown(state with { Beat = day.Beat, Card = day.FirstCard }, save: true);

    // The days of creation are over, watched or skipped: the story stands at the beat after them.
    private static StoryStep LeaveCreation(StoryState state) => new(
        state with { Chapter = StoryChapter.Formation, Beat = StoryBeat.B8, Card = -1, CreationWatched = true },
        [new SaveStory()]);

    private static StoryStep Shown(StoryState state, bool save)
    {
        var day = CreationStory.DayOf(state.Beat)!;
        var effects = new List<StoryEffect> { new ShowScene(CreationPicture.Layers(state.Beat, state.Card)) };
        if (state.Card >= 0)
        {
            var verse = day.Cards[state.Card];
            effects.Add(new ShowScripture(verse, day.Voice.Contains(verse) ? StorySpeaker.God : null));
        }

        if (AwaitsReveal(state))
        {
            effects.Add(new AwaitReveal());
        }

        if (state.CreationWatched)
        {
            effects.Add(new OfferChoices([StoryChoice.SkipCreation]));
        }

        if (save)
        {
            effects.Add(new SaveStory());
        }

        return new StoryStep(state, effects);
    }
}
