namespace AdamEve.Core.World;

/// <summary>
/// Tap-to-move: A* on the tile grid, in eight directions, around water and trees. A diagonal step never cuts a
/// corner: both tiles beside it must be walkable.
/// </summary>
public static class Pathfinder
{
    private static readonly double Diagonal = Math.Sqrt(2);

    /// <summary>Whether a character on <paramref name="from"/> may take one step of (dx, dy).</summary>
    /// <param name="map">The map.</param>
    /// <param name="from">The tile the character stands on.</param>
    /// <param name="dx">The column step: -1, 0 or 1.</param>
    /// <param name="dy">The row step: -1, 0 or 1.</param>
    /// <param name="obstacle">A tile another character stands on, or null.</param>
    public static bool CanStep(TileMap map, TilePos from, int dx, int dy, TilePos? obstacle)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (dx == 0 && dy == 0)
        {
            return false;
        }

        if (!Free(map, new TilePos(from.X + dx, from.Y + dy), obstacle))
        {
            return false;
        }

        return dx == 0 || dy == 0
            || (Free(map, new TilePos(from.X + dx, from.Y), obstacle) && Free(map, new TilePos(from.X, from.Y + dy), obstacle));
    }

    /// <summary>The shortest walk from one tile to another.</summary>
    /// <param name="map">The map.</param>
    /// <param name="start">The tile the character stands on.</param>
    /// <param name="goal">The tile to reach.</param>
    /// <param name="obstacle">A tile another character stands on, or null.</param>
    /// <returns>The tiles to step on, in order, the goal last; empty when there is no way or no need.</returns>
    public static IReadOnlyList<TilePos> FindPath(TileMap map, TilePos start, TilePos goal, TilePos? obstacle)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (start == goal || !map.IsWalkable(start) || !Free(map, goal, obstacle))
        {
            return [];
        }

        var count = map.Width * map.Height;
        var cost = new double[count];
        var cameFrom = new int[count];
        var closed = new bool[count];
        Array.Fill(cost, double.PositiveInfinity);
        Array.Fill(cameFrom, -1);
        var open = new PriorityQueue<int, double>();
        var startIndex = Index(map, start);
        var goalIndex = Index(map, goal);
        cost[startIndex] = 0;
        open.Enqueue(startIndex, Estimate(start, goal));

        while (open.TryDequeue(out var current, out _))
        {
            if (current == goalIndex)
            {
                break;
            }

            if (closed[current])
            {
                continue;
            }

            closed[current] = true;
            var tile = new TilePos(current % map.Width, current / map.Width);
            foreach (var facing in Facings.All)
            {
                var dx = facing.DeltaX();
                var dy = facing.DeltaY();
                if (!CanStep(map, tile, dx, dy, obstacle))
                {
                    continue;
                }

                var next = new TilePos(tile.X + dx, tile.Y + dy);
                var nextIndex = Index(map, next);
                var nextCost = cost[current] + (dx != 0 && dy != 0 ? Diagonal : 1);
                if (nextCost < cost[nextIndex] - 1e-9)
                {
                    cost[nextIndex] = nextCost;
                    cameFrom[nextIndex] = current;
                    open.Enqueue(nextIndex, nextCost + Estimate(next, goal));
                }
            }
        }

        if (cameFrom[goalIndex] < 0)
        {
            return [];
        }

        var path = new List<TilePos>();
        for (var index = goalIndex; index != startIndex; index = cameFrom[index])
        {
            path.Add(new TilePos(index % map.Width, index / map.Width));
        }

        path.Reverse();
        return path;
    }

    private static bool Free(TileMap map, TilePos tile, TilePos? obstacle) => map.IsWalkable(tile) && tile != obstacle;

    private static int Index(TileMap map, TilePos tile) => (tile.Y * map.Width) + tile.X;

    private static double Estimate(TilePos from, TilePos to)
    {
        var dx = Math.Abs(from.X - to.X);
        var dy = Math.Abs(from.Y - to.Y);
        return Math.Max(dx, dy) + ((Diagonal - 1) * Math.Min(dx, dy));
    }
}
