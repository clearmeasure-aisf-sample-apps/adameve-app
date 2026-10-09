namespace AdamEve.Core.World;

/// <summary>The map of the garden: a grid of tiles, its regions and the places where the characters start.</summary>
public sealed class TileMap
{
    private readonly TileKind[] tiles;
    private readonly Dictionary<string, TilePos> spawns;

    /// <summary>Makes a map.</summary>
    /// <param name="width">The number of columns.</param>
    /// <param name="height">The number of rows.</param>
    /// <param name="tileSize">The side of a tile in logical pixels.</param>
    /// <param name="tiles">The tiles, row by row.</param>
    /// <param name="regions">The regions.</param>
    /// <param name="spawns">The starting tiles, by id.</param>
    /// <exception cref="ArgumentException">The sizes do not agree, or a starting tile is not walkable.</exception>
    public TileMap(int width, int height, int tileSize, IReadOnlyList<TileKind> tiles, IReadOnlyList<Region> regions, IReadOnlyDictionary<string, TilePos> spawns)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(spawns);
        if (width <= 0 || height <= 0 || tileSize <= 0 || tiles.Count != width * height)
        {
            throw new ArgumentException("The map's tiles do not fill its width and height.", nameof(tiles));
        }

        Width = width;
        Height = height;
        TileSize = tileSize;
        this.tiles = [.. tiles];
        Regions = [.. regions];
        this.spawns = new Dictionary<string, TilePos>(spawns, StringComparer.Ordinal);
        foreach (var spawn in this.spawns)
        {
            if (!IsWalkable(spawn.Value))
            {
                throw new ArgumentException($"The starting tile \"{spawn.Key}\" is not walkable.", nameof(spawns));
            }
        }
    }

    /// <summary>The number of columns.</summary>
    public int Width { get; }

    /// <summary>The number of rows.</summary>
    public int Height { get; }

    /// <summary>The side of a tile in logical pixels.</summary>
    public int TileSize { get; }

    /// <summary>The regions.</summary>
    public IReadOnlyList<Region> Regions { get; }

    /// <summary>The ids of the starting tiles.</summary>
    public IReadOnlyCollection<string> SpawnIds => spawns.Keys;

    /// <summary>Whether the tile lies on the map.</summary>
    /// <param name="tile">The tile.</param>
    public bool Contains(TilePos tile) => tile.X >= 0 && tile.Y >= 0 && tile.X < Width && tile.Y < Height;

    /// <summary>The kind of a tile; thicket outside the map.</summary>
    /// <param name="tile">The tile.</param>
    public TileKind KindAt(TilePos tile) => Contains(tile) ? tiles[(tile.Y * Width) + tile.X] : TileKind.Thicket;

    /// <summary>Whether a character may stand on the tile.</summary>
    /// <param name="tile">The tile.</param>
    public bool IsWalkable(TilePos tile) => Contains(tile) && tiles[(tile.Y * Width) + tile.X].IsWalkable();

    /// <summary>The starting tile with this id.</summary>
    /// <param name="id">The id, for example "adam".</param>
    /// <exception cref="KeyNotFoundException">The map has no such starting tile.</exception>
    public TilePos Spawn(string id) => spawns[id];

    /// <summary>The first region that holds the tile, or null.</summary>
    /// <param name="tile">The tile.</param>
    public Region? RegionAt(TilePos tile)
    {
        foreach (var region in Regions)
        {
            if (region.Contains(tile))
            {
                return region;
            }
        }

        return null;
    }

    /// <summary>The tiles as numbers, row by row, for the renderer.</summary>
    public int[] ToKindNumbers() => [.. tiles.Select(kind => (int)kind)];
}
