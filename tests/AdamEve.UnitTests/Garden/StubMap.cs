using AdamEve.Content.Garden;
using AdamEve.Core.Rigs;
using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

/// <summary>Small maps drawn as text, and the shipped garden, for the tests of the world rules.</summary>
internal static class StubMap
{
    /// <summary>A map from rows of text: '.' grass, '~' water, '#' thicket, '=' crossing, 'T' tree.</summary>
    public static TileMap FromRows(params string[] rows)
    {
        var tiles = rows.SelectMany(row => row.Select(letter => letter switch
        {
            '.' => TileKind.Grass,
            '~' => TileKind.Water,
            '#' => TileKind.Thicket,
            '=' => TileKind.Crossing,
            'T' => TileKind.Tree,
            _ => throw new ArgumentException($"'{letter}' is not a tile of a stub map."),
        })).ToList();
        return new TileMap(rows[0].Length, rows.Length, 32, tiles, [new Region("stub", 0, 0, rows[0].Length, rows.Length)], new Dictionary<string, TilePos>());
    }

    public static GardenContent Shipped() => GardenContent.LoadEmbedded();

    /// <summary>A copy of a rig with other parts, zones or concealment records.</summary>
    public static Rig With(Rig rig, IEnumerable<RigPart>? parts = null, IEnumerable<ConcealmentZone>? zones = null, IEnumerable<ConcealmentRecord>? concealment = null) =>
        new(rig.Id, rig.Bones, [.. parts ?? rig.Parts], [.. zones ?? rig.Zones], [.. concealment ?? rig.Concealment]);
}
