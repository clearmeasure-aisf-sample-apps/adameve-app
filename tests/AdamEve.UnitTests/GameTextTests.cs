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
    public void Labels_GameText_ShouldNeverSayApple()
    {
        var definitions = Glossary.Parse(EmbeddedContent.Glossary()).Entries.Select(entry => entry.Definition);

        Labels().Concat(definitions).ShouldAllBe(text => !text.Contains("apple", StringComparison.OrdinalIgnoreCase));
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
