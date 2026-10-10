using AdamEve.Core.Rigs;
using AdamEve.Core.World;

namespace AdamEve.Core.Game;

/// <summary>
/// The atlas: every image a render-list entry may name, by id. Each image is art made by code, which the browser
/// paints or builds once from what is listed here: a rig part is one shape about its centre; what stands on a tile
/// (<see cref="SceneryKind"/>) is a few shapes about its foot, and the renderer is told its kind.
/// </summary>
public sealed class AtlasCatalog
{
    private readonly List<IReadOnlyList<FlatShape>> images = [];
    private readonly int[] rigOffsets;
    private readonly int[] spriteIds = new int[Enum.GetValues<SceneryKind>().Length];
    private readonly List<SceneryKind> kinds = [];

    /// <summary>Lists the images of the rigs and of everything that stands on a tile.</summary>
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
                images.Add([new FlatShape(part.Shape, 0, 0, part.Width, part.Height, part.Colour, part.Round)]);
                kinds.Add(SceneryKind.None);
            }
        }

        for (var kind = 0; kind < spriteIds.Length; kind++)
        {
            var shapes = PlaceholderArt.SpriteOf((SceneryKind)kind);
            spriteIds[kind] = shapes.Count == 0 ? -1 : images.Count;
            if (shapes.Count > 0)
            {
                images.Add(shapes);
                kinds.Add((SceneryKind)kind);
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

    /// <summary>The id of the image of what stands on a tile; -1 for nothing.</summary>
    /// <param name="kind">The kind.</param>
    public int SpriteId(SceneryKind kind) => spriteIds[(int)kind];

    /// <summary>What an image is: the kind of scenery, or <see cref="SceneryKind.None"/> for a part of a rig.</summary>
    /// <param name="id">The id of the image.</param>
    public SceneryKind KindOf(int id) => kinds[id];

    /// <summary>For the renderer: the kind of each image by its id, as its number (<see cref="KindOf"/>).</summary>
    public int[] ToKindNumbers() => [.. kinds.Select(kind => (int)kind)];

    /// <summary>
    /// The atlas as numbers, for the renderer: the number of images, then for each image the number of its shapes
    /// and for each shape its outline (the number of its <see cref="PartShape"/>), x, y, width, height, colour and the
    /// radius of its corners.
    /// </summary>
    public double[] ToNumbers()
    {
        var numbers = new List<double> { images.Count };
        foreach (var image in images)
        {
            numbers.Add(image.Count);
            foreach (var shape in image)
            {
                numbers.AddRange([(int)shape.Shape, shape.X, shape.Y, shape.Width, shape.Height, shape.Colour, shape.Round]);
            }
        }

        return [.. numbers];
    }
}
