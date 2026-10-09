using AdamEve.Core.Input;

namespace AdamEve.Core.World;

/// <summary>
/// A character that walks the map tile by tile, in eight directions. A direction held, or pressed once, takes the
/// next step; a tap sets a walk to its tile. A diagonal step takes longer by the square root of two, so the speed
/// over the ground is the same in every direction.
/// </summary>
public sealed class Walker
{
    /// <summary>The walking speed, in tiles a second.</summary>
    public const double TilesPerSecond = 4;

    private readonly TileMap map;
    private readonly Queue<TilePos> path = new();
    private TilePos target;
    private TilePos? pendingTap;
    private double progress;
    private double length = 1;

    /// <summary>Puts a character on a tile.</summary>
    /// <param name="map">The map.</param>
    /// <param name="tile">The tile to stand on.</param>
    /// <param name="facing">The way to face.</param>
    public Walker(TileMap map, TilePos tile, Facing facing)
    {
        ArgumentNullException.ThrowIfNull(map);
        this.map = map;
        Tile = tile;
        target = tile;
        Facing = facing;
        X = PreviousX = tile.X;
        Y = PreviousY = tile.Y;
    }

    /// <summary>The tile the character stands on, or last left.</summary>
    public TilePos Tile { get; private set; }

    /// <summary>The way the character faces.</summary>
    public Facing Facing { get; private set; }

    /// <summary>Whether the character is between two tiles.</summary>
    public bool Moving { get; private set; }

    /// <summary>How many steps from tile to tile the character has begun.</summary>
    public int StepsBegun { get; private set; }

    /// <summary>A tile another character stands on: never stepped on.</summary>
    public TilePos? Obstacle { get; set; }

    /// <summary>The place of the character after the last step, in tiles.</summary>
    public double X { get; private set; }

    /// <summary>The place of the character after the last step, in tiles.</summary>
    public double Y { get; private set; }

    /// <summary>The place of the character before the last step, in tiles: rendering interpolates from it.</summary>
    public double PreviousX { get; private set; }

    /// <summary>The place of the character before the last step, in tiles.</summary>
    public double PreviousY { get; private set; }

    /// <summary>Advances the character by one step of the simulation.</summary>
    /// <param name="input">What the player asks.</param>
    /// <param name="seconds">The length of the step.</param>
    /// <returns>True when the character arrived on a tile in this step and stays there.</returns>
    public bool Step(in InputSnapshot input, double seconds)
    {
        PreviousX = X;
        PreviousY = Y;
        var hasDirection = Direction(input.Move.X) != 0 || Direction(input.Move.Y) != 0;
        if (input.Tap is { } tap)
        {
            pendingTap = tap;
        }

        if (hasDirection)
        {
            path.Clear();
            pendingTap = null;
        }

        if (!Moving)
        {
            TryStart(input);
            if (!Moving)
            {
                return false;
            }
        }

        progress += TilesPerSecond * seconds / length;
        if (progress < 1 - 1e-9)
        {
            X = Tile.X + ((target.X - Tile.X) * progress);
            Y = Tile.Y + ((target.Y - Tile.Y) * progress);
            return false;
        }

        Tile = target;
        X = Tile.X;
        Y = Tile.Y;
        Moving = false;
        progress = 0;
        TryStart(input);
        return !Moving;
    }

    private static int Direction(float axis) => axis > 0.5f ? 1 : axis < -0.5f ? -1 : 0;

    private void TryStart(in InputSnapshot input)
    {
        if (pendingTap is { } tap)
        {
            pendingTap = null;
            path.Clear();
            foreach (var tile in Pathfinder.FindPath(map, Tile, tap, Obstacle))
            {
                path.Enqueue(tile);
            }
        }

        var dx = Direction(input.Move.X);
        var dy = Direction(input.Move.Y);
        if (dx != 0 || dy != 0)
        {
            Facing = Facings.FromDelta(dx, dy) ?? Facing;
            if (Pathfinder.CanStep(map, Tile, dx, dy, Obstacle))
            {
                Start(dx, dy);
            }
            else if (dx != 0 && dy != 0 && Pathfinder.CanStep(map, Tile, dx, 0, Obstacle))
            {
                Start(dx, 0);
            }
            else if (dx != 0 && dy != 0 && Pathfinder.CanStep(map, Tile, 0, dy, Obstacle))
            {
                Start(0, dy);
            }

            return;
        }

        if (path.Count == 0)
        {
            return;
        }

        var next = path.Dequeue();
        var stepX = next.X - Tile.X;
        var stepY = next.Y - Tile.Y;
        if (Math.Abs(stepX) <= 1 && Math.Abs(stepY) <= 1 && Pathfinder.CanStep(map, Tile, stepX, stepY, Obstacle))
        {
            Start(stepX, stepY);
        }
        else
        {
            path.Clear();
        }
    }

    private void Start(int dx, int dy)
    {
        target = new TilePos(Tile.X + dx, Tile.Y + dy);
        Facing = Facings.FromDelta(dx, dy) ?? Facing;
        length = dx != 0 && dy != 0 ? Math.Sqrt(2) : 1;
        progress = 0;
        Moving = true;
        StepsBegun++;
    }
}
