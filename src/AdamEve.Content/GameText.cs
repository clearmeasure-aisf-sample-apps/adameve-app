using AdamEve.Content.Scripture;

namespace AdamEve.Content;

/// <summary>
/// Every label the game itself wrote for the reader screen. Game-written text: Jeffrey Palermo reviews each string
/// before it ships, and <c>content/README.md</c> lists them all for that review (a unit test keeps the list whole).
/// None of it is Scripture.
/// </summary>
public static class GameText
{
    /// <summary>The name of the book in a reference: "Genesis 3:9".</summary>
    public const string Book = "Genesis";

    /// <summary>The link from the title page to the reader.</summary>
    public const string ReaderLink = "Read Genesis 1 to 3";

    /// <summary>The heading of the reader.</summary>
    public const string ReaderTitle = "Genesis 1 to 3, King James Version";

    /// <summary>The link from the reader to the title page.</summary>
    public const string BackToTitle = "Back to the title";

    /// <summary>The button that puts a glossary definition away.</summary>
    public const string CloseDefinition = "Close";

    /// <summary>What a player reads when the start-up content check fails (design, section 7.3).</summary>
    public const string ContentFailure = "The game's text could not be loaded";

    /// <summary>The reference a Scripture card carries: the book, then chapter and verse.</summary>
    /// <param name="reference">The verse.</param>
    /// <returns>For example "Genesis 3:9".</returns>
    public static string Citation(VerseRef reference) => $"{Book} {reference}";
}
