using AdamEve.Content.Scripture;
using AdamEve.Core.Saves;
using AdamEve.Core.Story;

namespace AdamEve.UnitTests.Story;

/// <summary>
/// The story machine over beats B0 to B7. What a test expects of Scripture is references, their order and their
/// count, and the canonical file as parsed; never text typed here.
/// </summary>
[TestFixture]
public class StoryMachineTests
{
    private static readonly ScriptureRef LastOfCreation = new(2, 3);

    private static StoryState Chosen(PlayerCharacter character = PlayerCharacter.Adam, bool watched = false) =>
        StoryMachine.Apply(StoryState.AtTitle(watched), new CharacterChosen(character)).State;

    /// <summary>The one thing a player can do next: the gesture of the day when the story waits for it, the next page otherwise.</summary>
    private static StoryEvent NextOf(StoryState state) => StoryMachine.AwaitsReveal(state) ? new Revealed() : new DialogueAdvanced();

    /// <summary>A whole playthrough of the days of creation: every step, the first being the choice of the character.</summary>
    private static List<(StoryEvent Event, StoryStep Step)> PlayThrough(PlayerCharacter character = PlayerCharacter.Adam, bool watched = false)
    {
        StoryEvent chosen = new CharacterChosen(character);
        var step = StoryMachine.Apply(StoryState.AtTitle(watched), chosen);
        var steps = new List<(StoryEvent, StoryStep)> { (chosen, step) };
        while (step.State.Chapter == StoryChapter.Creation)
        {
            steps.Count.ShouldBeLessThan(200);
            var next = NextOf(step.State);
            step = StoryMachine.Apply(step.State, next);
            steps.Add((next, step));
        }

        return steps;
    }

    private static IEnumerable<ShowScripture> CardsOf(IEnumerable<(StoryEvent Event, StoryStep Step)> steps) =>
        steps.SelectMany(step => step.Step.Effects.OfType<ShowScripture>());

    [TestCase(PlayerCharacter.Adam)]
    [TestCase(PlayerCharacter.Woman)]
    public void Apply_CharacterChosenAtTheTitle_ShouldBeginTheFirstDayWithItsFirstVerseAndSave(PlayerCharacter character)
    {
        var title = StoryState.AtTitle(creationWatched: false);

        var step = StoryMachine.Apply(title, new CharacterChosen(character));

        step.State.ShouldBe(new StoryState { Character = character, Chapter = StoryChapter.Creation, Beat = StoryBeat.B1, Card = 0 });
        step.Effects.OfType<ShowScripture>().Single().ShouldBe(new ShowScripture(new ScriptureRef(1, 1), null));
        step.Effects.OfType<SaveStory>().Count().ShouldBe(1);
        step.Effects.OfType<AwaitReveal>().ShouldBeEmpty();
        title.Chapter.ShouldBe(StoryChapter.Title);
        title.Character.ShouldBeNull();
    }

    [Test]
    public void Apply_AWholePlaythrough_ShouldShowEveryVerseFrom1_1To2_3OnceInTheOrderOfTheCanonicalFile()
    {
        var verses = KjvParser.Parse(CanonicalFile.Bytes()).Verses;
        var opening = verses.TakeWhile(verse => verse.Ref != new VerseRef(2, 4)).Select(verse => new ScriptureRef(verse.Ref.Chapter, verse.Ref.Number)).ToList();

        var shown = CardsOf(PlayThrough()).Select(card => card.Ref).ToList();

        shown.ShouldBe(opening);
        shown.Count.ShouldBe(34);
        shown[0].ShouldBe(new ScriptureRef(1, 1));
        shown[^1].ShouldBe(LastOfCreation);
        shown.ShouldBeUnique();
    }

    [Test]
    public void Apply_AWholePlaythrough_ShouldShowOneCardAtATimeAndNeverMoreThanOneForOneThingThePlayerDoes()
    {
        var steps = PlayThrough();

        steps.ShouldAllBe(step => step.Step.Effects.OfType<ShowScripture>().Count() <= 1);
        steps.Count(step => step.Step.Effects.OfType<ShowScripture>().Any()).ShouldBe(34);
    }

