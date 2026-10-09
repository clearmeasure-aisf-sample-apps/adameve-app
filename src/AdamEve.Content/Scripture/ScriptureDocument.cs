using System.Diagnostics.CodeAnalysis;
using System.Text;
using AdamEve.Core.Story;

namespace AdamEve.Content.Scripture;

/// <summary>
/// The canonical text as parsed by <see cref="KjvParser"/>: its title, its paragraphs as written and its verses.
/// </summary>
public sealed class ScriptureDocument
{
    private readonly Dictionary<VerseRef, Verse> byRef;

    internal ScriptureDocument(string title, IReadOnlyList<ScriptureParagraph> paragraphs, IReadOnlyList<Verse> verses)
    {
        Title = title;
        Paragraphs = paragraphs;
        Verses = verses;
        byRef = verses.ToDictionary(verse => verse.Ref);
    }

    /// <summary>The first line of the file.</summary>
    public string Title { get; }

    /// <summary>The paragraphs, in the order of the file, each with its original lines.</summary>
    public IReadOnlyList<ScriptureParagraph> Paragraphs { get; }

    /// <summary>The verses, from 1:1 to 3:24.</summary>
    public IReadOnlyList<Verse> Verses { get; }

    /// <summary>The verse with a reference.</summary>
    /// <param name="reference">The reference.</param>
    /// <exception cref="KeyNotFoundException">The text has no such verse.</exception>
    public Verse Find(VerseRef reference) => byRef.TryGetValue(reference, out var verse)
        ? verse
        : throw new KeyNotFoundException($"The canonical text has no verse {reference}.");

    /// <summary>The verse a beat of the story names.</summary>
    /// <param name="reference">The reference, as the story holds it.</param>
    /// <exception cref="KeyNotFoundException">The text has no such verse.</exception>
    public Verse Find(ScriptureRef reference) => Find(new VerseRef(reference.Chapter, reference.Verse));

    /// <summary>Finds the verse with a reference.</summary>
    /// <param name="reference">The reference.</param>
    /// <param name="verse">The verse, when the text has it.</param>
    /// <returns>Whether the text has the verse.</returns>
    public bool TryGetVerse(VerseRef reference, [NotNullWhen(true)] out Verse? verse) => byRef.TryGetValue(reference, out verse);

    /// <summary>
    /// Writes the document back as the file was: the title, two blank lines, the paragraphs with one blank line
    /// between them, LF line endings, one trailing newline, UTF-8 without a byte-order mark.
    /// </summary>
    /// <returns>The bytes of the file.</returns>
    public byte[] Render()
    {
        var text = new StringBuilder();
        text.Append(Title).Append("\n\n");
        foreach (var paragraph in Paragraphs)
        {
            text.Append('\n');
            foreach (var line in paragraph.Lines)
            {
                text.Append(line).Append('\n');
            }
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text.ToString());
    }
}
