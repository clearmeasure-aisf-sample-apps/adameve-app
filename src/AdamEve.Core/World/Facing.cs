namespace AdamEve.Core.World;

/// <summary>The eight ways a character faces in the 3/4 top-down world, clockwise from north.</summary>
public enum Facing
{
    /// <summary>North: away from the viewer.</summary>
    N,

    /// <summary>North-east: away from the viewer.</summary>
    NE,

    /// <summary>East.</summary>
    E,

    /// <summary>South-east.</summary>
    SE,

    /// <summary>South: toward the viewer.</summary>
    S,

    /// <summary>South-west.</summary>
    SW,

    /// <summary>West.</summary>
    W,

    /// <summary>North-west: away from the viewer.</summary>
    NW,
}

/// <summary>What a facing means for movement and for the modesty rule M1.</summary>
public static class Facings
{
    private static readonly int[] StepX = [0, 1, 1, 1, 0, -1, -1, -1];
    private static readonly int[] StepY = [-1, -1, 0, 1, 1, 1, 0, -1];

    /// <summary>All eight facings, clockwise from north.</summary>
    public static IReadOnlyList<Facing> All { get; } = [Facing.N, Facing.NE, Facing.E, Facing.SE, Facing.S, Facing.SW, Facing.W, Facing.NW];

    /// <summary>
    /// True for the three back facings (N, NE, NW): the only ones that count as "turned away from the viewer"
    /// (design, section 1). Every other facing requires an occluder.
    /// </summary>
    /// <param name="facing">The facing.</param>
    public static bool IsTurnedAway(this Facing facing) => facing is Facing.N or Facing.NE or Facing.NW;

    /// <summary>The column step of one tile in this facing: -1, 0 or 1.</summary>
    /// <param name="facing">The facing.</param>
    public static int DeltaX(this Facing facing) => StepX[(int)facing];

    /// <summary>The row step of one tile in this facing: -1, 0 or 1.</summary>
    /// <param name="facing">The facing.</param>
    public static int DeltaY(this Facing facing) => StepY[(int)facing];

    /// <summary>The facing of a step; null for no step.</summary>
    /// <param name="dx">The column step: its sign counts.</param>
    /// <param name="dy">The row step: its sign counts.</param>
    public static Facing? FromDelta(int dx, int dy)
    {
        dx = Math.Sign(dx);
        dy = Math.Sign(dy);
        for (var index = 0; index < StepX.Length; index++)
        {
            if (StepX[index] == dx && StepY[index] == dy)
            {
                return (Facing)index;
            }
        }

        return null;
    }
}
