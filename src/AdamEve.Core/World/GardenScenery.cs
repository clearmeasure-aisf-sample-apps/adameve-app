namespace AdamEve.Core.World;

/// <summary>
/// What stands on a tile of the garden beside the ground itself: a fact of the world, the same for every renderer.
/// The order is the order the renderers know the kinds by.
/// </summary>
public enum SceneryKind
{
    /// <summary>Nothing stands there.</summary>
    None,

    /// <summary>A broad tree with a wide crown.</summary>
    BroadTree,

    /// <summary>A tall, slender tree.</summary>
    TallTree,

    /// <summary>A tree that bears fruit (Genesis 2:9, "good for food").</summary>
    FruitTree,

    /// <summary>A palm.</summary>
    PalmTree,

    /// <summary>The fig tree near the two trees.</summary>
    FigTree,

    /// <summary>The tree of life, in the midst of the garden (Genesis 2:9). Shown, never interactive (decision D14).</summary>
    TreeOfLife,

    /// <summary>The tree of the knowledge of good and evil, in the midst of the garden (Genesis 2:9). No fruit is drawn yet.</summary>
    TreeOfKnowledge,

    /// <summary>A tree of the dense growth around the open regions.</summary>
    ForestTree,

    /// <summary>A flowering shrub at the edge of the dense growth.</summary>
    Shrub,

    /// <summary>A rock with moss.</summary>
    Rock,

    /// <summary>Reeds at the water's edge.</summary>
    Reeds,
}

/// <summary>
/// The planting of the garden (design, sections 5.1 and 5.2): which kind of tree stands on each tree tile, what
/// grows at the edge of the thicket and of the river, and where the flowers lie in drifts of one colour. It is made
/// from the map and one fixed seed, so it is the same garden in every visit, on every device and in every test.
/// <para>
/// It changes no rule. Everything that has height stands on a tile nobody can walk on (a tree tile, the thicket,
/// the river); on the ground a character walks on there are only flowers. The two trees in the midst of the garden
/// and the fig tree stand where the map puts them.
/// </para>
/// </summary>
public sealed class GardenScenery
{
    /// <summary>The seed of the planting. Changing it plants another garden.</summary>
    public const int Seed = 20261010;

    /// <summary>How many colours the drifts of flowers have.</summary>
    public const int DriftColours = 5;

    /// <summary>In <see cref="CoverAt"/>: the flowers stand thick (a flower tile of the map), not scattered.</summary>
    public const int Dense = 8;

