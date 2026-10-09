using System.Text;
using AdamEve.Content;
using AdamEve.Content.Scripture;

namespace AdamEve.UnitTests;

[TestFixture]
public class KjvParserTests
{
    private static readonly string[] MidParagraphMarkers =
        ["1:15", "1:18", "2:5", "2:12", "2:17", "2:22", "3:2", "3:3", "3:5", "3:10", "3:12", "3:15", "3:18", "3:19", "3:23"];

    private static IEnumerable<string> EveryRef() => CanonicalFile.AllRefs().Select(reference => reference.ToString());

    [Test]
    public void Parse_TheCanonicalFile_ShouldExtractAll80VersesInOrder()
    {
        var document = KjvParser.Parse(CanonicalFile.Bytes());

        document.Verses.Count.ShouldBe(80);
        document.Verses.Count(verse => verse.Ref.Chapter == 1).ShouldBe(31);
        document.Verses.Count(verse => verse.Ref.Chapter == 2).ShouldBe(25);
        document.Verses.Count(verse => verse.Ref.Chapter == 3).ShouldBe(24);
        document.Verses.Select(verse => verse.Ref).ShouldBe(CanonicalFile.AllRefs());
        document.Verses[0].Ref.ShouldBe(new VerseRef(1, 1));
        document.Verses[^1].Ref.ShouldBe(new VerseRef(3, 24));
    }

    [Test]
    public void Render_TheParsedCanonicalFile_ShouldReproduceTheSourceBytes()
    {
        var bytes = CanonicalFile.Bytes();

        var rendered = KjvParser.Parse(bytes).Render();

        rendered.ShouldBe(bytes);
    }

    [Test]
    public void Parse_TheCanonicalFile_ShouldKeepEveryParagraphAndEveryVerseOfIt()
    {
        var document = KjvParser.Parse(CanonicalFile.Bytes());

        foreach (var paragraph in document.Paragraphs)
        {
            var fromVerses = string.Join(' ', paragraph.Refs.Select(reference => $"{reference} {document.Find(reference).Text}"));
            fromVerses.ShouldBe(string.Join(' ', paragraph.Lines));
        }

        document.Paragraphs.SelectMany(paragraph => paragraph.Refs).ShouldBe(CanonicalFile.AllRefs());
    }

    [TestCaseSource(nameof(EveryRef))]
    public void Parse_AVerse_ShouldBeTheTextOfTheFileCharacterForCharacter(string reference)
    {
        var verseRef = CanonicalFile.Ref(reference);

        var verse = KjvParser.Parse(CanonicalFile.Bytes()).Find(verseRef);

        verse.Text.ShouldBe(CanonicalFile.VerseTextFromTheFile(verseRef));
    }

    [TestCaseSource(nameof(MidParagraphMarkers))]
    public void Parse_AMarkerInTheMiddleOfAParagraph_ShouldStartItsOwnVerse(string reference)
    {
        var verseRef = CanonicalFile.Ref(reference);
        var text = CanonicalFile.Text();
        var offset = CanonicalFile.MarkerOffset(text, verseRef);

        var document = KjvParser.Parse(CanonicalFile.Bytes());

        text[(offset - 2)..offset].ShouldNotBe("\n\n");
        var paragraph = document.Paragraphs.Single(candidate => candidate.Refs.Contains(verseRef));
        paragraph.Refs.Count.ShouldBeGreaterThan(1);
        paragraph.Refs[0].ShouldNotBe(verseRef);
        var verse = document.Find(verseRef);
        verse.Text.ShouldBe(CanonicalFile.VerseTextFromTheFile(verseRef));
        verse.Text.ShouldNotContain(reference);
        var before = document.Verses[document.Verses.ToList().IndexOf(verse) - 1];
        before.Text.ShouldNotContain(reference);
        before.Text.ShouldBe(CanonicalFile.VerseTextFromTheFile(before.Ref));
    }

