using AdamEve.Content.Scripture;
using AdamEve.Core.Saves;
using AdamEve.Core.Story;

namespace AdamEve.UnitTests.Story;

/// <summary>
/// The story machine over beats B8 to B13, the man's path before the woman. What a test expects of Scripture is
/// references, their order and their count, and the canonical file as parsed; never text typed here.
/// </summary>
[TestFixture]
public class GardenStoryTests
{
    private static StoryState AfterCreation(PlayerCharacter character = PlayerCharacter.Adam) =>
        new() { Character = character, Chapter = StoryChapter.Formation, Beat = StoryBeat.B8, CreationWatched = true };

    /// <summary>The one thing a player can do next at a step: turn the page, come to the place, or choose a kind-name.</summary>
    private static StoryEvent NextOf(StoryState state, int choice = 0) => GardenStory.StepOf(state)!.Kind switch
    {
        GardenStepKind.Card => new DialogueAdvanced(),
        GardenStepKind.Reach => new ReachedArea(GardenStory.StepOf(state)!.Area),
        _ => new AnimalNamed(AnimalRoster.Animals[state.NamedAnimals.Count].KindNames[choice]),
    };

    /// <summary>A whole playthrough of the man's path, from the formation: every step, the first being the resuming of the saved game.</summary>
    private static List<StoryStep> PlayThrough(int choice = 0)
    {
        var step = StoryMachine.Resume(AfterCreation());
        var steps = new List<StoryStep> { step };
        while (StoryMachine.PlaysTheGarden(step.State))
        {
            steps.Count.ShouldBeLessThan(200);
            step = StoryMachine.Apply(step.State, NextOf(step.State, choice));
            steps.Add(step);
        }

        return steps;
    }

    private static StoryState StateAt(StoryBeat beat, int card) =>
        PlayThrough().Select(step => step.State).First(state => state.Beat == beat && state.Card == card);

    [Test]
    public void Resume_TheManAfterTheDaysOfCreation_ShouldShowTheFormationFromGenesis2_4AndSaveNothing()
    {
        var step = StoryMachine.Resume(AfterCreation());

        step.State.ShouldBe(AfterCreation() with { Card = 0 });
        step.Effects.OfType<ShowScripture>().Single().ShouldBe(new ShowScripture(new ScriptureRef(2, 4), null));
        step.Effects.OfType<ShowFormation>().Single().Layers.ShouldBe([FormationLayer.Heavens, FormationLayer.Earth]);
        step.Effects.OfType<SaveStory>().ShouldBeEmpty();
    }

    [Test]
    public void Resume_TheWomanAfterTheDaysOfCreation_ShouldWalkTheGardenAsBeforeBecauseHerPathIsNotBuiltYet()
    {
        var step = StoryMachine.Resume(AfterCreation(PlayerCharacter.Woman));

        step.Effects.ShouldBeEmpty();
        step.State.ShouldBe(AfterCreation(PlayerCharacter.Woman));
        StoryMachine.PlaysTheGarden(step.State).ShouldBeFalse();
        StoryMachine.Apply(step.State, new DialogueAdvanced()).Effects.ShouldBeEmpty();
    }

    [Test]
    public void Apply_AWholePlaythrough_ShouldShowEveryVerseFrom2_4To2_20OnceInTheOrderOfTheCanonicalFile()
    {
        var verses = KjvParser.Parse(CanonicalFile.Bytes()).Verses;
        var path = verses.SkipWhile(verse => verse.Ref != new VerseRef(2, 4)).TakeWhile(verse => verse.Ref != new VerseRef(2, 21))
            .Select(verse => new ScriptureRef(verse.Ref.Chapter, verse.Ref.Number)).ToList();

        var steps = PlayThrough();
        var shown = steps.SelectMany(step => step.Effects.OfType<ShowScripture>()).Select(card => card.Ref).ToList();

        shown.ShouldBe(path);
        shown.Count.ShouldBe(17);
        shown.ShouldBe(GardenStory.Cards);
        steps.ShouldAllBe(step => step.Effects.OfType<ShowScripture>().Count() <= 1);
        CreationStory.Cards.Concat(GardenStory.Cards).Select(card => new VerseRef(card.Chapter, card.Verse)).ShouldBe(verses.Take(51).Select(verse => verse.Ref));
    }

