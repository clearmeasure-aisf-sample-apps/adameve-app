namespace AdamEve.Core.Rigs;

/// <summary>
/// A 2D transform as the canvas takes it: x' = A·x + C·y + E, y' = B·x + D·y + F.
/// </summary>
/// <param name="A">The x scale.</param>
/// <param name="B">The y skew.</param>
/// <param name="C">The x skew.</param>
/// <param name="D">The y scale.</param>
/// <param name="E">The x translation.</param>
/// <param name="F">The y translation.</param>
public readonly record struct Affine(double A, double B, double C, double D, double E, double F)
{
    /// <summary>The transform that changes nothing.</summary>
    public static Affine Identity { get; } = new(1, 0, 0, 1, 0, 0);

    /// <summary>A move.</summary>
    /// <param name="x">The move along x.</param>
    /// <param name="y">The move along y.</param>
    public static Affine Translation(double x, double y) => new(1, 0, 0, 1, x, y);

    /// <summary>A turn about the origin.</summary>
    /// <param name="degrees">The angle in degrees, clockwise on the screen.</param>
    public static Affine Rotation(double degrees)
    {
        if (degrees == 0)
        {
            return Identity;
        }

        var radians = degrees * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new Affine(cos, sin, -sin, cos, 0, 0);
    }

    /// <summary>A mirror about the vertical axis through the origin.</summary>
    public static Affine MirrorX { get; } = new(-1, 0, 0, 1, 0, 0);

    /// <summary>This transform applied after <paramref name="inner"/>.</summary>
    /// <param name="inner">The transform applied first.</param>
    public Affine Then(in Affine inner) => new(
        (A * inner.A) + (C * inner.B),
        (B * inner.A) + (D * inner.B),
        (A * inner.C) + (C * inner.D),
        (B * inner.C) + (D * inner.D),
        (A * inner.E) + (C * inner.F) + E,
        (B * inner.E) + (D * inner.F) + F);

    /// <summary>The x of a transformed point.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    public double ApplyX(double x, double y) => (A * x) + (C * y) + E;

    /// <summary>The y of a transformed point.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    public double ApplyY(double x, double y) => (B * x) + (D * y) + F;

    /// <summary>The transform that undoes this one.</summary>
    /// <exception cref="InvalidOperationException">The transform flattens the plane.</exception>
    public Affine Invert()
    {
        var determinant = (A * D) - (B * C);
        if (Math.Abs(determinant) < 1e-12)
        {
            throw new InvalidOperationException("The transform cannot be undone.");
        }

        var a = D / determinant;
        var b = -B / determinant;
        var c = -C / determinant;
        var d = A / determinant;
        return new Affine(a, b, c, d, -((a * E) + (c * F)), -((b * E) + (d * F)));
    }
}
