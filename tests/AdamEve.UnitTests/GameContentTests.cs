using System.Security.Cryptography;
using System.Text;
using AdamEve.Content;

namespace AdamEve.UnitTests;

[TestFixture]
public class GameContentTests
{
    [Test]
    public void Scripture_TheEmbeddedResource_ShouldBeTheCanonicalFile()
    {
        var embedded = EmbeddedContent.Scripture();

        var hash = Convert.ToHexStringLower(SHA256.HashData(embedded));

        hash.ShouldBe("b2eae7f0b5db545ca31502ef5a2f1bcb6853eee763a80a48553c13b876600e6f");
        hash.ShouldBe(CanonicalText.Sha256);
        embedded.ShouldBe(CanonicalFile.Bytes());
        embedded.Length.ShouldBe(11078);
    }

    [Test]
    public void LoadEmbedded_TheShippedContent_ShouldLoadWith80VersesAndAGlossary()
    {
        var result = GameContent.LoadEmbedded();

        result.IsLoaded.ShouldBeTrue(result.Failure);
        result.Failure.ShouldBeNull();
        result.Content.ShouldNotBeNull().Scripture.Verses.Count.ShouldBe(80);
        result.Content.Glossary.Entries.ShouldNotBeEmpty();
    }

    [Test]
    public void LoadEmbedded_TheShippedContent_ShouldFindEveryVerseTheDaysOfCreationShowAsTheParserReadsIt()
    {
        var content = GameContent.LoadEmbedded().Content.ShouldNotBeNull();
        var parsed = AdamEve.Content.Scripture.KjvParser.Parse(CanonicalFile.Bytes());

        var cards = AdamEve.Core.Story.CreationStory.Cards.Select(content.Scripture.Find).ToList();

        cards.ShouldBe(parsed.Verses.Take(34));
        cards.ShouldAllBe(verse => verse.Text == CanonicalFile.VerseTextFromTheFile(verse.Ref));
        cards[^1].Ref.ShouldBe(new AdamEve.Content.Scripture.VerseRef(2, 3));
    }

    [Test]
    public void Load_ACanonicalTextWithOneLetterChanged_ShouldBeRefused()
    {
        var bytes = CanonicalFile.Bytes();
        var letter = Array.FindIndex(bytes, value => value is >= (byte)'a' and <= (byte)'z');
        bytes[letter] = (byte)char.ToUpperInvariant((char)bytes[letter]);

        var exception = Should.Throw<ContentFormatException>(() => GameContent.Load(bytes, EmbeddedContent.Glossary()));

        exception.Message.ShouldContain("SHA-256");
    }

    [Test]
    public void Load_ATruncatedCanonicalText_ShouldBeRefused()
    {
        var half = CanonicalFile.Bytes()[..5000];

        Should.Throw<ContentFormatException>(() => GameContent.Load(half, EmbeddedContent.Glossary()));
    }

    [Test]
    public void Load_AGlossaryWordThatIsInNoVerse_ShouldBeRefused()
    {
        var glossary = Encoding.UTF8.GetBytes("""{ "zzyzx": "a word no verse has" }""");

        var exception = Should.Throw<ContentFormatException>(() => GameContent.Load(CanonicalFile.Bytes(), glossary));

        exception.Message.ShouldContain("zzyzx");
    }

    [Test]
    public void Load_ADamagedGlossary_ShouldBeRefused()
    {
        var glossary = EmbeddedContent.Glossary()[..20];

        Should.Throw<ContentFormatException>(() => GameContent.Load(CanonicalFile.Bytes(), glossary));
    }
}