    [Test]
    public void Apply_AWholePlaythrough_ShouldGoThroughBeatsB8ToB13InTheirChaptersAndSaveWhenEachBegins()
    {
        var steps = PlayThrough();

        var beats = steps.Select(step => (step.State.Chapter, step.State.Beat)).Distinct().ToList();
        var saves = steps.Where(step => step.Effects.OfType<SaveStory>().Any()).Select(step => step.State.Beat).ToList();

        beats.ShouldBe(
        [
            (StoryChapter.Formation, StoryBeat.B8), (StoryChapter.GardenIntro, StoryBeat.B9), (StoryChapter.GardenIntro, StoryBeat.B10),
            (StoryChapter.Command, StoryBeat.B11), (StoryChapter.Command, StoryBeat.B12), (StoryChapter.Naming, StoryBeat.B13),
            (StoryChapter.WomanFormed, StoryBeat.B14),
        ]);
        saves.Distinct().ShouldBe([StoryBeat.B9, StoryBeat.B10, StoryBeat.B11, StoryBeat.B12, StoryBeat.B13, StoryBeat.B14]);
        steps.ShouldAllBe(step => step.Effects.OfType<SaveStory>().Count() <= 1);
        steps[^1].State.Card.ShouldBe(-1);
    }

    [Test]
    public void Apply_TheFormation_ShouldShowMistWith2_6AndDustBreathAndTheManWholeOnlyWith2_7()
    {
        var steps = PlayThrough().Where(step => step.State.Beat == StoryBeat.B8).ToList();

        var pictures = steps.Select(step => (step.Effects.OfType<ShowScripture>().Single().Ref, step.Effects.OfType<ShowFormation>().Single().Layers)).ToList();

        pictures.Select(picture => picture.Ref).ShouldBe([new ScriptureRef(2, 4), new ScriptureRef(2, 5), new ScriptureRef(2, 6), new ScriptureRef(2, 7)]);
        pictures[0].Layers.ShouldNotContain(FormationLayer.Mist);
        pictures[1].Layers.ShouldNotContain(FormationLayer.Mist);
        pictures[2].Layers.ShouldContain(FormationLayer.Mist);
        pictures.Take(3).ShouldAllBe(picture => !picture.Layers.Contains(FormationLayer.Man) && !picture.Layers.Contains(FormationLayer.Dust) && !picture.Layers.Contains(FormationLayer.Breath));
        pictures[3].Layers.ShouldBe([FormationLayer.Heavens, FormationLayer.Earth, FormationLayer.Mist, FormationLayer.Dust, FormationLayer.Breath, FormationLayer.Man]);
        steps.ShouldAllBe(step => step.Effects.OfType<ShowScripture>().Single().Speaker == null);
    }

    [Test]
    public void ShapesOf_TheLayersOfTheFormation_ShouldBeShapesFromNumbersAndNoneForTheManWhoIsTheRig()
    {
        foreach (var layer in Enum.GetValues<FormationLayer>().Where(layer => layer != FormationLayer.Man))
        {
            var shapes = FormationPicture.ShapesOf(layer);

            shapes.ShouldNotBeEmpty();
            shapes.ShouldAllBe(shape => shape.Path.StartsWith('M') && shape.Path.EndsWith('Z'));
            shapes.ShouldAllBe(shape => shape.Right >= -40 && shape.Left <= FormationPicture.Width + 40);
        }

        FormationPicture.ShapesOf(FormationLayer.Man).ShouldBeEmpty();
        FormationPicture.ShapesOf(FormationLayer.Dust).ShouldAllBe(shape => shape.Right - shape.Left < 6, "dust is motes in the air: nothing in it has the size of a body or of a part of one");
    }

