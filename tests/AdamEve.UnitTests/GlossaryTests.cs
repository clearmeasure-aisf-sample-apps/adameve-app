using System.Text;
using AdamEve.Content;
using AdamEve.Content.Scripture;

namespace AdamEve.UnitTests;

[TestFixture]
public class GlossaryTests
{
    private static Glossary StubGlossary(string json) => Glossary.Parse(Encoding.UTF8.GetBytes(json));

    private static Glossary Shipped() => Glossary.Parse(EmbeddedContent.Glossary());

    private static ScriptureDocument Scripture() => KjvParser.Parse(CanonicalFile.Bytes());

    [Test]
    public void Parse_TheEmbeddedGlossary_ShouldBeTheFileOfTheRepository()
    {
        var file = File.ReadAllBytes(Path.Combine(CanonicalFile.RepositoryRoot(), "content", "glossary.json"));

        var embedded = EmbeddedContent.Glossary();

        embedded.ShouldBe(file);
    }

    [Test]
    public void EntriesWithoutAVerse_TheShippedGlossary_ShouldBeEmpty()
    {
        var strays = Shipped().EntriesWithoutAVerse(Scripture());

        strays.ShouldBeEmpty();
    }

    [Test]
    public void Segment_EveryVerse_ShouldPutTogetherToTheExactTextOfTheVerse()
    {
        var glossary = Shipped();

        foreach (var verse in Scripture().Verses)
        {
            var segments = glossary.Segment(verse.Text);

            string.Concat(segments.Select(segment => segment.Text)).ShouldBe(verse.Text);
            segments.Where(segment => segment.Entry is not null).ShouldAllBe(segment => segment.Text == segment.Entry!.Term);
        }
    }

    [Test]
    public void Segment_EveryVerse_ShouldMarkEveryWholeWordOccurrenceOfEveryEntry()
    {
        var glossary = Shipped();

        foreach (var entry in glossary.Entries)
        {
            var wholeWord = new System.Text.RegularExpressions.Regex($@"(?<![A-Za-z]){System.Text.RegularExpressions.Regex.Escape(entry.Term)}(?![A-Za-z])");
            foreach (var verse in Scripture().Verses)
            {
                var marked = glossary.Segment(verse.Text).Count(segment => segment.Entry == entry);

                marked.ShouldBe(wholeWord.Count(verse.Text), $"{entry.Term} in {verse.Ref}");
            }
        }
    }

    [Test]
    public void Segment_AWordInsideALongerWord_ShouldNotBeMarked()
    {
        var glossary = StubGlossary("""{ "art": "a stub definition" }""");

        var segments = glossary.Segment("cart art arts, art");

        segments.Select(segment => segment.Text).ShouldBe(["cart ", "art", " arts, ", "art"]);
        segments.Select(segment => segment.Entry is not null).ShouldBe([false, true, false, true]);
    }

    [Test]
    public void Segment_TwoEntriesThatStartAtTheSamePlace_ShouldMarkTheLongest()
    {
        var glossary = StubGlossary("""{ "stub": "short", "stub word": "long" }""");

        var segments = glossary.Segment("a stub word, a stub");

        segments.Where(segment => segment.Entry is not null).Select(segment => segment.Entry!.Definition).ShouldBe(["long", "short"]);
    }

    [Test]
    public void Segment_AWordInAnotherCase_ShouldNotBeMarked()
    {
        var glossary = StubGlossary("""{ "Stub": "a stub definition" }""");

        var segments = glossary.Segment("stub Stub");

        segments.Select(segment => segment.Text).ShouldBe(["stub ", "Stub"]);
    }

    [Test]
    public void Segment_ATextWithoutAGlossaryWord_ShouldBeOnePlainRun()
    {
        var glossary = StubGlossary("""{ "stub": "a stub definition" }""");

        var segments = glossary.Segment("nothing here");

        segments.ShouldBe([new TextSegment("nothing here", null)]);
    }

    [TestCase("[]")]
    [TestCase("\"a string\"")]
    [TestCase("{ \"stub\": 1 }")]
    [TestCase("{ \"stub\": \"\" }")]
    [TestCase("{ \"\": \"a definition\" }")]
    [TestCase("{ \" stub\": \"a definition\" }")]
    [TestCase("{ \"stub\": \"one\", \"stub\": \"two\" }")]
    [TestCase("{ \"stub\": \"one\" } { }")]
    [TestCase("{ \"stub\": ")]
    public void Parse_AFileThatIsNotWordsWithDefinitions_ShouldBeRefused(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);

        Should.Throw<ContentFormatException>(() => Glossary.Parse(bytes));
    }
}
