using AdamEve.Core.World;

namespace AdamEve.Core.Story;

/// <summary>
/// The places of the garden the man's path uses (beats B9 to B13): the bank of the river, where the sapling grows
/// and the branch fell, and where the animals stand. All of it follows from the map, the same every time; nothing
/// of it changes where anybody can walk.
/// </summary>
public sealed class StoryPlaces
{
    private const string Glade = "central-glade";

    private readonly TileMap map;

    /// <summary>Finds the places on a map.</summary>
    /// <param name="map">The map of the garden.</param>
    public StoryPlaces(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        this.map = map;
        var start = map.SpawnIds.Contains("adam") ? map.Spawn("adam") : new TilePos(map.Width / 2, map.Height / 2);
        var taken = new HashSet<TilePos> { start };
        Sapling = OpenGround(new TilePos(start.X + 2, start.Y - 3), taken);
        taken.Add(Sapling);
        Branch = OpenGround(new TilePos(start.X - 3, start.Y + 3), taken);
        taken.Add(Branch);
        AnimalPlaces = PlacesForPairs(start, taken);
    }

    /// <summary>The tile the sapling grows on.</summary>
    public TilePos Sapling { get; }

    /// <summary>The tile the fallen branch lies on.</summary>
    public TilePos Branch { get; }

    /// <summary>
    /// Where each animal stands once it has its name, in the order the animals are brought: its mate stands on
    /// the tile east of it.
    /// </summary>
    public IReadOnlyList<TilePos> AnimalPlaces { get; }

    /// <summary>Whether a player standing on a tile has reached a place.</summary>
    /// <param name="area">The place.</param>
    /// <param name="tile">The tile the player stands on.</param>
    public bool Reached(StoryArea area, TilePos tile) => area switch
    {
        StoryArea.River => Neighbours(tile).Any(near => map.KindAt(near) == TileKind.Water),
        StoryArea.Sapling => Beside(tile, Sapling),
        StoryArea.Branch => Beside(tile, Branch),
        _ => false,
    };

    private static bool Beside(TilePos tile, TilePos place) => Math.Abs(tile.X - place.X) <= 1 && Math.Abs(tile.Y - place.Y) <= 1;

    private static IEnumerable<TilePos> Neighbours(TilePos tile)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                yield return new TilePos(tile.X + dx, tile.Y + dy);
            }
        }
    }

    private bool IsOpen(TilePos tile) => map.KindAt(tile) is TileKind.Grass or TileKind.Flowers;

    // The nearest tile of open ground, away from the water, that nothing else has taken.
    private TilePos OpenGround(TilePos wanted, HashSet<TilePos> taken)
    {
        for (var reach = 0; reach < Math.Max(map.Width, map.Height); reach++)
        {
            for (var dy = -reach; dy <= reach; dy++)
            {
                for (var dx = -reach; dx <= reach; dx++)
                {
                    var tile = new TilePos(wanted.X + dx, wanted.Y + dy);
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == reach && IsOpen(tile) && !taken.Contains(tile)
                        && Neighbours(tile).All(near => map.KindAt(near) != TileKind.Water))
                    {
                        return tile;
                    }
                }
            }
        }

        return wanted;
    }

    // Two tiles of open ground side by side for each animal, in rows two apart, nearest the man's first tile first.
    private List<TilePos> PlacesForPairs(TilePos start, HashSet<TilePos> taken)
    {
        var region = map.Regions.FirstOrDefault(candidate => candidate.Id == Glade) ?? new Region(Glade, 0, 0, map.Width, map.Height);
        var places = new List<TilePos>();
        for (var y = region.Y + 1; y < region.Y + region.Height; y += 2)
        {
            for (var x = region.X + 1 + (((y - region.Y) / 2) % 2); x < region.X + region.Width - 1; x += 3)
            {
                var tile = new TilePos(x, y);
                var mate = new TilePos(x + 1, y);
                if (IsOpen(tile) && IsOpen(mate) && !taken.Contains(tile) && !taken.Contains(mate)
                    && Math.Max(Math.Abs(x - start.X), Math.Abs(y - start.Y)) >= 2 && Math.Max(Math.Abs(x + 1 - start.X), Math.Abs(y - start.Y)) >= 2)
                {
                    places.Add(tile);
                }
            }
        }

        return [.. places.OrderBy(tile => Math.Max(Math.Abs(tile.X - start.X), Math.Abs(tile.Y - start.Y) * 1.5)).ThenBy(tile => tile.Y).ThenBy(tile => tile.X).Take(AnimalRoster.Count)];
    }
}
