using AdamEve.Core.World;

namespace AdamEve.Core.Rigs;

/// <summary>One flat shape of a composed figure, placed in the rig's space (the feet at the origin).</summary>
/// <param name="Shape">The outline.</param>
/// <param name="Transform">From the shape's own space (its centre at the origin) to the rig's space.</param>
/// <param name="Width">The width in logical pixels.</param>
/// <param name="Height">The height in logical pixels.</param>
/// <param name="Colour">The colour, 0xRRGGBB.</param>
public readonly record struct PlacedShape(PartShape Shape, Affine Transform, double Width, double Height, int Colour);

/// <summary>
/// A character drawn outside the garden, standing still: at the character select, and far away in the sixth day of
/// creation. It is composed from the same rig as in the garden and judged by the same check (rule M1): the shapes
/// to draw and the verdict are of one frame. A frame that is not concealed gets the default foliage cluster in
/// front of the figure (fail closed) and a verdict that names the zone.
/// </summary>
public sealed class StillFigure
{
    private StillFigure(IReadOnlyList<PlacedShape> shapes, string concealment)
    {
        Shapes = shapes;
        Concealment = concealment;
    }

    /// <summary>The shapes to draw, in order: the body, then the occluder layer, then the default cluster of a frame that failed.</summary>
    public IReadOnlyList<PlacedShape> Shapes { get; }

    /// <summary>The M1 verdict of the frame: "ok" or "fail:&lt;rig&gt;:&lt;zone&gt;".</summary>
    public string Concealment { get; }

    /// <summary>Composes a standing figure and judges it.</summary>
    /// <param name="rig">The rig.</param>
    /// <param name="animation">The animation whose first frame is the stance.</param>
    /// <param name="facing">The facing.</param>
    /// <param name="covering">The covering variant.</param>
    /// <param name="scale">Pixels for one logical pixel, as the figure is drawn.</param>
    /// <param name="light">One colour for every shape, for a figure of light; null for the colours of the rig.</param>
    public static StillFigure Compose(Rig rig, RigAnimation animation, Facing facing, Covering covering, double scale, int? light = null)
    {
        ArgumentNullException.ThrowIfNull(rig);
        var pose = new RigPose(rig);
        pose.Sample(animation, 0, facing, covering);
        var exposed = new ConcealmentChecker().FirstExposedZone(pose, scale);

        var shapes = new List<PlacedShape>();
        foreach (var occluders in new[] { false, true })
        {
            foreach (var placed in pose.Parts)
            {
                var part = rig.Parts[placed.PartIndex];
                if ((part.Role == PartRole.Occluder) == occluders)
                {
                    shapes.Add(new PlacedShape(part.Shape, placed.Transform, part.Width, part.Height, light ?? part.Colour));
                }
            }
        }

        if (exposed >= 0)
        {
            foreach (var cluster in DefaultFoliage.Shapes)
            {
                shapes.Add(new PlacedShape(cluster.Shape, Affine.Translation(cluster.X, cluster.Y), cluster.Width, cluster.Height, cluster.Colour));
            }
        }

        return new StillFigure(shapes, exposed < 0 ? Ok : $"fail:{rig.Id}:{rig.Zones[exposed].Id}");
    }

    /// <summary>The verdict of a frame in which every zone is concealed, or in which no character is drawn.</summary>
    public const string Ok = "ok";

    /// <summary>The verdict of several figures drawn together: the first that is not "ok", or "ok".</summary>
    /// <param name="figures">The figures.</param>
    public static string VerdictOf(IEnumerable<StillFigure> figures)
    {
        ArgumentNullException.ThrowIfNull(figures);
        return figures.Select(figure => figure.Concealment).FirstOrDefault(verdict => verdict != Ok) ?? Ok;
    }
}
