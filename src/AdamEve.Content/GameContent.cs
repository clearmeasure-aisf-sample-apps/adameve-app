using System.Security.Cryptography;
using AdamEve.Content.Garden;
using AdamEve.Content.Scripture;
using AdamEve.Core.Story;

namespace AdamEve.Content;

/// <summary>
/// The content of the game, loaded and checked: the parsed canonical text, the glossary and the garden. The client loads it once
/// at start and plays nothing when it cannot.
/// </summary>
public sealed class GameContent
{
    private GameContent(ScriptureDocument scripture, Glossary glossary, GardenContent garden)
    {
        Scripture = scripture;
        Glossary = glossary;
        Garden = garden;
    }

    /// <summary>The canonical text, parsed.</summary>
    public ScriptureDocument Scripture { get; }

    /// <summary>The glossary.</summary>
    public Glossary Glossary { get; }

    /// <summary>The garden: its map, the rigs of Adam and the woman, and their animations.</summary>
    public GardenContent Garden { get; }

    /// <summary>
    /// Loads the content from bytes and checks it: the SHA-256 of the canonical text is the pinned value, the text
    /// parses, the glossary parses and each of its words occurs in at least one verse; the days of creation show
    /// the opening verses of the text in order; and the embedded garden
    /// loads (<see cref="GardenContent.Load"/>).
    /// </summary>
    /// <param name="scripture">The bytes of the canonical text.</param>
    /// <param name="glossary">The bytes of the glossary.</param>
    /// <returns>The content.</returns>
    /// <exception cref="ContentFormatException">A check failed.</exception>
    public static GameContent Load(ReadOnlySpan<byte> scripture, ReadOnlySpan<byte> glossary)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(scripture));
        if (hash != CanonicalText.Sha256)
        {
            throw new ContentFormatException("The canonical text is not the pinned file: its SHA-256 differs.");
        }

        var document = KjvParser.Parse(scripture);
        var words = Glossary.Parse(glossary);
        var strays = words.EntriesWithoutAVerse(document);
        if (strays.Count > 0)
        {
            throw new ContentFormatException($"The glossary word \"{strays[0].Term}\" occurs in no verse.");
        }

        // The story: the days of creation show the first verses of the text, each once and in order.
        var opening = document.Verses.Take(CreationStory.Cards.Count).Select(verse => new ScriptureRef(verse.Ref.Chapter, verse.Ref.Number));
        if (!opening.SequenceEqual(CreationStory.Cards))
        {
            throw new ContentFormatException("The days of creation do not show the opening verses of the canonical text in order.");
        }

        return new GameContent(document, words, GardenContent.LoadEmbedded());
    }

    /// <summary>The start-up content check: loads and checks the content this assembly embeds.</summary>
    /// <returns>The content, or the failure.</returns>
    public static ContentLoadResult LoadEmbedded()
    {
        try
        {
            return new ContentLoadResult(Load(EmbeddedContent.Scripture(), EmbeddedContent.Glossary()), null);
        }
        catch (ContentFormatException exception)
        {
            return new ContentLoadResult(null, exception.Message);
        }
    }
}
