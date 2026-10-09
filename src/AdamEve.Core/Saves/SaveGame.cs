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
/// The saved game (design, section 7.3). It holds numbers and choices from fixed lists only: no name, no free text.
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