    [Test]
    public void Apply_AWholePlaythroughAsTheWoman_ShouldShowTheSameCardsAsAdam()
    {
        var adam = CardsOf(PlayThrough(PlayerCharacter.Adam)).ToList();

        var woman = CardsOf(PlayThrough(PlayerCharacter.Woman)).ToList();

        woman.ShouldBe(adam);
    }

    [Test]
    public void Apply_AWholePlaythrough_ShouldGoThroughBeatsB1ToB7InOrderAndSaveAsEachBegins()
    {
        var steps = PlayThrough();

        var beats = steps.Where(step => step.Step.State.Chapter == StoryChapter.Creation).Select(step => step.Step.State.Beat).Distinct().ToList();
        var saves = steps.Where(step => step.Step.Effects.OfType<SaveStory>().Any()).Select(step => step.Step.State.Beat).ToList();

        beats.ShouldBe([StoryBeat.B1, StoryBeat.B2, StoryBeat.B3, StoryBeat.B4, StoryBeat.B5, StoryBeat.B6, StoryBeat.B7]);
        saves.ShouldBe([StoryBeat.B1, StoryBeat.B2, StoryBeat.B3, StoryBeat.B4, StoryBeat.B5, StoryBeat.B6, StoryBeat.B7, StoryBeat.B8]);
    }

    [Test]
    public void Apply_AWholePlaythrough_ShouldAskOneGestureInEachOfTheFirstSixDaysAndNoneInTheSeventh()
    {
        var steps = PlayThrough();

        var reveals = steps.Where(step => step.Event is Revealed).Select(step => step.Step.State.Beat).ToList();
        var seventh = steps.Where(step => step.Step.State is { Chapter: StoryChapter.Creation, Beat: StoryBeat.B7 }).ToList();

        reveals.ShouldBe([StoryBeat.B1, StoryBeat.B2, StoryBeat.B3, StoryBeat.B4, StoryBeat.B5, StoryBeat.B6]);
        seventh.Count.ShouldBe(3);
        seventh.ShouldAllBe(step => step.Event is DialogueAdvanced && !step.Step.Effects.OfType<AwaitReveal>().Any());
        seventh.Select(step => step.Step.Effects.OfType<ShowScripture>().Single().Ref).ShouldBe([new ScriptureRef(2, 1), new ScriptureRef(2, 2), LastOfCreation]);
    }

    [Test]
    public void Apply_EveryGestureOfADay_ShouldShowACardOnWhichGodSpeaksAndNothingThePlayerMade()
    {
        var steps = PlayThrough();

        var revealed = steps.Where(step => step.Event is Revealed).Select(step => step.Step.Effects.OfType<ShowScripture>().Single()).ToList();

        revealed.ShouldAllBe(card => card.Speaker == StorySpeaker.God);
        revealed.Select(card => card.Ref).ShouldBe([new(1, 3), new(1, 6), new(1, 9), new(1, 14), new(1, 20), new(1, 24)]);
        steps.Where(step => step.Event is Revealed).ShouldAllBe(step => step.Step.Effects.All(effect => effect is ShowScene || effect is ShowScripture));
    }

    [Test]
    public void Apply_AWholePlaythrough_ShouldGiveGodAVoiceOnScriptureCardsOnlyAndOnTheVersesInWhichHeSpeaks()
    {
        ScriptureRef[] spoken = [new(1, 3), new(1, 6), new(1, 9), new(1, 11), new(1, 14), new(1, 15), new(1, 20), new(1, 22), new(1, 24), new(1, 26), new(1, 28), new(1, 29), new(1, 30)];
        var steps = PlayThrough();

        var voiced = CardsOf(steps).Where(card => card.Speaker is not null).ToList();
        var kinds = steps.SelectMany(step => step.Step.Effects).Select(effect => effect.GetType()).Distinct();

        voiced.Select(card => card.Ref).ShouldBe(spoken);
        voiced.ShouldAllBe(card => card.Speaker == StorySpeaker.God);
        kinds.ShouldBe([typeof(ShowScene), typeof(ShowScripture), typeof(SaveStory), typeof(AwaitReveal)], ignoreOrder: true);
    }

