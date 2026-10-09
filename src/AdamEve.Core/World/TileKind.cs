namespace AdamEve.Core.World;

/// <summary>What stands on a tile of the garden. The order is the order of the map's tile set.</summary>
public enum TileKind
{
    /// <summary>Grass: walkable.</summary>
    Grass,

    /// <summary>The river: not walkable.</summary>
    Water,

    /// <summary>Dense growth at the edge of the regions that are open: not walkable.</summary>
    Thicket,

    /// <summary>Flat stones across the river: walkable.</summary>
    Crossing,

    /// <summary>A tree of the garden: not walkable.</summary>
    Tree,

    /// <summary>The tree of life, in the midst of the garden (Genesis 2:9): not walkable, not interactive.</summary>
    TreeOfLife,

    /// <summary>The tree of the knowledge of good and evil, in the midst of the garden: not walkable.</summary>
    TreeOfKnowledge,

    /// <summary>The fig tree near the two trees: not walkable.</summary>
    FigTree,

    /// <summary>The resting place: walkable.</summary>
    RestingPlace,

    /// <summary>Grass with flowers: walkable.</summary>
    Flowers,
}

/// <summary>The rules of a tile kind.</summary>
public static class TileKinds
{
    /// <summary>How many kinds there are.</summary>
    public const int Count = 10;

    /// <summary>Whether a character may stand on a tile of this kind.</summary>
    /// <param name="kind">The kind.</param>
    public static bool IsWalkable(this TileKind kind) =>
        kind is TileKind.Grass or TileKind.Crossing or TileKind.RestingPlace or TileKind.Flowers;

    /// <summary>Whether a tile of this kind is drawn as a tall sprite, sorted by depth with the characters.</summary>
    /// <param name="kind">The kind.</param>
    public static bool IsSprite(this TileKind kind) =>
        kind is TileKind.Tree or TileKind.TreeOfLife or TileKind.TreeOfKnowledge or TileKind.FigTree;
}
