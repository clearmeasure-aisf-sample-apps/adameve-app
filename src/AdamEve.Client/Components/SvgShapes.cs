using System.Globalization;
using AdamEve.Core.Rigs;
using AdamEve.Core.Story;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AdamEve.Client.Components;

/// <summary>
/// Art outside the canvas: what the game lists, written as the shapes of an <c>svg</c> element of the page: the
/// flat shapes of a figure as ellipses and rectangles (plain or with rounded corners), the shapes of a picture as
/// paths with their fills. Made by code from numbers; no image file, and no style attribute.
/// </summary>
internal static class SvgShapes
{
    /// <summary>Shapes placed about an anchor, in the order to draw them.</summary>
    public static RenderFragment Of(IEnumerable<FlatShape> shapes) => builder =>
    {
        foreach (var shape in shapes)
        {
            Add(builder, shape.Shape, Affine.Translation(shape.X, shape.Y), shape.Width, shape.Height, shape.Colour, shape.Round);
        }
    };

    /// <summary>The shapes of a composed figure, in the order to draw them.</summary>
    public static RenderFragment Of(IEnumerable<PlacedShape> shapes) => builder =>
    {
        foreach (var shape in shapes)
        {
            Add(builder, shape.Shape, shape.Transform, shape.Width, shape.Height, shape.Colour, shape.Round);
        }
    };

    /// <summary>
    /// The shapes of one layer of a picture, in the order to draw them: a path each, and for each fill that is a
    /// gradient its definition, named after the layer and the place of the shape in it.
    /// </summary>
    public static RenderFragment Of(string layer, IReadOnlyList<PictureShape> shapes) => builder =>
    {
        builder.OpenElement(20, "defs");
        for (var index = 0; index < shapes.Count; index++)
        {
            var paint = shapes[index].Paint;
            if (paint.Kind == PaintKind.Flat)
            {
                continue;
            }

            builder.OpenElement(21, paint.Kind == PaintKind.Linear ? "linearGradient" : "radialGradient");
            builder.SetKey(index);
            builder.AddAttribute(22, "id", $"paint-{layer}-{index}");
            builder.AddAttribute(23, "gradientUnits", "userSpaceOnUse");
            if (paint.Kind == PaintKind.Linear)
            {
                builder.AddAttribute(24, "x1", Number(paint.X1));
                builder.AddAttribute(25, "y1", Number(paint.Y1));
                builder.AddAttribute(26, "x2", Number(paint.X2));
                builder.AddAttribute(27, "y2", Number(paint.Y2));
            }
            else
            {
                builder.AddAttribute(28, "cx", Number(paint.X1));
                builder.AddAttribute(29, "cy", Number(paint.Y1));
                builder.AddAttribute(30, "r", Number(paint.X2));
                if (paint.Y2 > 0 && paint.X2 > 0)
                {
                    // A glow that is wider than high: the round one, pressed toward its centre line.
                    var press = paint.Y2 / paint.X2;
                    builder.AddAttribute(31, "gradientTransform", $"matrix(1 0 0 {Number(press)} 0 {Number(paint.Y1 * (1 - press))})");
                }
            }

            foreach (var stop in paint.Stops)
            {
                builder.OpenElement(32, "stop");
                builder.AddAttribute(33, "offset", Number(stop.At));
                builder.AddAttribute(34, "stop-color", Hex(stop.Colour));
                if (stop.Opacity < 1)
                {
                    builder.AddAttribute(35, "stop-opacity", Number(stop.Opacity));
                }

                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
        for (var index = 0; index < shapes.Count; index++)
        {
            var shape = shapes[index];
            builder.OpenElement(40, "path");
            builder.SetKey(index);
            builder.AddAttribute(41, "d", shape.Path);
            builder.AddAttribute(42, "fill", shape.Paint.Kind == PaintKind.Flat ? Hex(shape.Paint.Stops[0].Colour) : $"url(#paint-{layer}-{index})");
            if (shape.Opacity < 1)
            {
                builder.AddAttribute(43, "fill-opacity", Number(shape.Opacity));
            }

            builder.CloseElement();
        }
    };

    private static string Hex(int colour) => "#" + colour.ToString("X6", CultureInfo.InvariantCulture);

    private static void Add(RenderTreeBuilder builder, PartShape shape, Affine transform, double width, double height, int colour, double round)
    {
        if (shape is PartShape.Rectangle or PartShape.Rounded)
        {
            builder.OpenElement(0, "rect");
            builder.AddAttribute(1, "x", Number(-width / 2));
            builder.AddAttribute(2, "y", Number(-height / 2));
            builder.AddAttribute(3, "width", Number(width));
            builder.AddAttribute(4, "height", Number(height));
            if (shape == PartShape.Rounded)
            {
                builder.AddAttribute(10, "rx", Number(round));
            }
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