    [Test]
    public void Apply_TheLastCardOfTheSeventhDayTurned_ShouldLeaveForWhatFollowsAndMarkTheDaysWatched()
    {
        var steps = PlayThrough(PlayerCharacter.Woman);

        var last = steps[^1].Step;

        last.State.ShouldBe(new StoryState { Character = PlayerCharacter.Woman, Chapter = StoryChapter.Formation, Beat = StoryBeat.B8, Card = -1, CreationWatched = true });
        last.Effects.ShouldHaveSingleItem().ShouldBeOfType<SaveStory>();
        steps[..^1].ShouldAllBe(step => !step.Step.State.CreationWatched);
    }

    [Test]
    public void Apply_DialogueAdvancedWhileTheStoryWaitsForTheGesture_ShouldBeRefused()
    {
        var waiting = StoryMachine.Apply(Chosen(), new DialogueAdvanced()).State;

        var step = StoryMachine.Apply(waiting, new DialogueAdvanced());

        StoryMachine.AwaitsReveal(waiting).ShouldBeTrue();
        step.State.ShouldBeSameAs(waiting);
        step.Effects.ShouldBeEmpty();
    }

    [Test]
    public void Apply_RevealedWhenTheStoryDoesNotWaitForIt_ShouldBeRefused()
    {
        var first = Chosen();

        var step = StoryMachine.Apply(first, new Revealed());

        StoryMachine.AwaitsReveal(first).ShouldBeFalse();
        step.State.ShouldBeSameAs(first);
        step.Effects.ShouldBeEmpty();
    }

    [Test]
    public void Apply_EveryStateOfAFirstPlaythrough_ShouldRefuseToSkipAndNeverOfferIt()
    {
        var steps = PlayThrough();

        foreach (var (_, step) in steps.Where(step => step.Step.State.Chapter == StoryChapter.Creation))
        {
            var skipped = StoryMachine.Apply(step.State, new ChoiceMade(StoryChoice.SkipCreation));

            skipped.State.ShouldBeSameAs(step.State);
            skipped.Effects.ShouldBeEmpty();
            step.Effects.OfType<OfferChoices>().ShouldBeEmpty();
        }
    }

    [Test]
    public void Apply_SkipAfterAFirstCompletion_ShouldBeOfferedInEveryStateAndLeaveWithoutShowingScripture()
    {
        var watched = PlayThrough()[^1].Step.State.CreationWatched;
        var again = PlayThrough(PlayerCharacter.Woman, watched);

        foreach (var (_, step) in again.Where(step => step.Step.State.Chapter == StoryChapter.Creation))
        {
            var skipped = StoryMachine.Apply(step.State, new ChoiceMade(StoryChoice.SkipCreation));

            step.Effects.OfType<OfferChoices>().Single().Choices.ShouldBe([StoryChoice.SkipCreation]);
            skipped.State.ShouldBe(new StoryState { Character = PlayerCharacter.Woman, Chapter = StoryChapter.Formation, Beat = StoryBeat.B8, Card = -1, CreationWatched = true });
            skipped.Effects.ShouldHaveSingleItem().ShouldBeOfType<SaveStory>();
        }

        watched.ShouldBeTrue();
        CardsOf(again).Count().ShouldBe(34);
    }

