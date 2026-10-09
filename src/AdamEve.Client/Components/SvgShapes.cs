using System.Globalization;
using AdamEve.Core.Rigs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AdamEve.Client.Components;

/// <summary>
/// Placeholder art outside the canvas: the flat shapes the game lists, written as the ellipses and rectangles of an
/// <c>svg</c> element of the page. Made by code from numbers; no image file, and no style attribute.
/// </summary>
internal static class SvgShapes
{
    /// <summary>Shapes placed about an anchor, in the order to draw them.</summary>
    public static RenderFragment Of(IEnumerable<FlatShape> shapes) => builder =>
    {
        foreach (var shape in shapes)
        {
            Add(builder, shape.Shape, Affine.Translation(shape.X, shape.Y), shape.Width, shape.Height, shape.Colour);
        }
    };

    /// <summary>The shapes of a composed figure, in the order to draw them.</summary>
    public static RenderFragment Of(IEnumerable<PlacedShape> shapes) => builder =>
    {
        foreach (var shape in shapes)
        {
            Add(builder, shape.Shape, shape.Transform, shape.Width, shape.Height, shape.Colour);
        }
    };

    private static void Add(RenderTreeBuilder builder, PartShape shape, Affine transform, double width, double height, int colour)
    {
        if (shape == PartShape.Rectangle)
        {
            builder.OpenElement(0, "rect");
            builder.AddAttribute(1, "x", Number(-width / 2));
            builder.AddAttribute(2, "y", Number(-height / 2));
            builder.AddAttribute(3, "width", Number(width));
            builder.AddAttribute(4, "height", Number(height));
        }
        else
        {
            builder.OpenElement(5, "ellipse");
            builder.AddAttribute(6, "rx", Number(width / 2));
            builder.AddAttribute(7, "ry", Number(height / 2));
        }

        builder.AddAttribute(8, "fill", "#" + colour.ToString("X6", CultureInfo.InvariantCulture));
        builder.AddAttribute(9, "transform", $"matrix({Number(transform.A)} {Number(transform.B)} {Number(transform.C)} {Number(transform.D)} {Number(transform.E)} {Number(transform.F)})");
        builder.CloseElement();
    }

    private static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
