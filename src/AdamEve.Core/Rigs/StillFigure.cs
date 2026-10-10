using AdamEve.Core.World;

namespace AdamEve.Core.Rigs;

/// <summary>One flat shape of a composed figure, placed in the rig's space (the feet at the origin).</summary>
/// <param name="Shape">The outline.</param>
/// <param name="Transform">From the shape's own space (its centre at the origin) to the rig's space.</param>
/// <param name="Width">The width in logical pixels.</param>
/// <param name="Height">The height in logical pixels.</param>
/// <param name="Colour">The colour, 0xRRGGBB.</param>
/// <param name="Round">The radius of the corners of a rounded rectangle; 0 for the other outlines.</param>
public readonly record struct PlacedShape(PartShape Shape, Affine Transform, double Width, double Height, int Colour, double Round = 0);

/// <summary>
/// A character drawn outside the garden, standing still: at the character select, and far away in the sixth day of
/// creation. It is composed from the same rig as in the garden and judged by the same checks (rule M1 as decision
/// D18 amended it): the shapes to draw and the verdict are of one frame. A frame that does not pass is not drawn
/// at all (fail closed): it has no shapes, and its verdict names what failed.
/// </summary>
public sealed class StillFigure
{
    private StillFigure(IReadOnlyList<PlacedShape> shapes, string concealment)
    {
        Shapes = shapes;
        Concealment = concealment;
    }

    /// <summary>The shapes to draw, the farthest first; none for a frame that did not pass.</summary>
    public IReadOnlyList<PlacedShape> Shapes { get; }

    /// <summary>The M1 verdict of the frame: "ok", "fail:&lt;rig&gt;:&lt;zone&gt;" or "fail:&lt;rig&gt;:structure".</summary>
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
        if (RigStructure.Violations(rig).Count > 0)
        {
            return new StillFigure([], $"fail:{rig.Id}:{Structure}");
        }

        var pose = new RigPose(rig);
        pose.Sample(animation, 0, facing, covering);
        var exposed = new ConcealmentChecker().FirstExposedZone(pose, scale);
        if (exposed >= 0)
        {
            return new StillFigure([], $"fail:{rig.Id}:{rig.Zones[exposed].Id}");
        }

        var shapes = new List<PlacedShape>();
        foreach (var placed in pose.Parts)
        {
            var part = rig.Parts[placed.PartIndex];
            shapes.Add(new PlacedShape(part.Shape, placed.Transform, part.Width, part.Height, light ?? part.Colour, part.Round));
        }

        return new StillFigure(shapes, Ok);
    }

    /// <summary>What a verdict names in place of a zone when the rig itself holds what the rule does not allow.</summary>
    public const string Structure = "structure";

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