    [Test]
    public void Apply_TheCommand_ShouldBeGivenToTheManBeforeTheWomanExistsAsTheTwoVersesAndNothingElse()
    {
        var steps = PlayThrough().Where(step => step.State.Chapter == StoryChapter.Command && step.State.Beat == StoryBeat.B11).ToList();

        var cards = steps.SelectMany(step => step.Effects.OfType<ShowScripture>()).ToList();

        cards.ShouldBe([new ShowScripture(new ScriptureRef(2, 16), StorySpeaker.LordGod), new ShowScripture(new ScriptureRef(2, 17), StorySpeaker.LordGod)]);
        cards.Select(card => card.Ref).ShouldBe(GardenStory.TheCommand);
        steps.ShouldAllBe(step => !step.State.WomanCreated);
        steps.ShouldAllBe(step => step.State.Character == PlayerCharacter.Adam && step.State.Man == ManLabel.TheMan);
        steps.SelectMany(step => step.Effects).Select(effect => effect.GetType()).Distinct().ShouldBeSubsetOf([typeof(ShowScripture), typeof(SaveStory)], "the command is the text and nothing beside it: no narration, no task, no choice");
        steps.SelectMany(step => step.Effects.OfType<OfferChoices>()).ShouldBeEmpty();
    }

    [Test]
    public void Apply_AnythingButTurningThePageAtTheCommand_ShouldBeRefused()
    {
        var command = StateAt(StoryBeat.B11, 0);

        StoryEvent[] others = [new ReachedArea(StoryArea.River), new AnimalNamed("pachyderm"), new Revealed(), new ChoiceMade(StoryChoice.SkipCreation), new CharacterChosen(PlayerCharacter.Woman)];

        foreach (var other in others)
        {
            var step = StoryMachine.Apply(command, other);

            step.State.ShouldBe(command);
            step.Effects.ShouldBeEmpty();
        }
    }

    [Test]
    public void Apply_AWholePlaythrough_ShouldLetTheLordGodSpeakOnlyIn2_16And2_17And2_18AndOnlyScripture()
    {
        var steps = PlayThrough();

        var voiced = steps.SelectMany(step => step.Effects.OfType<ShowScripture>()).Where(card => card.Speaker is not null).ToList();

        voiced.Select(card => card.Ref).ShouldBe([new ScriptureRef(2, 16), new ScriptureRef(2, 17), new ScriptureRef(2, 18)]);
        voiced.ShouldAllBe(card => card.Speaker == StorySpeaker.LordGod);
        steps.SelectMany(step => step.Effects).Select(effect => effect.GetType().Name).ShouldAllBe(name => !name.Contains("Speech", StringComparison.Ordinal) && !name.Contains("Narration", StringComparison.Ordinal));
    }

    [Test]
    public void Apply_TurningThePageOf2_18_ShouldChangeTheLabelFromTheManToAdamWithTheCardOf2_19AndNotBefore()
    {
        var steps = PlayThrough();
        var before = steps.Last(step => step.State.Beat == StoryBeat.B12).State;

        var step = StoryMachine.Apply(before, new DialogueAdvanced());

        before.Man.ShouldBe(ManLabel.TheMan);
        steps.TakeWhile(earlier => earlier.State.Beat < StoryBeat.B13).ShouldAllBe(earlier => earlier.State.Man == ManLabel.TheMan && !earlier.Effects.OfType<SetLabel>().Any());
        step.State.Man.ShouldBe(ManLabel.Adam);
        step.Effects.OfType<ShowScripture>().Single().Ref.ShouldBe(new ScriptureRef(2, 19));
        step.Effects.OfType<SetLabel>().Single().ShouldBe(new SetLabel(ManLabel.Adam));
        steps.SkipWhile(later => later.State.Beat < StoryBeat.B13).ShouldAllBe(later => later.State.Man == ManLabel.Adam);
        steps.SelectMany(any => any.Effects.OfType<SetLabel>()).Count().ShouldBe(1);
    }

