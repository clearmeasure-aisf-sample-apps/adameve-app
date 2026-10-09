namespace AdamEve.Core.World;

/// <summary>A tile of the map: column and row, from the north-west corner.</summary>
/// <param name="X">The column.</param>
/// <param name="Y">The row.</param>
public readonly record struct TilePos(int X, int Y)
{
    /// <summary>The tile as the game root shows it: "x,y".</summary>
    public override string ToString() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{X},{Y}");
}
