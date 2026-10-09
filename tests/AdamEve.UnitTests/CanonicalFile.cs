using System.Text;
using AdamEve.Content;
using AdamEve.Content.Scripture;

namespace AdamEve.UnitTests;

/// <summary>
/// The canonical file of the repository, read by the tests themselves: what a test expects of Scripture comes from
/// these bytes, never from text typed into a test.
/// </summary>
internal static class CanonicalFile
{
    private static readonly int[] VersesOfChapter = [31, 25, 24];

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AdamEve.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("AdamEve.slnx was not found above the test directory.");
    }

    public static byte[] Bytes() => File.ReadAllBytes(Path.Combine(RepositoryRoot(), CanonicalText.RepositoryPath));

    public static string Text() => Encoding.UTF8.GetString(Bytes());

    public static string[] Lines() => Text().Split('\n');

    public static IEnumerable<VerseRef> AllRefs() =>
        VersesOfChapter.SelectMany((count, chapter) => Enumerable.Range(1, count).Select(number => new VerseRef(chapter + 1, number)));

    /// <summary>The offset of a verse's marker in the text of the file: the reference after a line break or a space.</summary>
    public static int MarkerOffset(string text, VerseRef reference)
    {
        var marker = reference.ToString();
        var offset = -1;
        while ((offset = text.IndexOf(marker, offset + 1, StringComparison.Ordinal)) >= 0)
        {
            var before = text[offset - 1];
            var after = text[offset + marker.Length];
            if ((before == '\n' || before == ' ') && (after == '\n' || after == ' '))
            {
                return offset;
            }
        }

        throw new InvalidOperationException($"The file has no marker {marker}.");
    }

    /// <summary>
    /// The text of a verse cut straight out of the file, without the parser: from the end of its marker to the next
    /// verse's marker (or the end of the file), each line break a space, trimmed.
    /// </summary>
    public static string VerseTextFromTheFile(VerseRef reference)
    {
        var text = Text();
        var refs = AllRefs().ToList();
        var position = refs.IndexOf(reference);
        var start = MarkerOffset(text, reference) + reference.ToString().Length;
        var end = position + 1 < refs.Count ? MarkerOffset(text, refs[position + 1]) : text.Length;
        return string.Join(' ', text[start..end].Split('\n', StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    public static VerseRef Ref(string reference)
    {
        var parts = reference.Split(':');
        return new VerseRef(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }
}
