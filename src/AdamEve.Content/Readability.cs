namespace AdamEve.Content;

/// <summary>
/// How hard a line of narration is to read: the Flesch-Kincaid grade (design, section 1: narration aims at grade 5
/// to 6, and a test fails above 7.0). Syllables are counted by a rule of thumb: groups of vowels, less a silent e.
/// </summary>
public static class Readability
{
    /// <summary>The highest grade a line of narration may have.</summary>
    public const double HighestGrade = 7.0;

    /// <summary>The Flesch-Kincaid grade of a text.</summary>
    /// <param name="text">The text.</param>
    public static double FleschKincaidGrade(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = text.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => new string([.. word.Where(char.IsLetter)]))
            .Where(word => word.Length > 0)
            .ToList();
        if (words.Count == 0)
        {
            return 0;
        }

        var sentences = Math.Max(1, text.Count(letter => letter is '.' or '!' or '?'));
        var syllables = words.Sum(Syllables);
        return (0.39 * words.Count / sentences) + (11.8 * syllables / words.Count) - 15.59;
    }

    /// <summary>The syllables of a word, by the rule of thumb.</summary>
    /// <param name="word">The word: letters only.</param>
    public static int Syllables(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        var lower = word.ToLowerInvariant();
        var count = 0;
        var inGroup = false;
        foreach (var letter in lower)
        {
            var vowel = letter is 'a' or 'e' or 'i' or 'o' or 'u' or 'y';
            if (vowel && !inGroup)
            {
                count++;
            }

            inGroup = vowel;
        }

        if (lower.Length > 2 && lower.EndsWith('e') && !lower.EndsWith("le", StringComparison.Ordinal) && count > 1)
        {
            count--;
        }

        return Math.Max(1, count);
    }
}
