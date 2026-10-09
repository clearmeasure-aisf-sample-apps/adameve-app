using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AdamEve.Content.Scripture;

/// <summary>
/// Reads the canonical text, Genesis 1 to 3 in the King James Version, into a <see cref="ScriptureDocument"/>. It
/// refuses anything that is not laid out as the canonical file is, so a changed file is detected and never silently
/// accepted.
/// </summary>
public static partial class KjvParser
{
    private static readonly int[] VersesOfChapter = [31, 25, 24];

    /// <summary>Parses the bytes of the canonical text.</summary>
    /// <param name="bytes">The bytes of the file.</param>
    /// <returns>The title, the paragraphs as written and the 80 verses.</returns>
    /// <exception cref="ContentFormatException">
    /// The bytes have a byte-order mark, a carriage return or invalid UTF-8; the layout is not a title, two blank
    /// lines and paragraphs with one blank line between them; a paragraph does not start with a verse marker; or the
    /// markers do not run 1:1 to 1:31, 2:1 to 2:25 and 3:1 to 3:24 without a gap or a repeat.
    /// </exception>
    public static ScriptureDocument Parse(ReadOnlySpan<byte> bytes)
    {
        var lines = Lines(bytes);
        if (lines.Length < 4 || lines[0].Length == 0 || lines[1].Length != 0 || lines[2].Length != 0)
        {
            throw new ContentFormatException("The text does not start with a title line and two blank lines.");
        }

        var paragraphs = new List<ScriptureParagraph>();
        var verses = new List<Verse>();
        var current = new List<string>();
        foreach (var line in lines.Skip(3))
        {
            if (line.Length != 0)
            {
                current.Add(line);
                continue;
            }

            if (current.Count == 0)
            {
                throw new ContentFormatException("The text has two blank lines between paragraphs.");
            }

            paragraphs.Add(Paragraph(current, verses));
            current = [];
        }

        if (current.Count == 0)
        {
            throw new ContentFormatException("The text does not end with a paragraph and one newline.");
        }

        paragraphs.Add(Paragraph(current, verses));
        CheckOrder(verses);

        var document = new ScriptureDocument(lines[0], paragraphs, verses);
        if (!document.Render().AsSpan().SequenceEqual(bytes))
        {
            throw new ContentFormatException("The text cannot be written back byte for byte.");
        }

        return document;
    }

    [GeneratedRegex(@"(?<![\w:])(\d+):(\d+) ", RegexOptions.CultureInvariant)]
    private static partial Regex Marker();

    private static string[] Lines(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Encoding.UTF8.Preamble))
        {
            throw new ContentFormatException("The text starts with a byte-order mark.");
        }

        if (bytes.Contains((byte)'\r'))
        {
            throw new ContentFormatException("The text has a carriage return.");
        }

        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new ContentFormatException("The text is not UTF-8.", exception);
        }

        if (!text.EndsWith('\n'))
        {
            throw new ContentFormatException("The text does not end with a newline.");
        }

        var lines = text[..^1].Split('\n');
        if (lines.Any(line => line.Length != 0 && (char.IsWhiteSpace(line[0]) || char.IsWhiteSpace(line[^1]))))
        {
            throw new ContentFormatException("A line of the text starts or ends with white space.");
        }

        return lines;
    }

    private static ScriptureParagraph Paragraph(List<string> lines, List<Verse> verses)
    {
        var unwrapped = string.Join(' ', lines);
        var markers = Marker().Matches(unwrapped);
        if (markers.Count == 0 || markers[0].Index != 0)
        {
            throw new ContentFormatException($"A paragraph does not start with a verse marker: line \"{lines[0]}\".");
        }

        var refs = new List<VerseRef>(markers.Count);
        for (var index = 0; index < markers.Count; index++)
        {
            var marker = markers[index];
            var reference = new VerseRef(Number(marker.Groups[1]), Number(marker.Groups[2]));
            var start = marker.Index + marker.Length;
            var end = index + 1 < markers.Count ? markers[index + 1].Index : unwrapped.Length;
            var text = unwrapped[start..end].Trim();
            if (text.Length == 0 || text.Any(char.IsDigit))
            {
                throw new ContentFormatException($"Verse {reference} is empty or holds a digit.");
            }

            refs.Add(reference);
            verses.Add(new Verse(reference, text));
        }

        return new ScriptureParagraph(lines.ToArray(), refs);
    }

    private static int Number(Group digits) =>
        int.TryParse(digits.ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : -1;

    private static void CheckOrder(List<Verse> verses)
    {
        var expected = VersesOfChapter
            .SelectMany((count, chapter) => Enumerable.Range(1, count).Select(number => new VerseRef(chapter + 1, number)))
            .ToList();
        for (var index = 0; index < Math.Min(expected.Count, verses.Count); index++)
        {
            if (verses[index].Ref != expected[index])
            {
                throw new ContentFormatException($"Verse {verses[index].Ref} stands where {expected[index]} belongs.");
            }
        }

        if (verses.Count != expected.Count)
        {
            throw new ContentFormatException($"The text has {verses.Count} verses, not {expected.Count}.");
        }
    }
}
