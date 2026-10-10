using AdamEve.Core.Saves;

namespace AdamEve.Core.Story;

/// <summary>
/// The story machine (design, section 3.1): a pure function from a state and an event to the next state and what
/// the game does. It holds beats B0 to B7 (the title, and the seven days of creation) and, on the man's path,
/// beats B8 to B13 (<see cref="GardenStory"/>): the formation, the garden, the command and the naming of the animals.
/// <para>
/// In a day of creation the player does two things only: turn the page to the next verse, and, once a day from the
/// first to the sixth, make the gesture that uncovers what the next verse tells. That verse is always one in which
/// God speaks. No event makes anything: the player reveals what God made.
/// </para>
/// <para>
/// In a beat of the garden the player turns the page of a Scripture card, walks to a place, or gives the animal
/// that was brought one of its three kind-names. The command is two cards and no choice. What the game calls the
/// man changes with the card of Genesis 2:19 and not before.
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
            (_, DialogueAdvanced) when GardenStory.StepOf(state) is { Kind: GardenStepKind.Card } => Next(state),
            (_, ReachedArea reached) when GardenStory.StepOf(state) is { Kind: GardenStepKind.Reach } step && step.Area == reached.Area => Next(state),
            (_, AnimalNamed named) when GardenStory.StepOf(state) is { Kind: GardenStepKind.Naming } => Name(state, named.KindName),
            _ => new StoryStep(state, Nothing),
        };
    }

    /// <summary>What shows a state again, as after a reload: a beat is resumed from its beginning, and nothing is saved.</summary>
    /// <param name="state">The state.</param>
    public static StoryStep Resume(StoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (PlaysTheGarden(state))
        {
            return ShownInTheGarden(state with { Card = 0, Man = ManAt(state.Beat) }, save: false);
        }

        return state.Chapter == StoryChapter.Creation && CreationStory.DayOf(state.Beat) is { } day
            ? Shown(state with { Card = day.FirstCard }, save: false)
            : new StoryStep(state with { Card = -1 }, Nothing);
    }

    /// <summary>
    /// Whether a state stands in a beat of the garden that is played: the man's path, beats B8 to B13. The woman's
    /// path of these beats is not built yet.
    /// </summary>
    /// <param name="state">The state.</param>
    public static bool PlaysTheGarden(StoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Character == PlayerCharacter.Adam && GardenStory.BeatOf(state.Beat) is { } beat && beat.Chapter == state.Chapter;
    }

    /// <summary>What the game calls the man in a beat (design, decision D3): "Adam" from the beat that shows Genesis 2:19.</summary>
    /// <param name="beat">The beat.</param>
    public static ManLabel ManAt(StoryBeat beat) => beat >= StoryBeat.B13 ? ManLabel.Adam : ManLabel.TheMan;

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
        StoryChapter.Title => false,
        StoryChapter.Creation => CreationStory.DayOf(beat) is not null,
        StoryChapter.WomanFormed => beat == StoryBeat.B14,
        StoryChapter.GardenLife => beat == StoryBeat.B17,
        _ => GardenStory.BeatOf(beat) is { } garden && garden.Chapter == chapter,
    };

    /// <summary>
    /// Whether a saved game can stand where it says: its beat is one of its chapter, and, as far as the story is
    /// built, only the man stands in the beats after the formation that come before life in the garden.
    /// </summary>
    /// <param name="character">Whom the player plays.</param>
    /// <param name="chapter">The chapter.</param>
    /// <param name="beat">The beat.</param>
    public static bool CanStandAt(PlayerCharacter character, StoryChapter chapter, StoryBeat beat) =>
        IsBeatOf(chapter, beat)
        && (character == PlayerCharacter.Adam || chapter is StoryChapter.Creation or StoryChapter.Formation or StoryChapter.GardenLife);

    // The next step of the beat, or the first of the next beat; after the last beat built, the walk in the garden.
    private static StoryStep Next(StoryState state)
    {
        var beat = GardenStory.BeatOf(state.Beat)!;
        if (state.Card < beat.Steps.Count - 1)
        {
            return ShownInTheGarden(state with { Card = state.Card + 1 }, save: false);
        }

        return GardenStory.BeatOf(state.Beat + 1) is { } next
            ? ShownInTheGarden(state with { Chapter = next.Chapter, Beat = next.Beat, Card = 0, Man = ManAt(next.Beat) }, save: true)
            : new StoryStep(state with { Chapter = StoryChapter.WomanFormed, Beat = StoryBeat.B14, Card = -1 }, [new SaveStory()]);
    }

    // Whatsoever Adam called every living creature, that was the name thereof: any of the three is accepted.
    private static StoryStep Name(StoryState state, string kindName)
    {
        var count = state.NamedAnimals.Count;
        if (count >= AnimalRoster.Count || !AnimalRoster.Animals[count].KindNames.Contains(kindName))
        {
            return new StoryStep(state, Nothing);
        }

        var named = state with { NamedAnimals = [.. state.NamedAnimals, kindName] };
        var after = ShownInTheGarden(named, save: true);
        return new StoryStep(after.State, [new AddJournal(AnimalRoster.Animals[count].Id, kindName), .. after.Effects]);
    }

    private static StoryStep ShownInTheGarden(StoryState state, bool save)
    {
        var step = GardenStory.BeatOf(state.Beat)!.Steps[state.Card];
        var effects = new List<StoryEffect>();
        switch (step.Kind)
        {
            case GardenStepKind.Card:
                if (state.Beat == StoryBeat.B8)
                {
                    effects.Add(new ShowFormation(FormationPicture.Layers(state.Card)));
                }

                effects.Add(new ShowScripture(step.Ref, step.Speaker));
                if (step.Ref == GardenStory.AdamNamed)
                {
                    effects.Add(new SetLabel(ManLabel.Adam));
                }

                if (step.Ref == GardenStory.NoHelpMeetFound)
                {
                    effects.Add(new ShowPairs());
                }

                break;
            case GardenStepKind.Reach:
                effects.Add(new ShowTask(step.Task));
                break;
            case GardenStepKind.Naming when state.NamedAnimals.Count < AnimalRoster.Count:
                var animal = AnimalRoster.Animals[state.NamedAnimals.Count];
                effects.Add(new BringAnimal(animal.Id, animal.KindNames));
                break;
            default:
                // Every animal has its name: the story goes on.
                var next = Next(state);
                return save && !next.Effects.OfType<SaveStory>().Any() ? new StoryStep(next.State, [.. next.Effects, new SaveStory()]) : next;
        }

        if (save)
        {
            effects.Add(new SaveStory());
        }

        return new StoryStep(state, effects);
    }

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
