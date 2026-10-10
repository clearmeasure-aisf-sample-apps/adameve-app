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

    /// <summary>
    /// Header: where the characters stand, two numbers for each character in the order of their numbers: the
    /// logical pixel of the map under its feet (x, then y). A renderer with depth stands the flat figure there; the
    /// canvas renderer does not read it.
    /// </summary>
    public const int Anchors = 10;

    /// <summary>How many characters the header has an anchor for.</summary>
    public const int AnchorCount = 2;

    /// <summary>
    /// Header: the projection the frame was composed for: <see cref="Input.InputBlock.Perspective"/> or
    /// <see cref="Input.InputBlock.Flat"/>, as the renderer asked in the input block.
    /// </summary>
    public const int Projection = 14;

    /// <summary>Header: the eye of the perspective camera, its x over the map (<see cref="World.PerspectiveCamera"/>).</summary>
    public const int EyeX = 16;

    /// <summary>Header: the eye of the perspective camera, its height above the ground.</summary>
    public const int EyeHeight = 17;

    /// <summary>Header: the eye of the perspective camera, its y over the map (south of what it looks at).</summary>
    public const int EyeY = 18;

    /// <summary>Header: how far the perspective camera looks down from the horizontal, in radians.</summary>
    public const int Tilt = 19;

    /// <summary>Header: the field of view of the perspective camera from top to bottom, in radians.</summary>
    public const int FieldOfView = 20;

    /// <summary>Header: the depth at which the haze begins.</summary>
    public const int HazeStart = 21;

    /// <summary>Header: the depth at which the haze closes; the far layer (hills and sky) stands there.</summary>
    public const int HazeEnd = 22;

    /// <summary>
    /// Header: the height above its feet of the point whose depth a figure has for hiding and being hidden
    /// (<see cref="World.PerspectiveCamera.FigureDepthHeight"/>).
    /// </summary>
    public const int FigureDepthHeight = 23;

    /// <summary>The length of the header.</summary>
    public const int HeaderLength = 32;

    /// <summary>Entry: the id of the image in the atlas.</summary>
    public const int AtlasId = 0;

    /// <summary>Entry: the first of the six numbers of the transform (a b c d e f), in logical pixels of the map.</summary>
    public const int Transform = 1;

    /// <summary>
    /// Entry: whose part the entry is: 0 for scenery, otherwise the number of the character (1 for the first
    /// anchor, 2 for the second). A renderer with depth keeps the parts of one character in one plane.
    /// </summary>
    public const int Character = 7;

    /// <summary>The length of an entry.</summary>
    public const int EntryLength = 8;

    /// <summary>The most entries a frame may hold.</summary>
    public const int Capacity = 480;

    /// <summary>The length of the whole block.</summary>
    public const int Length = HeaderLength + (Capacity * EntryLength);
}