    [Test]
    public void Parse_TheCanonicalFile_ShouldHaveNoMidParagraphMarkerBesideTheFifteenOfTheDesign()
    {
        var document = KjvParser.Parse(CanonicalFile.Bytes());

        var midParagraph = document.Paragraphs.SelectMany(paragraph => paragraph.Refs.Skip(1)).Select(reference => reference.ToString());

        midParagraph.ShouldBe(MidParagraphMarkers);
    }

    [Test]
    public void Parse_TheMarkerThatEndsALine_ShouldStartItsVerseOnTheNextLine()
    {
        var lines = CanonicalFile.Lines();
        var document = KjvParser.Parse(CanonicalFile.Bytes());
        var endOfVerse4 = lines[188][..^" 3:5".Length];
        if (endOfVerse4.StartsWith("3:4 ", StringComparison.Ordinal))
        {
            endOfVerse4 = endOfVerse4["3:4 ".Length..];
        }

        var verse4 = document.Find(new VerseRef(3, 4));
        var verse5 = document.Find(new VerseRef(3, 5));

        lines[188].ShouldEndWith(" 3:5");
        lines.Count(line => System.Text.RegularExpressions.Regex.IsMatch(line, @"\d+:\d+$")).ShouldBe(1);
        verse4.Text.ShouldEndWith(endOfVerse4);
        verse5.Text.ShouldStartWith(lines[189]);
        char.IsLetter(verse5.Text[0]).ShouldBeTrue();
    }

    [TestCase("subtil", "3:1")]
    [TestCase("Cherubims", "3:24")]
    [TestCase("an help meet", "2:18")]
    [TestCase("an help meet", "2:20")]
    public void Parse_AWordTheKingJamesVersionSpellsItsOwnWay_ShouldKeepTheSpelling(string glossaryTerm, string reference)
    {
        var terms = Glossary.Parse(EmbeddedContent.Glossary()).Entries.Select(entry => entry.Term);

        var verse = KjvParser.Parse(CanonicalFile.Bytes()).Find(CanonicalFile.Ref(reference));

        terms.ShouldContain(glossaryTerm);
        verse.Text.ShouldContain(glossaryTerm, Case.Sensitive);
    }

    [Test]
    public void Parse_TheNameWrittenInCapitals_ShouldKeepTheCapitalsFromChapter2Verse4()
    {
        var capitals = new System.Text.RegularExpressions.Regex("[A-Z]{4}");
        var fileBody = string.Join(' ', CanonicalFile.Lines().Skip(1));

        var document = KjvParser.Parse(CanonicalFile.Bytes());

        var withCapitals = document.Verses.Where(verse => capitals.IsMatch(verse.Text)).ToList();
        withCapitals[0].Ref.ShouldBe(new VerseRef(2, 4));
        withCapitals.Sum(verse => capitals.Count(verse.Text)).ShouldBe(capitals.Count(fileBody));
        withCapitals.SelectMany(verse => capitals.Matches(verse.Text).Select(match => match.Value)).Distinct().Count().ShouldBe(1);
    }

    [Test]
    public void Parse_TheOneCurlyApostropheOfTheFile_ShouldBeKeptInChapter3Verse20()
    {
        var bytes = CanonicalFile.Bytes();
        byte[] curlyApostrophe = [0xE2, 0x80, 0x99];

        var document = KjvParser.Parse(bytes);

        bytes.Count(value => value > 0x7F).ShouldBe(3);
        bytes.AsSpan().IndexOf(curlyApostrophe).ShouldBeGreaterThan(0);
        var verse = document.Find(new VerseRef(3, 20));
        verse.Text.Count(character => character == '’').ShouldBe(1);
        verse.Text.ShouldNotContain('\'');
        document.Verses.Where(other => other.Text.Any(character => character > '\u007F')).ShouldBe([verse]);
    }

    [Test]
    public void Parse_TheCanonicalFile_ShouldLeaveNoDigitInAVerseText()
    {
        var document = KjvParser.Parse(CanonicalFile.Bytes());

        document.Verses.ShouldAllBe(verse => !verse.Text.Any(char.IsDigit));
    }

