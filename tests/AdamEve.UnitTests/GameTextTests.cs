using System.Reflection;
using AdamEve.Content;
using AdamEve.Content.Scripture;

namespace AdamEve.UnitTests;

[TestFixture]
public class GameTextTests
{
    private static string ReviewList() => File.ReadAllText(Path.Combine(CanonicalFile.RepositoryRoot(), "content", "README.md"));

    private static IEnumerable<string> Labels() => typeof(GameText)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .Select(field => (string)field.GetRawConstantValue()!);

    [Test]
    public void Citation_AVerse_ShouldBeTheBookThenChapterAndVerse()
    {
        var citation = GameText.Citation(new VerseRef(3, 9));

        citation.ShouldBe("Genesis 3:9");
    }

    [Test]
    public void ReviewList_EveryGlossaryEntry_ShouldBeListedForReview()
    {
        var list = ReviewList();

        foreach (var entry in Glossary.Parse(EmbeddedContent.Glossary()).Entries)
        {
            list.ShouldContain($"| {entry.Term} | {entry.Definition} |", Case.Sensitive);
        }
    }

    [Test]
    public void ReviewList_EveryLabel_ShouldBeListedForReview()
    {
        var list = ReviewList();

        Labels().ShouldNotBeEmpty();
        foreach (var label in Labels())
        {
            list.ShouldContain($"| {label} |", Case.Sensitive);
        }
    }

    [Test]
    public void ReviewList_EveryKindNameWithItsMeaning_ShouldBeListedForReviewWithItsAnimalAndItsGroup()
    {
        var list = ReviewList();

        foreach (var animal in Animals.Parse(EmbeddedContent.Animals()).List)
        {
            foreach (var kindName in animal.KindNames)
            {
                list.ShouldContain($"| {animal.Id} | {animal.Category} | {kindName.Word} | {kindName.Meaning} |", Case.Sensitive);
            }
        }
    }

    [Test]
    public void Narration_EveryLine_ShouldReadAtGrade7OrLower()
    {
        GameText.Narration.ShouldNotBeEmpty();
        foreach (var line in GameText.Narration)
        {
            Readability.FleschKincaidGrade(line).ShouldBeLessThanOrEqualTo(Readability.HighestGrade, line);
        }

        Labels().Where(label => label.Contains(' ') && (label.EndsWith('.') || label.EndsWith('?'))).ShouldBeSubsetOf(GameText.Narration, "every sentence the game wrote is held to the grade");
    }

    [TestCase("cat", 1)]
    [TestCase("river", 2)]
    [TestCase("animal", 3)]
    [TestCase("name", 1)]
    [TestCase("sapling", 2)]
    [TestCase("the", 1)]
    [TestCase("table", 2)]
    public void Syllables_AWord_ShouldBeCountedByTheRuleOfThumb(string word, int syllables)
    {
        Readability.Syllables(word).ShouldBe(syllables);
    }

    [Test]
    public void FleschKincaidGrade_AHardSentence_ShouldBeAboveTheGradeOfNarration()
    {
        var grade = Readability.FleschKincaidGrade("Considerable deliberation accompanies unnecessarily complicated institutional communication.");

        grade.ShouldBeGreaterThan(Readability.HighestGrade);
        Readability.FleschKincaidGrade(string.Empty).ShouldBe(0);
    }

    [Test]
    public void Labels_TheMansPath_ShouldSayTheManThenAdamAndTheLordGodAsTheDesignWritesThem()
    {
        GameText.ManName(AdamEve.Core.Story.ManLabel.TheMan).ShouldBe("The man");
        GameText.ManName(AdamEve.Core.Story.ManLabel.Adam).ShouldBe("Adam");
        GameText.SpeakerName(AdamEve.Core.Story.StorySpeaker.LordGod).ShouldBe("The LORD God");
        foreach (var task in Enum.GetValues<AdamEve.Core.Story.StoryTask>())
        {
            GameText.TaskLine(task).ShouldNotBeNullOrWhiteSpace();
            GameText.Narration.ShouldContain(GameText.TaskLine(task));
        }
    }

    [Test]
    public void Labels_GameText_ShouldNeverSayApple()
    {
        var definitions = Glossary.Parse(EmbeddedContent.Glossary()).Entries.Select(entry => entry.Definition);

        Labels().Concat(definitions).ShouldAllBe(text => !text.Contains("apple", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void Labels_GameText_ShouldNeverSayThatAnyoneCreates()
    {
        // The player reveals what God made. No label says or implies that the player creates or makes anything.
        string[] forbidden = ["creat", "make", "made", "build", "form "];

        foreach (var label in Labels())
        {
            forbidden.ShouldAllBe(word => !label.Contains(word, StringComparison.OrdinalIgnoreCase), label);
        }
    }

    [Test]
    public void Labels_TheControlsOfTheDaysOfCreation_ShouldBeRevealAndTurnThePageAndSkip()
    {
        GameText.Reveal.ShouldBe("Reveal");
        GameText.TurnThePage.ShouldBe("Turn the page");
        GameText.Skip.ShouldBe("Skip");
    }

    [Test]
    public void Labels_TheCharacterSelect_ShouldSayAdamAndTheWomanNamedEveInGenesis3_20()
    {
        GameText.CharacterName(AdamEve.Core.Saves.PlayerCharacter.Adam).ShouldBe("Adam");
        GameText.CharacterName(AdamEve.Core.Saves.PlayerCharacter.Woman).ShouldBe("The woman");
        GameText.CharacterWomanNote.ShouldBe("named Eve in " + GameText.Citation(new VerseRef(3, 20)));
        GameText.SpeakerName(AdamEve.Core.Story.StorySpeaker.God).ShouldBe("God");
    }

    [Test]
    public void Labels_GameText_ShouldHoldNoVerseOfScripture()
    {
        var verses = KjvParser.Parse(CanonicalFile.Bytes()).Verses;

        foreach (var label in Labels())
        {
            verses.ShouldAllBe(verse => !label.Contains(verse.Text, StringComparison.Ordinal));
        }
    }
}
