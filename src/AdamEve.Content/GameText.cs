using AdamEve.Content.Scripture;

namespace AdamEve.Content;

/// <summary>
/// Every label the game itself wrote, for the title page, the reader and the garden. Game-written text: Jeffrey Palermo reviews each string
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

    /// <summary>The link from the title page to the garden.</summary>
    public const string GardenLink = "Walk in the garden";

    /// <summary>The menu button of the garden.</summary>
    public const string Menu = "Menu";

    /// <summary>The heading of the settings.</summary>
    public const string Settings = "Settings";

    /// <summary>The setting of the text size (design, section 1: S, M, L or XL).</summary>
    public const string TextSize = "Text size";

    /// <summary>The smallest text size.</summary>
    public const string TextSizeSmall = "S";

    /// <summary>The medium text size.</summary>
    public const string TextSizeMedium = "M";

    /// <summary>The large text size.</summary>
    public const string TextSizeLarge = "L";

    /// <summary>The largest text size.</summary>
    public const string TextSizeExtraLarge = "XL";

    /// <summary>The setting that turns sound on and off.</summary>
    public const string Sound = "Sound";

    /// <summary>The D-pad button that walks north.</summary>
    public const string Up = "Up";

    /// <summary>The D-pad button that walks south.</summary>
    public const string Down = "Down";

    /// <summary>The D-pad button that walks west.</summary>
    public const string Left = "Left";

    /// <summary>The D-pad button that walks east.</summary>
    public const string Right = "Right";

    /// <summary>The name of the region in the midst of the garden (design, section 5.1).</summary>
    public const string CentralGlade = "Central Glade";

    /// <summary>The name of the region where the river enters (design, section 5.1).</summary>
    public const string SpringOfEden = "Spring of Eden";

    /// <summary>The name of the region along the Pison (design, section 5.1).</summary>
    public const string PisonMeadows = "Pison Meadows";

    /// <summary>What a player reads when the saved game cannot be read (design, section 7.3).</summary>
    public const string SaveUnreadable = "Your saved game could not be read. Start again?";

    /// <summary>The name of a region of the garden, for the status line; empty for a place without a name.</summary>
    /// <param name="regionId">The id of the region on the map.</param>
    public static string RegionName(string? regionId) => regionId switch
    {
        "central-glade" => CentralGlade,
        "spring-of-eden" => SpringOfEden,
        "pison-meadows" => PisonMeadows,
        _ => string.Empty,
    };

    /// <summary>The reference a Scripture card carries: the book, then chapter and verse.</summary>
    /// <param name="reference">The verse.</param>
    /// <returns>For example "Genesis 3:9".</returns>
    public static string Citation(VerseRef reference) => $"{Book} {reference}";
}
