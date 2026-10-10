using AdamEve.Content.Scripture;
using AdamEve.Core.Saves;
using AdamEve.Core.Story;

namespace AdamEve.Content;

/// <summary>
/// Every label the game itself wrote, for the title, the days of creation, the reader and the garden. Game-written text: Jeffrey Palermo reviews each string
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

    /// <summary>The character select: the man (design, decision D3).</summary>
    public const string CharacterAdam = "Adam";

    /// <summary>The character select: the woman, as in the game's title (design, decision D3).</summary>
    public const string CharacterWoman = "The woman";

    /// <summary>The character select: the small line beneath "The woman" (design, decision D3).</summary>
    public const string CharacterWomanNote = "named Eve in Genesis 3:20";

    /// <summary>The title: the link that goes on with the saved game.</summary>
    public const string Continue = "Continue";

    /// <summary>The speaker label of a Scripture card of chapter 1 on which God speaks (design, section 1).</summary>
    public const string SpeakerGod = "God";

    /// <summary>The days of creation: the control of the one gesture of a day. The player reveals what God made.</summary>
    public const string Reveal = "Reveal";

    /// <summary>The days of creation: the control that shows the next Scripture card.</summary>
    public const string TurnThePage = "Turn the page";

    /// <summary>The days of creation: the control that leaves them, offered after a first completion.</summary>
    public const string Skip = "Skip";

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

    /// <summary>
    /// The setting that chooses what draws the garden. Temporary: it is there for the trial of the Three.js
    /// renderer (docs/spike-threejs.md) and goes when the trial ends.
    /// </summary>
    public const string RendererTrial = "Renderer (trial)";

    /// <summary>The trial setting: the canvas 2D renderer, the default.</summary>
    public const string RendererCanvas = "Canvas";

    /// <summary>The trial setting: the Three.js renderer.</summary>
    public const string RendererThree = "Three.js";

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

    /// <summary>The name on the character select.</summary>
    /// <param name="character">The character.</param>
    public static string CharacterName(PlayerCharacter character) => character == PlayerCharacter.Adam ? CharacterAdam : CharacterWoman;

    /// <summary>The speaker label of a Scripture card.</summary>
    /// <param name="speaker">Who speaks in the verse.</param>
    public static string SpeakerName(StorySpeaker speaker) => speaker switch
    {
        StorySpeaker.God => SpeakerGod,
        _ => string.Empty,
    };

    /// <summary>The reference a Scripture card carries: the book, then chapter and verse.</summary>
    /// <param name="reference">The verse.</param>
    /// <returns>For example "Genesis 3:9".</returns>
    public static string Citation(VerseRef reference) => $"{Book} {reference}";
}
