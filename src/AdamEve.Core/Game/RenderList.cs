namespace AdamEve.Core.Game;

/// <summary>
/// The layout of the render list (design, section 7.1): one pre-allocated block of numbers the game writes each
/// frame and the browser reads without a call for each sprite. A header, then one entry for each thing to draw, in
/// the order to draw it.
/// </summary>
public static class RenderList
{
    /// <summary>Header: how many entries follow.</summary>
    public const int Count = 0;

    /// <summary>Header: the logical pixel of the map at the left edge of the play area.</summary>
    public const int CameraX = 1;

    /// <summary>Header: the logical pixel of the map at the top edge of the play area.</summary>
    public const int CameraY = 2;

    /// <summary>Header: play-area pixels for one logical pixel.</summary>
    public const int Scale = 3;

    /// <summary>Header: the column of the player's tile.</summary>
    public const int PlayerTileX = 4;

    /// <summary>Header: the row of the player's tile.</summary>
    public const int PlayerTileY = 5;

    /// <summary>Header: the M1 verdict of the frame: 0 for ok, otherwise the index of its name.</summary>
    public const int Concealment = 6;

    /// <summary>Header: the player's facing.</summary>
    public const int Facing = 7;

    /// <summary>Header: how many frames so far had a verdict other than ok.</summary>
    public const int ExposedFrames = 8;

    /// <summary>Header: 1 while the player is between two tiles.</summary>
    public const int Moving = 9;

    /// <summary>The length of the header.</summary>
    public const int HeaderLength = 16;

    /// <summary>Entry: the id of the image in the atlas.</summary>
    public const int AtlasId = 0;

    /// <summary>Entry: the first of the six numbers of the transform (a b c d e f), in logical pixels of the map.</summary>
    public const int Transform = 1;

    /// <summary>Entry: flags.</summary>
    public const int Flags = 7;

    /// <summary>The length of an entry.</summary>
    public const int EntryLength = 8;

    /// <summary>Flag: the entry belongs to the occluder layer, drawn after every character and sprite.</summary>
    public const int OccluderLayer = 1;

    /// <summary>Flag: the entry is part of the default foliage cluster of a frame that failed closed.</summary>
    public const int FailClosed = 2;

    /// <summary>The most entries a frame may hold.</summary>
    public const int Capacity = 480;

    /// <summary>The length of the whole block.</summary>
    public const int Length = HeaderLength + (Capacity * EntryLength);
}