    [Test]
    public void Apply_AnEventThatDoesNotFitTheChapter_ShouldBeRefused()
    {
        var title = StoryState.AtTitle(creationWatched: true);
        var after = PlayThrough()[^1].Step.State;
        StoryEvent[] events = [new DialogueAdvanced(), new Revealed(), new ChoiceMade(StoryChoice.SkipCreation)];

        foreach (var storyEvent in events)
        {
            StoryMachine.Apply(title, storyEvent).ShouldSatisfyAllConditions(step => step.State.ShouldBeSameAs(title), step => step.Effects.ShouldBeEmpty());
            StoryMachine.Apply(after, storyEvent).ShouldSatisfyAllConditions(step => step.State.ShouldBeSameAs(after), step => step.Effects.ShouldBeEmpty());
        }

        StoryMachine.Apply(Chosen(), new CharacterChosen(PlayerCharacter.Woman)).Effects.ShouldBeEmpty();
        StoryMachine.Apply(title, new CharacterChosen((PlayerCharacter)7)).Effects.ShouldBeEmpty();
    }

    [TestCase(StoryBeat.B1, 0, false)]
    [TestCase(StoryBeat.B2, -1, true)]
    [TestCase(StoryBeat.B6, -1, true)]
    [TestCase(StoryBeat.B7, 0, false)]
    public void Resume_ASavedBeat_ShouldShowItFromItsBeginningWithoutSaving(StoryBeat beat, int card, bool awaits)
    {
        var save = new SaveGame { Character = PlayerCharacter.Woman, Chapter = StoryChapter.Creation, Beat = beat };

        var step = StoryMachine.Resume(StoryState.FromSave(save));

        step.State.ShouldBe(new StoryState { Character = PlayerCharacter.Woman, Chapter = StoryChapter.Creation, Beat = beat, Card = card });
        step.Effects.OfType<AwaitReveal>().Any().ShouldBe(awaits);
        step.Effects.OfType<ShowScripture>().Any().ShouldBe(!awaits);
        step.Effects.OfType<SaveStory>().ShouldBeEmpty();
        step.Effects.OfType<ShowScene>().Count().ShouldBe(1);
    }

    [Test]
    public void Of_AStateOfTheStory_ShouldBeASavedGameThatResumesTheSameBeat()
    {
        var state = PlayThrough(PlayerCharacter.Woman, watched: true).First(step => step.Step.State.Beat == StoryBeat.B4).Step.State;

        var save = SaveGame.Of(state, new AdamEve.Core.World.TilePos(3, 4));

        save.ShouldBe(new SaveGame { Character = PlayerCharacter.Woman, TileX = 3, TileY = 4, Chapter = StoryChapter.Creation, Beat = StoryBeat.B4, CreationWatched = true });
        StoryState.FromSave(save).ShouldBe(state);
        Should.Throw<ArgumentException>(() => SaveGame.Of(StoryState.AtTitle(creationWatched: false), new AdamEve.Core.World.TilePos(3, 4)));
    }

    [Test]
    public void Days_TheDaysOfCreation_ShouldBeTheVersesOfTheBeatListOfTheDesign()
    {
        (StoryBeat Beat, ScriptureRef First, ScriptureRef Last)[] design =
        [
            (StoryBeat.B1, new(1, 1), new(1, 5)), (StoryBeat.B2, new(1, 6), new(1, 8)), (StoryBeat.B3, new(1, 9), new(1, 13)), (StoryBeat.B4, new(1, 14), new(1, 19)),
            (StoryBeat.B5, new(1, 20), new(1, 23)), (StoryBeat.B6, new(1, 24), new(1, 31)), (StoryBeat.B7, new(2, 1), new(2, 3)),
        ];

        var days = CreationStory.Days.Select(day => (day.Beat, day.Cards[0], day.Cards[^1]));

        days.ShouldBe(design);
        CreationStory.Days.ShouldAllBe(day => day.Voice.All(verse => day.Cards.Contains(verse)));
        CreationStory.Days.ShouldAllBe(day => day.RevealAt < 0 || day.Voice[0] == day.Cards[day.RevealAt]);
        CreationStory.Cards.Count.ShouldBe(34);
    }
}
