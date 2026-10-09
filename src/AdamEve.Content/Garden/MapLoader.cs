using System.Text.Json;
using AdamEve.Core.World;

namespace AdamEve.Content.Garden;

/// <summary>
/// Reads the map of the garden (<c>content/maps/garden.tmj</c>, Tiled JSON): the tile layer "ground", the object
/// layer "regions" (one rectangle each, named by its id) and the object layer "spawns" (one point each). The kind
/// of a tile is the <c>type</c> of its entry in the map's one tile set.
/// </summary>
public static class MapLoader
{
    private static readonly Dictionary<string, TileKind> Kinds = new(StringComparer.Ordinal)
    {
        ["grass"] = TileKind.Grass,
        ["water"] = TileKind.Water,
        ["thicket"] = TileKind.Thicket,
        ["crossing"] = TileKind.Crossing,
        ["tree"] = TileKind.Tree,
        ["tree-of-life"] = TileKind.TreeOfLife,
        ["tree-of-knowledge"] = TileKind.TreeOfKnowledge,
        ["fig-tree"] = TileKind.FigTree,
        ["resting-place"] = TileKind.RestingPlace,
        ["flowers"] = TileKind.Flowers,
    };

    /// <summary>Reads the map.</summary>
    /// <param name="utf8Json">The bytes of the file.</param>
    /// <exception cref="ContentFormatException">The file is not a map of the garden.</exception>
    public static TileMap Parse(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            var reader = new Utf8JsonReader(utf8Json);
            using var json = JsonDocument.ParseValue(ref reader);
            var root = json.RootElement;
            var width = root.GetProperty("width").GetInt32();
            var height = root.GetProperty("height").GetInt32();
            var tileSize = root.GetProperty("tilewidth").GetInt32();
            if (tileSize <= 0 || root.GetProperty("tileheight").GetInt32() != tileSize)
            {
                throw new ContentFormatException("The map's tiles are not square.");
            }

            var tileSet = root.GetProperty("tilesets").EnumerateArray().Single();
            var firstGid = tileSet.GetProperty("firstgid").GetInt32();
            var kindOfTile = new Dictionary<int, TileKind>();
            foreach (var tile in tileSet.GetProperty("tiles").EnumerateArray())
            {
                var type = tile.GetProperty("type").GetString() ?? string.Empty;
                kindOfTile[firstGid + tile.GetProperty("id").GetInt32()] = Kinds.TryGetValue(type, out var kind)
                    ? kind
                    : throw new ContentFormatException($"The map's tile set has the unknown type \"{type}\".");
            }

            TileKind[]? tiles = null;
            var regions = new List<Region>();
            var spawns = new Dictionary<string, TilePos>(StringComparer.Ordinal);
            foreach (var layer in root.GetProperty("layers").EnumerateArray())
            {
                var name = layer.GetProperty("name").GetString();
                if (name == "ground")
                {
                    tiles = [.. layer.GetProperty("data").EnumerateArray().Select(gid => kindOfTile.TryGetValue(gid.GetInt32(), out var kind)
                        ? kind
                        : throw new ContentFormatException($"The map's ground has the unknown tile {gid.GetInt32()}."))];
                }
                else if (name is "regions" or "spawns")
                {
                    foreach (var item in layer.GetProperty("objects").EnumerateArray())
                    {
                        var id = item.GetProperty("name").GetString();
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            throw new ContentFormatException($"An object of the map's layer \"{name}\" has no name.");
                        }

                        var x = (int)Math.Floor(item.GetProperty("x").GetDouble() / tileSize);
                        var y = (int)Math.Floor(item.GetProperty("y").GetDouble() / tileSize);
                        if (name == "spawns")
                        {
                            spawns.Add(id, new TilePos(x, y));
                        }
                        else
                        {
                            regions.Add(new Region(id, x, y, (int)Math.Round(item.GetProperty("width").GetDouble() / tileSize), (int)Math.Round(item.GetProperty("height").GetDouble() / tileSize)));
                        }
                    }
                }
            }

            if (tiles is null || regions.Count == 0)
            {
                throw new ContentFormatException("The map has no layer \"ground\" or no region.");
            }

            return new TileMap(width, height, tileSize, tiles, regions, spawns);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or FormatException)
        {
            throw new ContentFormatException($"The map cannot be read: {exception.Message}", exception);
        }
    }
}
