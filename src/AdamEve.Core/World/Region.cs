namespace AdamEve.Core.World;

/// <summary>A region of the garden (design, section 5.1): a rectangle of tiles with an id.</summary>
/// <param name="Id">The id, for example "central-glade".</param>
/// <param name="X">The first column.</param>
/// <param name="Y">The first row.</param>
/// <param name="Width">The number of columns.</param>
/// <param name="Height">The number of rows.</param>
public sealed record Region(string Id, int X, int Y, int Width, int Height)
{
    /// <summary>Whether the tile lies in the region.</summary>
    /// <param name="tile">The tile.</param>
    public bool Contains(TilePos tile) => tile.X >= X && tile.X < X + Width && tile.Y >= Y && tile.Y < Y + Height;
}