    [Test]
    public void ManAt_EveryBeat_ShouldBeTheManBeforeB13AndAdamFromIt()
    {
        var verses = KjvParser.Parse(CanonicalFile.Bytes());

        foreach (var beat in Enum.GetValues<StoryBeat>())
        {
            StoryMachine.ManAt(beat).ShouldBe(beat >= StoryBeat.B13 ? ManLabel.Adam : ManLabel.TheMan);
        }

        // The text itself: no verse before 2:19 says "Adam", and 2:19 does.
        verses.Verses.TakeWhile(verse => verse.Ref != new VerseRef(2, 19)).ShouldAllBe(verse => !verse.Text.Contains("Adam", StringComparison.Ordinal));
        verses.Find(GardenStory.AdamNamed).Text.ShouldContain("Adam");
    }

    [Test]
    public void Resume_OnEitherSideOf2_19_ShouldHoldTheLabelOfItsBeat()
    {
        var command = new StoryState { Character = PlayerCharacter.Adam, Chapter = StoryChapter.Command, Beat = StoryBeat.B12 };
        var naming = new StoryState { Character = PlayerCharacter.Adam, Chapter = StoryChapter.Naming, Beat = StoryBeat.B13 };

        StoryMachine.Resume(command).State.Man.ShouldBe(ManLabel.TheMan);
        StoryMachine.Resume(naming).State.Man.ShouldBe(ManLabel.Adam);
        StoryMachine.Resume(naming).Effects.OfType<SetLabel>().Single().Man.ShouldBe(ManLabel.Adam);
    }

    [Test]
    public void Apply_ThePlacesOfTheGarden_ShouldBeAwaitedOneAfterAnotherAndNoOtherPlaceShouldDo()
    {
        var steps = PlayThrough();

        var tasks = steps.SelectMany(step => step.Effects.OfType<ShowTask>()).Select(task => task.Task).ToList();
        var river = steps.First(step => step.Effects.OfType<ShowTask>().Any()).State;

        tasks.ShouldBe([StoryTask.WalkToTheRiver, StoryTask.WaterTheSapling, StoryTask.WaterTheSapling, StoryTask.ClearTheBranch]);
        river.Beat.ShouldBe(StoryBeat.B9);
        StoryMachine.Apply(river, new ReachedArea(StoryArea.Branch)).Effects.ShouldBeEmpty();
        StoryMachine.Apply(river, new DialogueAdvanced()).Effects.ShouldBeEmpty();
        StoryMachine.Apply(river, new ReachedArea(StoryArea.River)).Effects.OfType<ShowScripture>().Single().Ref.ShouldBe(new ScriptureRef(2, 10));
    }

