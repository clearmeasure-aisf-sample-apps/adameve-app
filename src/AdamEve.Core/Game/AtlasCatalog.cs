using AdamEve.Core.Rigs;
using AdamEve.Core.World;

namespace AdamEve.Core.Game;

/// <summary>
/// The atlas: every image a render-list entry may name, by id. In this slice each image is placeholder art the
/// browser paints once from the shapes listed here: a rig part is one shape about its centre, a tree a few shapes
/// about the foot of its trunk.
/// </summary>
public sealed class AtlasCatalog
{
    private readonly List<IReadOnlyList<FlatShape>> images = [];
    private readonly int[] rigOffsets;
    private readonly int[] spriteIds = new int[TileKinds.Count];

    /// <summary>Lists the images of the rigs and of the trees.</summary>
    /// <param name="rigs">The rigs.</param>
    public AtlasCatalog(IReadOnlyList<Rig> rigs)
    {
        ArgumentNullException.ThrowIfNull(rigs);
        rigOffsets = new int[rigs.Count];
        for (var index = 0; index < rigs.Count; index++)
        {
            rigOffsets[index] = images.Count;
            foreach (var part in rigs[index].Parts)
            {
                images.Add([new FlatShape(part.Shape, 0, 0, part.Width, part.Height, part.Colour)]);
            }
        }

        for (var kind = 0; kind < TileKinds.Count; kind++)
        {
            var shapes = PlaceholderArt.SpriteOf((TileKind)kind);
            spriteIds[kind] = shapes.Count == 0 ? -1 : images.Count;
            if (shapes.Count > 0)
            {
                images.Add(shapes);
            }
        }
    }

    /// <summary>How many images the atlas has.</summary>
    public int Count => images.Count;

    /// <summary>The shapes of an image.</summary>
    /// <param name="id">The id of the image.</param>
    public IReadOnlyList<FlatShape> Shapes(int id) => images[id];

    /// <summary>The id of the image of a rig part.</summary>
    /// <param name="rigIndex">The index of the rig.</param>
    /// <param name="partIndex">The index of the part in its rig.</param>
    public int PartId(int rigIndex, int partIndex) => rigOffsets[rigIndex] + partIndex;

    /// <summary>The id of the image of a tree; -1 for a kind that is not a tree.</summary>
    /// <param name="kind">The tile kind.</param>
    public int SpriteId(TileKind kind) => spriteIds[(int)kind];

    /// <summary>
    /// The atlas as numbers, for the renderer: the number of images, then for each image the number of its shapes
    /// and for each shape its outline (0 ellipse, 1 rectangle), x, y, width, height and colour.
    /// </summary>
    public double[] ToNumbers()
    {
        var numbers = new List<double> { images.Count };
        foreach (var image in images)
        {
            numbers.Add(image.Count);
            foreach (var shape in image)
            {
                numbers.AddRange([(int)shape.Shape, shape.X, shape.Y, shape.Width, shape.Height, shape.Colour]);
            }
        }

        return [.. numbers];
    }
}
