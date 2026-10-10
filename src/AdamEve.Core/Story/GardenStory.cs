namespace AdamEve.Core.Story;

/// <summary>A place of the garden the story waits for the player to come to.</summary>
public enum StoryArea
{
    /// <summary>The bank of the river.</summary>
    River,

    /// <summary>The sapling.</summary>
    Sapling,

    /// <summary>The fallen branch.</summary>
    Branch,
}

/// <summary>What the player is to do, said in one line of narration (design, sections 2.2 and 3.4).</summary>
public enum StoryTask
{
    /// <summary>Walk to the river (beat B9).</summary>
    WalkToTheRiver,

    /// <summary>Carry water from the river to the sapling (beat B10).</summary>
    WaterTheSapling,

    /// <summary>Clear the fallen branch (beat B10).</summary>
    ClearTheBranch,
}

/// <summary>What a step of a beat of the garden is.</summary>
public enum GardenStepKind
{
    /// <summary>A Scripture card: the world waits until the player turns the page.</summary>
    Card,

    /// <summary>The player walks, until a place is reached.</summary>
    Reach,

    /// <summary>The animals are brought one by one, and each is given a kind-name.</summary>
    Naming,
}

/// <summary>One step of a beat of the garden.</summary>
/// <param name="Kind">What the step is.</param>
/// <param name="Ref">A card: the verse.</param>
/// <param name="Speaker">A card: who speaks in the verse, or null.</param>
/// <param name="Area">A walk: the place to reach.</param>
/// <param name="Task">A walk: what the player is told.</param>
public sealed record GardenStep(GardenStepKind Kind, ScriptureRef Ref = default, StorySpeaker? Speaker = null, StoryArea Area = default, StoryTask Task = default);

/// <summary>A beat of the garden: its chapter and its steps, in order.</summary>
/// <param name="Chapter">The chapter.</param>
/// <param name="Beat">The beat.</param>
/// <param name="Steps">The steps.</param>
public sealed record GardenBeat(StoryChapter Chapter, StoryBeat Beat, IReadOnlyList<GardenStep> Steps);

/// <summary>
/// The man's path before the woman, as the design maps it to the text (section 2.2, beats B8 to B13): Genesis 2:4
/// to 2:20, every verse once and in order, one on each Scripture card, and between the cards what the player does.
/// References only: the text of a verse comes from the canonical file through the parser. The LORD God speaks in
/// 2:16, 2:17 and 2:18 and nowhere else here; the command (2:16 and 2:17) is given to the man, with no choice, and
/// before the woman exists.
/// </summary>
public static class GardenStory
{
    /// <summary>The verse at which the text first says "Adam": the label of the man changes as it is shown.</summary>
    public static readonly ScriptureRef AdamNamed = new(2, 19);

    /// <summary>The two verses of the command.</summary>
    public static IReadOnlyList<ScriptureRef> TheCommand { get; } = [new(2, 16), new(2, 17)];

    /// <summary>The verse shown when the animals stand in pairs.</summary>
    public static readonly ScriptureRef NoHelpMeetFound = new(2, 20);

    /// <summary>The beats, in order.</summary>
    public static IReadOnlyList<GardenBeat> Beats { get; } =
    [
        new(StoryChapter.Formation, StoryBeat.B8, [Card(4), Card(5), Card(6), Card(7)]),
        new(StoryChapter.GardenIntro, StoryBeat.B9,
        [
            Card(8), Card(9), Reach(StoryArea.River, StoryTask.WalkToTheRiver),
            Card(10), Card(11), Card(12), Card(13), Card(14),
        ]),
        new(StoryChapter.GardenIntro, StoryBeat.B10,
        [
            Card(15),
            Reach(StoryArea.River, StoryTask.WaterTheSapling), Reach(StoryArea.Sapling, StoryTask.WaterTheSapling),
            Reach(StoryArea.Branch, StoryTask.ClearTheBranch),
        ]),
        new(StoryChapter.Command, StoryBeat.B11, [Card(16, StorySpeaker.LordGod), Card(17, StorySpeaker.LordGod)]),
        new(StoryChapter.Command, StoryBeat.B12, [Card(18, StorySpeaker.LordGod)]),
        new(StoryChapter.Naming, StoryBeat.B13, [Card(19), new GardenStep(GardenStepKind.Naming), Card(20)]),
    ];

    /// <summary>Every verse the beats show, in order: Genesis 2:4 to 2:20.</summary>
    public static IReadOnlyList<ScriptureRef> Cards { get; } =
        [.. Beats.SelectMany(beat => beat.Steps).Where(step => step.Kind == GardenStepKind.Card).Select(step => step.Ref)];

    /// <summary>The beat of the garden with this number, or null.</summary>
    /// <param name="beat">The beat.</param>
    public static GardenBeat? BeatOf(StoryBeat beat) => Beats.FirstOrDefault(candidate => candidate.Beat == beat);

    /// <summary>The step a state stands at, or null when the state is not in a beat of the garden.</summary>
    /// <param name="state">The state.</param>
    public static GardenStep? StepOf(StoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return StoryMachine.PlaysTheGarden(state) && BeatOf(state.Beat) is { } beat && state.Card >= 0 && state.Card < beat.Steps.Count
            ? beat.Steps[state.Card]
            : null;
    }

    /// <summary>
    /// The sapling of beat B10 as a state shows it: 0 when there is none, 1 before it is watered, 2 after.
    /// </summary>
    /// <param name="state">The state.</param>
    public static int SaplingOf(StoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!StoryMachine.PlaysTheGarden(state) && state.Chapter != StoryChapter.WomanFormed)
        {
            return 0;
        }

        return state.Beat < StoryBeat.B10 ? 0 : state.Beat == StoryBeat.B10 && state.Card <= 2 ? 1 : 2;
    }

    /// <summary>Whether the fallen branch of beat B10 lies there still.</summary>
    /// <param name="state">The state.</param>
    public static bool BranchLies(StoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return StoryMachine.PlaysTheGarden(state) && state.Beat == StoryBeat.B10;
    }

    /// <summary>Whether the animals stand in pairs: from the card of Genesis 2:20 on.</summary>
    /// <param name="state">The state.</param>
    public static bool InPairs(StoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.NamedAnimals.Count == AnimalRoster.Count
            && (state.Chapter == StoryChapter.WomanFormed || StepOf(state) is { Kind: GardenStepKind.Card } step && step.Ref == NoHelpMeetFound);
    }

    private static GardenStep Card(int verse, StorySpeaker? speaker = null) => new(GardenStepKind.Card, new ScriptureRef(2, verse), speaker);

    private static GardenStep Reach(StoryArea area, StoryTask task) => new(GardenStepKind.Reach, Area: area, Task: task);
}
