using AdamEve.Core.Story;
using AdamEve.Core.World;

namespace AdamEve.Core.Saves;

/// <summary>Whom the player plays.</summary>
public enum PlayerCharacter
{
    /// <summary>Adam.</summary>
    Adam,

    /// <summary>The woman (named Eve in Genesis 3:20).</summary>
    Woman,
}

/// <summary>
/// The saved game (design, section 7.3). It holds numbers, choices from fixed lists and ids of the content's lists
/// only: no name, no free text.
/// It is also the scenario format of the tests, so the game has no test-only way in.
/// </summary>
public sealed record SaveGame
{
    /// <summary>The version of this format.</summary>
    public int SchemaVersion { get; init; } = SaveCodec.CurrentVersion;

    /// <summary>Whom the player plays.</summary>
    public PlayerCharacter Character { get; init; }

    /// <summary>The column of the player's tile.</summary>
    public int TileX { get; init; }

    /// <summary>The row of the player's tile.</summary>
    public int TileY { get; init; }

    /// <summary>The way the player faces.</summary>
    public Facing Facing { get; init; } = Facing.S;

    /// <summary>
    /// The chapter of the story. A saved game of slice S2 has none: it was made walking the garden with both
    /// characters in it, which is life in the garden. (Settable, like the properties after it, so that a saved game
    /// without the property keeps this value when it is read: the generated reader gives an init-only property the
    /// default of its type instead.)
    /// </summary>
    public StoryChapter Chapter { get; set; } = StoryChapter.GardenLife;

    /// <summary>The beat of the story. It is resumed from its beginning.</summary>
    public StoryBeat Beat { get; set; } = StoryBeat.B17;

    /// <summary>Whether the days of creation were watched to the end once: only then may they be skipped.</summary>
    public bool CreationWatched { get; set; }

    /// <summary>
    /// The kind-names Adam gave, one for each animal named so far, in the order the animals are brought: each is
    /// the id of one of the three kind-names of its animal (<see cref="AnimalRoster"/>), never typed text. A saved
    /// game of an earlier slice has none.
    /// </summary>
    public string[] NamedAnimals { get; set; } = [];

    /// <summary>The saved game of a state of the story, with the player on a tile.</summary>
    /// <param name="state">The state. Its character is chosen.</param>
    /// <param name="tile">The player's tile.</param>
    /// <exception cref="ArgumentException">The state is at the title: no character is chosen yet.</exception>
    public static SaveGame Of(StoryState state, TilePos tile)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Character is not { } character || !StoryMachine.IsBeatOf(state.Chapter, state.Beat))
        {
            throw new ArgumentException("The title is not saved: a saved game begins when a character is chosen.", nameof(state));
        }

        return new SaveGame
        {
            Character = character,
            TileX = tile.X,
            TileY = tile.Y,
            Chapter = state.Chapter,
            Beat = state.Beat,
            CreationWatched = state.CreationWatched,
            NamedAnimals = [.. state.NamedAnimals],
        };
    }

    /// <summary>Two saved games are equal when everything they hold is, the kind-names one by one.</summary>
    /// <param name="other">The other saved game.</param>
    public bool Equals(SaveGame? other) => other is not null
        && SchemaVersion == other.SchemaVersion && Character == other.Character && TileX == other.TileX && TileY == other.TileY
        && Facing == other.Facing && Chapter == other.Chapter && Beat == other.Beat && CreationWatched == other.CreationWatched
        && NamedAnimals.AsSpan().SequenceEqual(other.NamedAnimals);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(SchemaVersion, Character, TileX, TileY, Facing, Chapter, Beat, NamedAnimals.Length);
}

/// <summary>The size of the game's text (design, section 1).</summary>
public enum TextSize
{
    /// <summary>Small: 18 px, the minimum body size.</summary>
    S,

    /// <summary>Medium.</summary>
    M,

    /// <summary>Large.</summary>
    L,

    /// <summary>Extra large.</summary>
    XL,
}

/// <summary>The settings of the player, kept apart from the saved game.</summary>
public sealed record GameSettings
{
    /// <summary>The version of this format.</summary>
    public int SchemaVersion { get; init; } = SaveCodec.CurrentVersion;

    /// <summary>The size of the game's text.</summary>
    public TextSize TextSize { get; init; } = TextSize.M;

    /// <summary>Whether sound plays.</summary>
    public bool Sound { get; init; } = true;
}

/// <summary>What reading the saved game found.</summary>
public enum SaveState
{
    /// <summary>There is no saved game: a new game starts.</summary>
    None,

    /// <summary>The saved game was read.</summary>
    Loaded,

    /// <summary>There is a saved game and it cannot be read: it is kept aside and a new game starts.</summary>
    Corrupt,
}

/// <summary>The saved game as read.</summary>
/// <param name="State">What was found.</param>
/// <param name="Save">The saved game, when it was read.</param>
public sealed record SaveReadResult(SaveState State, SaveGame? Save);