    private static readonly (int X, int Y)[] Around = [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)];
    private static readonly (int X, int Y)[] Beside = [(0, -1), (-1, 0), (1, 0), (0, 1)];

    private readonly TileMap map;
    private readonly SceneryKind[] kinds;
    private readonly int[] cover;

    /// <summary>Plants the garden of a map.</summary>
    /// <param name="map">The map.</param>
    public GardenScenery(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        this.map = map;
        kinds = new SceneryKind[map.Width * map.Height];
        cover = new int[map.Width * map.Height];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var tile = new TilePos(x, y);
                kinds[(y * map.Width) + x] = Plant(tile);
                cover[(y * map.Width) + x] = Flowers(tile);
            }
        }
    }

    /// <summary>What stands on a tile; nothing outside the map.</summary>
    /// <param name="tile">The tile.</param>
    public SceneryKind KindAt(TilePos tile) => map.Contains(tile) ? kinds[(tile.Y * map.Width) + tile.X] : SceneryKind.None;

    /// <summary>
    /// The flowers of a tile: 0 for none, otherwise the colour of its drift (1 to <see cref="DriftColours"/>), with
    /// <see cref="Dense"/> added on a flower tile of the map.
    /// </summary>
    /// <param name="tile">The tile.</param>
    public int CoverAt(TilePos tile) => map.Contains(tile) ? cover[(tile.Y * map.Width) + tile.X] : 0;

    /// <summary>The flowers of every tile, row by row, for the renderer (<see cref="CoverAt"/>).</summary>
    public int[] ToCoverNumbers() => [.. cover];

    /// <summary>A number in [0, 1) from a tile and a purpose: the same every time.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="salt">The purpose.</param>
    public static double Chance(int x, int y, int salt)
    {
        unchecked
        {
            var n = (uint)((x * 374761393) + (y * 668265263) + (salt * 1442695041) + Seed);
            n = (n ^ (n >> 13)) * 1274126177u;
            n ^= n >> 16;
            return n / 4294967296.0;
        }
    }

    private SceneryKind Plant(TilePos tile)
    {
        var chance = Chance(tile.X, tile.Y, 1);
        switch (map.KindAt(tile))
        {
            case TileKind.TreeOfLife:
                return SceneryKind.TreeOfLife;
            case TileKind.TreeOfKnowledge:
                return SceneryKind.TreeOfKnowledge;
            case TileKind.FigTree:
                return SceneryKind.FigTree;
            case TileKind.Tree:
                // Each region has its own mix: palms and tall trees by the spring, broad and fruit trees elsewhere.
                var (broad, tall, fruit) = map.RegionAt(tile)?.Id == "spring-of-eden" ? (0.25, 0.55, 0.65) : (0.38, 0.58, 0.88);
                return chance < broad ? SceneryKind.BroadTree : chance < tall ? SceneryKind.TallTree : chance < fruit ? SceneryKind.FruitTree : SceneryKind.PalmTree;
            case TileKind.Thicket:
                if (Around.Any(step => map.KindAt(new TilePos(tile.X + step.X, tile.Y + step.Y)) != TileKind.Thicket))
                {
                    return chance < 0.42 ? SceneryKind.Shrub : chance < 0.5 ? SceneryKind.Rock : SceneryKind.None;
                }

                // The forest stands behind the open ground, never before it: a tree south of a place a character can
                // walk on would stand between that place and the viewer.
                for (var north = 1; north <= 7; north++)
                {
                    for (var across = -2; across <= 2; across++)
                    {
                        if (map.IsWalkable(new TilePos(tile.X + across, tile.Y - north)))
                        {
                            return SceneryKind.None;
                        }
                    }
                }

                return chance < 0.3 ? SceneryKind.ForestTree : SceneryKind.None;
            case TileKind.Water:
                // The crossings stay clear, so they are read as crossings.
                var beside = Beside.Select(step => map.KindAt(new TilePos(tile.X + step.X, tile.Y + step.Y))).ToList();
                if (beside.Contains(TileKind.Crossing) || beside.All(kind => kind == TileKind.Water))
                {
                    return SceneryKind.None;
                }

                return chance < 0.24 ? SceneryKind.Reeds : chance < 0.3 ? SceneryKind.Rock : SceneryKind.None;
            default:
                return SceneryKind.None;
        }
    }

    private int Flowers(TilePos tile)
    {
        var kind = map.KindAt(tile);
        if (kind is not (TileKind.Grass or TileKind.Flowers))
        {
            return 0;
        }

        // The colour changes slowly across the garden: neighbouring flowers are of one drift.
        var colour = 1 + (int)Math.Min(DriftColours - 1, Math.Floor(Smooth(tile.X / 6.0, tile.Y / 6.0) * DriftColours));
        if (kind == TileKind.Flowers)
        {
            return colour | Dense;
        }

        var near = Around.Any(step => map.KindAt(new TilePos(tile.X + step.X, tile.Y + step.Y)) == TileKind.Flowers);
        return near && Chance(tile.X, tile.Y, 2) < 0.6 ? colour : 0;
    }

    // A value that changes smoothly from place to place, in [0, 1).
    private static double Smooth(double x, double y)
    {
        var column = (int)Math.Floor(x);
        var row = (int)Math.Floor(y);
        var alongX = x - column;
        var alongY = y - row;
        alongX = alongX * alongX * (3 - (2 * alongX));
        alongY = alongY * alongY * (3 - (2 * alongY));
        var top = Chance(column, row, 3) + ((Chance(column + 1, row, 3) - Chance(column, row, 3)) * alongX);
        var bottom = Chance(column, row + 1, 3) + ((Chance(column + 1, row + 1, 3) - Chance(column, row + 1, 3)) * alongX);
        return top + ((bottom - top) * alongY);
    }
}