    [Test]
    public void Parse_TheCanonicalFile_ShouldTakeTheFirstLineAsTheTitle()
    {
        var document = KjvParser.Parse(CanonicalFile.Bytes());

        document.Title.ShouldBe(CanonicalFile.Lines()[0]);
        document.Title.ShouldNotBeEmpty();
        document.Verses.ShouldAllBe(verse => !verse.Text.Contains(document.Title, StringComparison.Ordinal));
    }

    [Test]
    public void Parse_AByteOrderMark_ShouldBeRefused()
    {
        byte[] withMark = [0xEF, 0xBB, 0xBF, .. CanonicalFile.Bytes()];

        Should.Throw<ContentFormatException>(() => KjvParser.Parse(withMark));
    }

    [Test]
    public void Parse_ACarriageReturn_ShouldBeRefused()
    {
        var withCarriageReturns = Encoding.UTF8.GetBytes(CanonicalFile.Text().Replace("\n", "\r\n", StringComparison.Ordinal));

        Should.Throw<ContentFormatException>(() => KjvParser.Parse(withCarriageReturns));
    }

    [Test]
    public void Parse_InvalidUtf8_ShouldBeRefused()
    {
        var bytes = CanonicalFile.Bytes();
        bytes[bytes.AsSpan().IndexOf((byte)0xE2) + 1] = 0x20;

        Should.Throw<ContentFormatException>(() => KjvParser.Parse(bytes));
    }

    [Test]
    public void Parse_AMissingParagraph_ShouldBeRefusedAsAGap()
    {
        var text = CanonicalFile.Text();
        var start = CanonicalFile.MarkerOffset(text, new VerseRef(1, 3));
        var end = CanonicalFile.MarkerOffset(text, new VerseRef(1, 4));

        var withoutAVerse = Encoding.UTF8.GetBytes(text.Remove(start, end - start));

        Should.Throw<ContentFormatException>(() => KjvParser.Parse(withoutAVerse));
    }

    [Test]
    public void Parse_AParagraphWrittenTwice_ShouldBeRefusedAsARepeat()
    {
        var text = CanonicalFile.Text();
        var start = CanonicalFile.MarkerOffset(text, new VerseRef(1, 3));
        var end = CanonicalFile.MarkerOffset(text, new VerseRef(1, 4));

        var withARepeat = Encoding.UTF8.GetBytes(text.Insert(end, text[start..end]));

        Should.Throw<ContentFormatException>(() => KjvParser.Parse(withARepeat));
    }

    [Test]
    public void Parse_AChangedLayout_ShouldBeRefused()
    {
        var text = CanonicalFile.Text();
        var paragraphBreak = text.IndexOf("\n\n", text.IndexOf("\n\n\n", StringComparison.Ordinal) + 3, StringComparison.Ordinal);

        var withoutTheLastNewline = Encoding.UTF8.GetBytes(text[..^1]);
        var withTwoNewlinesAtTheEnd = Encoding.UTF8.GetBytes(text + "\n");
        var withAnExtraBlankLine = Encoding.UTF8.GetBytes(text.Insert(paragraphBreak, "\n"));
        var withASpaceAtTheEndOfALine = Encoding.UTF8.GetBytes(text.Insert(paragraphBreak, " "));
        var withOneBlankLineAfterTheTitle = Encoding.UTF8.GetBytes(text.Remove(text.IndexOf('\n', StringComparison.Ordinal), 1));

        Should.Throw<ContentFormatException>(() => KjvParser.Parse(withoutTheLastNewline));
        Should.Throw<ContentFormatException>(() => KjvParser.Parse(withTwoNewlinesAtTheEnd));
        Should.Throw<ContentFormatException>(() => KjvParser.Parse(withAnExtraBlankLine));
        Should.Throw<ContentFormatException>(() => KjvParser.Parse(withASpaceAtTheEndOfALine));
        Should.Throw<ContentFormatException>(() => KjvParser.Parse(withOneBlankLineAfterTheTitle));
    }
}
