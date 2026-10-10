using System.Globalization;
using System.Text;

namespace AdamEve.Core.Story;

/// <summary>How a shape of a picture is filled.</summary>
public enum PaintKind
{
    /// <summary>One colour.</summary>
    Flat,

    /// <summary>Colours along a line from one point to another.</summary>
    Linear,

    /// <summary>Colours outward from a centre to a radius.</summary>
    Radial,
}

/// <summary>A colour at a place along a gradient.</summary>
/// <param name="At">The place, from 0 to 1.</param>
/// <param name="Colour">The colour, 0xRRGGBB.</param>
/// <param name="Opacity">How much of it is seen, from 0 to 1.</param>
public readonly record struct PaintStop(double At, int Colour, double Opacity = 1);

/// <summary>
/// The fill of a shape: one colour, or a gradient made of numbers. Nothing here is an image.
/// </summary>
/// <param name="Kind">The kind of fill.</param>
/// <param name="Stops">The colours; one for a flat fill.</param>
/// <param name="X1">Linear: where the line begins. Radial: the centre.</param>
/// <param name="Y1">Linear: where the line begins. Radial: the centre.</param>
/// <param name="X2">Linear: where the line ends. Radial: the radius across.</param>
/// <param name="Y2">Linear: where the line ends. Radial: the radius up and down; 0 for a round glow.</param>
public sealed record Paint(PaintKind Kind, IReadOnlyList<PaintStop> Stops, double X1 = 0, double Y1 = 0, double X2 = 0, double Y2 = 0)
{
    /// <summary>One colour.</summary>
    /// <param name="colour">The colour, 0xRRGGBB.</param>
    public static Paint Flat(int colour) => new(PaintKind.Flat, [new PaintStop(0, colour)]);

    /// <summary>Colours from one height of the picture down to another.</summary>
    /// <param name="from">The upper height.</param>
    /// <param name="to">The lower height.</param>
    /// <param name="stops">The colours.</param>
    public static Paint Down(double from, double to, params PaintStop[] stops) => new(PaintKind.Linear, stops, 0, from, 0, to);

    /// <summary>Colours from one place across the picture to another.</summary>
    /// <param name="from">The left place.</param>
    /// <param name="to">The right place.</param>
    /// <param name="stops">The colours.</param>
    public static Paint Across(double from, double to, params PaintStop[] stops) => new(PaintKind.Linear, stops, from, 0, to, 0);

    /// <summary>Colours outward from a centre.</summary>
    /// <param name="x">The centre.</param>
    /// <param name="y">The centre.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="stops">The colours.</param>
    public static Paint Glow(double x, double y, double radius, params PaintStop[] stops) => new(PaintKind.Radial, stops, x, y, radius);

    /// <summary>Colours outward from a centre, wider than high or higher than wide.</summary>
    /// <param name="x">The centre.</param>
    /// <param name="y">The centre.</param>
    /// <param name="radiusX">The radius across.</param>
    /// <param name="radiusY">The radius up and down.</param>
    /// <param name="stops">The colours.</param>
    public static Paint Oval(double x, double y, double radiusX, double radiusY, params PaintStop[] stops) => new(PaintKind.Radial, stops, x, y, radiusX, radiusY);
}

/// <summary>
/// One shape of a picture made by code: an outline (the path data of an <c>svg</c> path: lines and curves through
/// points that are numbers), its fill, and the box it lies in, in the units of the picture.
/// </summary>
/// <param name="Path">The outline.</param>
/// <param name="Paint">The fill.</param>
/// <param name="Left">The left edge of its box.</param>
/// <param name="Top">The top edge of its box.</param>
/// <param name="Right">The right edge of its box.</param>
/// <param name="Bottom">The bottom edge of its box.</param>
/// <param name="Opacity">How much of the shape is seen, from 0 to 1.</param>
public sealed record PictureShape(string Path, Paint Paint, double Left, double Top, double Right, double Bottom, double Opacity = 1)
{
    /// <summary>The middle of its box.</summary>
    public (double X, double Y) Centre => ((Left + Right) / 2, (Top + Bottom) / 2);
}

/// <summary>
/// Draws the shapes of a picture: ellipses, polygons and smooth closed curves, each from numbers. It keeps the box
/// of every shape.
/// </summary>
internal sealed class PictureBrush
{
    private readonly List<PictureShape> shapes = [];

    /// <summary>What was drawn, the farthest first.</summary>
    public IReadOnlyList<PictureShape> Shapes => shapes;

    /// <summary>An ellipse.</summary>
    public void Ellipse(double x, double y, double radiusX, double radiusY, Paint paint, double opacity = 1) => shapes.Add(new PictureShape(
        $"M{N(x - radiusX)} {N(y)}a{N(radiusX)} {N(radiusY)} 0 1 0 {N(radiusX * 2)} 0a{N(radiusX)} {N(radiusY)} 0 1 0 {N(-radiusX * 2)} 0Z",
        paint, x - radiusX, y - radiusY, x + radiusX, y + radiusY, opacity));

    /// <summary>A polygon through points (x, y, x, y, and so on).</summary>
    public void Polygon(Paint paint, double opacity, params double[] points)
    {
        var path = new StringBuilder();
        for (var index = 0; index < points.Length; index += 2)
        {
            path.Append(index == 0 ? 'M' : 'L').Append(N(points[index])).Append(' ').Append(N(points[index + 1]));
        }

        Add(path.Append('Z').ToString(), paint, opacity, points);
    }

    /// <summary>A closed curve that passes smoothly through points (x, y, x, y, and so on).</summary>
    public void Curve(Paint paint, double opacity, params double[] points)
    {
        var count = points.Length / 2;
        (double X, double Y) At(int index) => (points[(((index % count) + count) % count) * 2], points[((((index % count) + count) % count) * 2) + 1]);
        var path = new StringBuilder().Append('M').Append(N(points[0])).Append(' ').Append(N(points[1]));
        for (var index = 0; index < count; index++)
        {
            // From this point to the next, bent by their neighbours on either side.
            var (before, from, to, after) = (At(index - 1), At(index), At(index + 1), At(index + 2));
            path.Append('C').Append(N(from.X + ((to.X - before.X) / 6))).Append(' ').Append(N(from.Y + ((to.Y - before.Y) / 6)))
                .Append(' ').Append(N(to.X - ((after.X - from.X) / 6))).Append(' ').Append(N(to.Y - ((after.Y - from.Y) / 6)))
                .Append(' ').Append(N(to.X)).Append(' ').Append(N(to.Y));
        }

        Add(path.Append('Z').ToString(), paint, opacity, points);
    }

    private void Add(string path, Paint paint, double opacity, double[] points)
    {
        double left = double.PositiveInfinity, top = double.PositiveInfinity, right = double.NegativeInfinity, bottom = double.NegativeInfinity;
        for (var index = 0; index < points.Length; index += 2)
        {
            left = Math.Min(left, points[index]);
            right = Math.Max(right, points[index]);
            top = Math.Min(top, points[index + 1]);
            bottom = Math.Max(bottom, points[index + 1]);
        }

        shapes.Add(new PictureShape(path, paint, left, top, right, bottom, opacity));
    }

    private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
