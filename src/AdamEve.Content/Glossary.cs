using System.Text.Json;
using AdamEve.Content.Scripture;

namespace AdamEve.Content;

/// <summary>
/// The glossary: the hard words of the King James Version with a short definition each. It marks the words in a
/// verse's text without changing a character of it.
/// </summary>
public sealed class Glossary
{
    private readonly GlossaryEntry[] longestFirst;

    private Glossary(IReadOnlyList<GlossaryEntry> entries)
    {
        Entries = entries;
        longestFirst = [.. entries.OrderByDescending(entry => entry.Term.Length)];
    }

    /// <summary>The entries, in the order of the file.</summary>
    public IReadOnlyList<GlossaryEntry> Entries { get; }

    /// <summary>Reads <c>content/glossary.json</c>: one JSON object, word to definition.</summary>
    /// <param name="utf8Json">The bytes of the file.</param>
    /// <returns>The glossary.</returns>
    /// <exception cref="ContentFormatException">
    /// The bytes are not one JSON object of strings, or a word or a definition is empty, or a word is there twice.
    /// </exception>
    public static Glossary Parse(ReadOnlySpan<byte> utf8Json)
    {
        var entries = new List<GlossaryEntry>();
        try
        {
            var reader = new Utf8JsonReader(utf8Json);
            using var json = JsonDocument.ParseValue(ref reader);
            if (json.RootElement.ValueKind != JsonValueKind.Object || reader.Read())
            {
                throw new ContentFormatException("The glossary is not one JSON object.");
            }

            foreach (var property in json.RootElement.EnumerateObject())
            {
                var definition = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                if (string.IsNullOrWhiteSpace(property.Name) || string.IsNullOrWhiteSpace(definition)
                    || property.Name != property.Name.Trim() || definition != definition.Trim())
                {
                    throw new ContentFormatException($"The glossary entry \"{property.Name}\" is not a word with a definition.");
                }

                if (entries.Any(entry => entry.Term == property.Name))
                {
                    throw new ContentFormatException($"The glossary has \"{property.Name}\" twice.");
                }

                entries.Add(new GlossaryEntry(property.Name, definition));
            }
        }
        catch (JsonException exception)
        {
            throw new ContentFormatException("The glossary is not JSON.", exception);
        }

        return new Glossary(entries);
    }

    /// <summary>
    /// Cuts a text into runs: plain text and glossary words. A word is matched as written (ordinal), as whole words
    /// only, the longest entry first. The runs, put together, are the text.
    /// </summary>
    /// <param name="text">The text of a verse.</param>
    /// <returns>The runs, in order.</returns>
    public IReadOnlyList<TextSegment> Segment(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var segments = new List<TextSegment>();
        var plainFrom = 0;
        var index = 0;
        while (index < text.Length)
        {
            var entry = EntryAt(text, index);
            if (entry is null)
            {
                index++;
                continue;
            }

            if (index > plainFrom)
            {
                segments.Add(new TextSegment(text[plainFrom..index], null));
            }

            segments.Add(new TextSegment(text.Substring(index, entry.Term.Length), entry));
            index += entry.Term.Length;
            plainFrom = index;
        }

        if (plainFrom < text.Length)
        {
            segments.Add(new TextSegment(text[plainFrom..], null));
        }

        return segments;
    }

    /// <summary>The entries whose word occurs in no verse. A glossary that ships has none.</summary>
    /// <param name="scripture">The parsed canonical text.</param>
    /// <returns>The entries without a verse.</returns>
    public IReadOnlyList<GlossaryEntry> EntriesWithoutAVerse(ScriptureDocument scripture)
    {
        ArgumentNullException.ThrowIfNull(scripture);
        var found = scripture.Verses
            .SelectMany(verse => Segment(verse.Text))
            .Where(segment => segment.Entry is not null)
            .Select(segment => segment.Entry!)
            .ToHashSet();
        return [.. Entries.Where(entry => !found.Contains(entry))];
    }

    private GlossaryEntry? EntryAt(string text, int index)
    {
        if (index > 0 && char.IsLetter(text[index - 1]))
        {
            return null;
        }

        foreach (var entry in longestFirst)
        {
            var end = index + entry.Term.Length;
            if (end <= text.Length
                && text.AsSpan(index).StartsWith(entry.Term, StringComparison.Ordinal)
                && (end == text.Length || !char.IsLetter(text[end])))
            {
                return entry;
            }
        }

        return null;
    }
}