    [Test]
    public void SaplingOfAndBranchLies_ThroughTheTasks_ShouldShowTheSaplingWateredAndTheBranchCleared()
    {
        var states = PlayThrough().Select(step => step.State).ToList();

        states.Where(state => state.Beat < StoryBeat.B10).ShouldAllBe(state => GardenStory.SaplingOf(state) == 0 && !GardenStory.BranchLies(state));
        states.Where(state => state.Beat == StoryBeat.B10 && state.Card <= 2).ShouldAllBe(state => GardenStory.SaplingOf(state) == 1 && GardenStory.BranchLies(state));
        GardenStory.SaplingOf(StateAt(StoryBeat.B10, 3)).ShouldBe(2);
        GardenStory.BranchLies(StateAt(StoryBeat.B10, 3)).ShouldBeTrue();
        states.Where(state => state.Beat > StoryBeat.B10).ShouldAllBe(state => GardenStory.SaplingOf(state) == 2 && !GardenStory.BranchLies(state));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void Apply_ChoosingAnyOfTheThree_ShouldBeAcceptedAndKeptAsItsIdInTheJournalAndTheState(int choice)
    {
        var steps = PlayThrough(choice);

        var journal = steps.SelectMany(step => step.Effects.OfType<AddJournal>()).ToList();
        var last = steps[^1].State;

        journal.Count.ShouldBe(24);
        journal.Select(entry => entry.Animal).ShouldBe(AnimalRoster.Animals.Select(animal => animal.Id));
        journal.Select(entry => entry.KindName).ShouldBe(AnimalRoster.Animals.Select(animal => animal.KindNames[choice]));
        last.NamedAnimals.ShouldBe(journal.Select(entry => entry.KindName));
        SaveGame.Of(last, new AdamEve.Core.World.TilePos(1, 1)).NamedAnimals.ShouldBe(last.NamedAnimals);
        steps.Where(step => step.Effects.OfType<AddJournal>().Any()).ShouldAllBe(step => step.Effects.OfType<SaveStory>().Count() == 1, "every name is saved when it is given");
    }

    [Test]
    public void Apply_TheNaming_ShouldBringTheAnimalsOneByOneInTheOrderOfTheRosterEachWithItsThreeKindNames()
    {
        var steps = PlayThrough();

        var brought = steps.SelectMany(step => step.Effects.OfType<BringAnimal>()).ToList();

        brought.Select(animal => animal.Animal).ShouldBe(AnimalRoster.Animals.Select(animal => animal.Id));
        brought.ShouldAllBe(animal => animal.KindNames.Count == 3);
        steps.ShouldAllBe(step => step.Effects.OfType<BringAnimal>().Count() <= 1);
        brought.Count.ShouldBe(24);
    }

    [TestCase("a stub name", Description = "typed text")]
    [TestCase("", Description = "nothing")]
    [TestCase("swine", Description = "a kind-name of another animal")]
    [TestCase("Pachyderm", Description = "not the id")]
    public void Apply_AKindNameThatIsNotOneOfTheThreeOfTheAnimalBrought_ShouldBeRefused(string kindName)
    {
        var naming = StateAt(StoryBeat.B13, 1);

        var step = StoryMachine.Apply(naming, new AnimalNamed(kindName));

        step.Effects.ShouldBeEmpty();
        step.State.ShouldBe(naming);
        step.State.NamedAnimals.ShouldBeEmpty();
    }

    [Test]
    public void Apply_TheLastKindName_ShouldStandTheAnimalsInPairsWithTheCardOf2_20AndThenLeaveTheManWalkingWithoutTheWoman()
    {
        var steps = PlayThrough();
        var pairs = steps.Single(step => step.Effects.OfType<ShowPairs>().Any());

        var end = StoryMachine.Apply(pairs.State, new DialogueAdvanced());

        pairs.Effects.OfType<ShowScripture>().Single().Ref.ShouldBe(GardenStory.NoHelpMeetFound);
        pairs.Effects.OfType<AddJournal>().Single().Animal.ShouldBe(AnimalRoster.Animals[^1].Id);
        GardenStory.InPairs(pairs.State).ShouldBeTrue();
        steps.TakeWhile(step => step != pairs).ShouldAllBe(step => !GardenStory.InPairs(step.State));
        end.State.ShouldBe(pairs.State with { Chapter = StoryChapter.WomanFormed, Beat = StoryBeat.B14, Card = -1 });
        end.Effects.ShouldBe([new SaveStory()]);
        end.State.WomanCreated.ShouldBeFalse();
        GardenStory.InPairs(end.State).ShouldBeTrue();
        StoryMachine.Apply(end.State, new DialogueAdvanced()).Effects.ShouldBeEmpty();
    }

    [Test]
    public void Resume_InTheMiddleOfTheNaming_ShouldBeginTheBeatAgainAndBringTheNextAnimalThatHasNoName()
    {
        var named = AnimalRoster.Animals.Take(7).Select(animal => animal.KindNames[1]).ToList();
        var saved = new StoryState { Character = PlayerCharacter.Adam, Chapter = StoryChapter.Naming, Beat = StoryBeat.B13, NamedAnimals = named };

        var resumed = StoryMachine.Resume(saved);
        var next = StoryMachine.Apply(resumed.State, new DialogueAdvanced());

        resumed.Effects.OfType<ShowScripture>().Single().Ref.ShouldBe(new ScriptureRef(2, 19));
        resumed.State.NamedAnimals.ShouldBe(named);
        next.Effects.OfType<BringAnimal>().Single().Animal.ShouldBe(AnimalRoster.Animals[7].Id);
    }

    [Test]
    public void Resume_WithEveryAnimalNamed_ShouldGoFromTheCardOf2_19StraightToTheCardOf2_20()
    {
        var named = AnimalRoster.Animals.Select(animal => animal.KindNames[0]).ToList();
        var saved = new StoryState { Character = PlayerCharacter.Adam, Chapter = StoryChapter.Naming, Beat = StoryBeat.B13, NamedAnimals = named };

        var next = StoryMachine.Apply(StoryMachine.Resume(saved).State, new DialogueAdvanced());

        next.Effects.OfType<BringAnimal>().ShouldBeEmpty();
        next.Effects.OfType<ShowScripture>().Single().Ref.ShouldBe(new ScriptureRef(2, 20));
        next.Effects.OfType<ShowPairs>().Count().ShouldBe(1);
    }

    [Test]
    public void Equals_TwoStatesWithTheSameKindNames_ShouldBeEqualAndWithOtherKindNamesNot()
    {
        var one = new StoryState { Character = PlayerCharacter.Adam, Chapter = StoryChapter.Naming, Beat = StoryBeat.B13, NamedAnimals = ["pachyderm", "ovine"] };
        var same = new StoryState { Character = PlayerCharacter.Adam, Chapter = StoryChapter.Naming, Beat = StoryBeat.B13, NamedAnimals = ["pachyderm", "ovine"] };
        var other = same with { NamedAnimals = ["pachyderm", "ruminant"] };

        one.ShouldBe(same);
        one.GetHashCode().ShouldBe(same.GetHashCode());
        one.ShouldNotBe(other);
    }

    [Test]
    public void CanStandAt_TheChaptersAndBeats_ShouldHoldEachBeatInItsChapterAndOnlyTheManBetweenTheFormationAndLifeInTheGarden()
    {
        StoryMachine.IsBeatOf(StoryChapter.Formation, StoryBeat.B8).ShouldBeTrue();
        StoryMachine.IsBeatOf(StoryChapter.GardenIntro, StoryBeat.B9).ShouldBeTrue();
        StoryMachine.IsBeatOf(StoryChapter.GardenIntro, StoryBeat.B10).ShouldBeTrue();
        StoryMachine.IsBeatOf(StoryChapter.Command, StoryBeat.B11).ShouldBeTrue();
        StoryMachine.IsBeatOf(StoryChapter.Command, StoryBeat.B12).ShouldBeTrue();
        StoryMachine.IsBeatOf(StoryChapter.Naming, StoryBeat.B13).ShouldBeTrue();
        StoryMachine.IsBeatOf(StoryChapter.WomanFormed, StoryBeat.B14).ShouldBeTrue();
        StoryMachine.IsBeatOf(StoryChapter.GardenLife, StoryBeat.B17).ShouldBeTrue();
        StoryMachine.IsBeatOf(StoryChapter.Formation, StoryBeat.B9).ShouldBeFalse();
        StoryMachine.IsBeatOf(StoryChapter.Naming, StoryBeat.B12).ShouldBeFalse();
        StoryMachine.IsBeatOf(StoryChapter.WomanFormed, StoryBeat.B15).ShouldBeFalse("beats B15 and B16 are not built yet");
        StoryMachine.IsBeatOf(StoryChapter.Title, StoryBeat.B0).ShouldBeFalse();
        StoryMachine.CanStandAt(PlayerCharacter.Woman, StoryChapter.Formation, StoryBeat.B8).ShouldBeTrue();
        StoryMachine.CanStandAt(PlayerCharacter.Woman, StoryChapter.GardenLife, StoryBeat.B17).ShouldBeTrue();
        StoryMachine.CanStandAt(PlayerCharacter.Woman, StoryChapter.Command, StoryBeat.B11).ShouldBeFalse();
        StoryMachine.CanStandAt(PlayerCharacter.Adam, StoryChapter.Command, StoryBeat.B11).ShouldBeTrue();
    }
}
