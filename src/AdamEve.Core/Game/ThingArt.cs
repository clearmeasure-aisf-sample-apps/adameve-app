using AdamEve.Core.Story;

namespace AdamEve.Core.Game;

/// <summary>The outline of one shape of a thing of the garden.</summary>
public enum ThingShapeKind
{
    /// <summary>An ellipse.</summary>
    Ellipse,

    /// <summary>A rectangle, its corners rounded by one radius (0 for none).</summary>
    Box,

    /// <summary>A triangle with two equal sides: its point up, its foot the width of its box.</summary>
    Triangle,
}

/// <summary>One flat shape of a thing of the garden, placed about the foot of the thing.</summary>
/// <param name="Kind">The outline.</param>
/// <param name="X">The centre, from the foot; x grows toward the way the thing looks.</param>
/// <param name="Y">The centre, from the foot; y grows downward, so what stands has negative y.</param>
/// <param name="Width">The width in logical pixels.</param>
/// <param name="Height">The height in logical pixels.</param>
/// <param name="Turn">How far the shape is turned about its centre, in degrees, clockwise as seen.</param>
/// <param name="Colour">The colour, 0xRRGGBB. A shape is opaque.</param>
/// <param name="Round">The radius of the corners of a box.</param>
public readonly record struct ThingShape(ThingShapeKind Kind, double X, double Y, double Width, double Height, double Turn, int Colour, double Round = 0);

/// <summary>
/// The pictures of the things of the garden that are not scenery and not a person: the animals brought to Adam
/// (<see cref="AnimalRoster"/>), the sapling and the fallen branch of beat B10. Art made by code (design, section
/// 5.2: "rounded, friendly, gently comic"): each is a handful of flat shapes about its foot, seen from the side.
/// Nothing here is an image file, nothing is copied from anywhere, and there is no fish and no serpent.
/// </summary>
public static class ThingArt
{
    /// <summary>The picture of the sapling before it is watered. The pictures of the animals come first, by their place in the roster.</summary>
    public const int Sapling = AnimalRoster.Count;

    /// <summary>The picture of the sapling after it is watered.</summary>
    public const int SaplingWatered = AnimalRoster.Count + 1;

    /// <summary>The picture of the fallen branch.</summary>
    public const int Branch = AnimalRoster.Count + 2;

    private const int Eye = 0x2B1D16;
    private const int Cream = 0xF4F0E6;
    private const int Gold = 0xE8B33A;

    private static readonly Dictionary<string, Action<Brush>> Animals = new()
    {
        ["elephant"] = Elephant, ["sheep"] = Sheep, ["dove"] = Dove, ["lion"] = Lion, ["pig"] = Pig, ["rabbit"] = Rabbit,
        ["eagle"] = Eagle, ["horse"] = Horse, ["deer"] = Deer, ["rooster"] = Rooster, ["bear"] = Bear, ["goat"] = Goat,
        ["sparrow"] = Sparrow, ["camel"] = Camel, ["squirrel"] = Squirrel, ["duck"] = Duck, ["cow"] = Cow, ["wolf"] = Wolf,
        ["peacock"] = Peacock, ["donkey"] = Donkey, ["hedgehog"] = Hedgehog, ["raven"] = Raven, ["badger"] = Badger, ["heron"] = Heron,
    };

    /// <summary>Every picture: the animals in the order of the roster, then the sapling (dry, watered), then the branch.</summary>
    public static IReadOnlyList<IReadOnlyList<ThingShape>> Images { get; } = Build();

    /// <summary>The picture of an animal.</summary>
    /// <param name="animal">The id of the animal.</param>
    /// <exception cref="ArgumentException">The id is not an animal of the roster.</exception>
    public static IReadOnlyList<ThingShape> OfAnimal(string animal)
    {
        var index = AnimalRoster.IndexOf(animal);
        return index >= 0 ? Images[index] : throw new ArgumentException($"\"{animal}\" is not an animal of the roster.", nameof(animal));
    }

    /// <summary>The box a picture lies in, about its foot: left, top, right, bottom.</summary>
    /// <param name="shapes">The shapes of the picture.</param>
    public static (double Left, double Top, double Right, double Bottom) Bounds(IReadOnlyList<ThingShape> shapes)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        double left = 0, top = 0, right = 0, bottom = 0;
        foreach (var shape in shapes)
        {
            // A turned shape lies within the circle about its centre through its corners.
            var reach = Math.Sqrt((shape.Width * shape.Width) + (shape.Height * shape.Height)) / 2;
            var (halfWidth, halfHeight) = shape.Turn == 0 ? (shape.Width / 2, shape.Height / 2) : (reach, reach);
            left = Math.Min(left, shape.X - halfWidth);
            right = Math.Max(right, shape.X + halfWidth);
            top = Math.Min(top, shape.Y - halfHeight);
            bottom = Math.Max(bottom, shape.Y + halfHeight);
        }

        return (left, top, right, bottom);
    }

    /// <summary>
    /// The pictures as numbers, for the renderer: how many pictures, then for each the number of its shapes and for
    /// each shape its outline (the number of its <see cref="ThingShapeKind"/>), x, y, width, height, turn in
    /// degrees, colour and the radius of its corners.
    /// </summary>
    public static double[] ToNumbers()
    {
        var numbers = new List<double> { Images.Count };
        foreach (var image in Images)
        {
            numbers.Add(image.Count);
            foreach (var shape in image)
            {
                numbers.AddRange([(int)shape.Kind, shape.X, shape.Y, shape.Width, shape.Height, shape.Turn, shape.Colour, shape.Round]);
            }
        }

        return [.. numbers];
    }

    private static IReadOnlyList<ThingShape>[] Build()
    {
        var images = new List<IReadOnlyList<ThingShape>>();
        foreach (var animal in AnimalRoster.Animals)
        {
            images.Add(Draw(Animals[animal.Id]));
        }

        images.Add(Draw(brush => SaplingOf(brush, watered: false)));
        images.Add(Draw(brush => SaplingOf(brush, watered: true)));
        images.Add(Draw(BranchOf));
        return [.. images];
    }

    private static IReadOnlyList<ThingShape> Draw(Action<Brush> draw)
    {
        var brush = new Brush();
        draw(brush);
        return brush.Shapes;
    }

    // A beast on four legs, seen from the side, looking toward +x: the far legs, the body, the near legs. The head
    // and everything that tells one kind from another is drawn by the animal itself. Returns the height of the back.
    private static double FourLegs(Brush brush, double length, double tall, double leg, double legWidth, int coat, int shade)
    {
        var middle = -(leg + (tall * 0.42));
        var reach = (length * 0.5) - (legWidth * 0.9);
        brush.Box(-reach + (legWidth * 0.55), -leg / 2, legWidth, leg + 3, shade, legWidth / 2);
        brush.Box(reach - (legWidth * 0.95), -leg / 2, legWidth, leg + 3, shade, legWidth / 2);
        brush.Ellipse(0, middle, length, tall, coat);
        brush.Box(-reach - (legWidth * 0.2), -leg / 2, legWidth, leg + 3, coat, legWidth / 2);
        brush.Box(reach, -leg / 2, legWidth, leg + 3, coat, legWidth / 2);
        return middle;
    }

    // A bird, seen from the side, looking toward +x: the legs, the tail, the body, the wing, the head, the beak and
    // the eye. Returns where the middle of the head lies.
    private static (double X, double Y) Bird(Brush brush, double length, double tall, double leg, int coat, int wing, int head, double headSize, double neck, double beak, int beakColour, int legColour, int tail)
    {
        var middle = -(leg + (tall * 0.45));
        brush.Box(-1.5, -leg / 2, 1.4, leg + 2, legColour, 0.6);
        brush.Box(2, -leg / 2, 1.4, leg + 2, legColour, 0.6);
        brush.Triangle(-length * 0.56, middle + (tall * 0.08), tall * 0.6, length * 0.5, tail, -104);
        brush.Ellipse(0, middle, length, tall, coat, -12);
        brush.Ellipse(-length * 0.1, middle + (tall * 0.02), length * 0.62, tall * 0.56, wing, 10);
        var headX = (length * 0.36) + (neck * 0.25);
        var headY = middle - (tall * 0.42) - neck;
        if (neck > 3)
        {
            brush.Box((length * 0.3) + (neck * 0.12), middle - (tall * 0.2) - (neck / 2), headSize * 0.5, neck + (tall * 0.4), coat, headSize * 0.25, 14);
        }

        brush.Ellipse(headX, headY, headSize, headSize, head);
        brush.Triangle(headX + (headSize * 0.42) + (beak * 0.42), headY + (headSize * 0.06), headSize * 0.42, beak, beakColour, 90);
        brush.Ellipse(headX + (headSize * 0.14), headY - (headSize * 0.1), Math.Max(1.3, headSize * 0.18), Math.Max(1.3, headSize * 0.2), Eye);
        return (headX, headY);
    }

    private static void Face(Brush brush, double x, double y, double size = 2.2) => brush.Ellipse(x, y, size, size * 1.15, Eye);

    private static void Elephant(Brush brush)
    {
        const int Coat = 0x9AA3A8, Shade = 0x7F898F;
        brush.Box(-23, -28, 2.4, 13, Shade, 1.2, 12);
        var back = FourLegs(brush, 46, 30, 15, 9, Coat, Shade);
        brush.Ellipse(24, back - 7, 23, 22, Coat);
        brush.Box(35, back + 8, 6.5, 26, Coat, 3.2, -6);
        brush.Ellipse(38.5, back + 20, 7, 5, Coat);
        brush.Triangle(31, back + 4, 4, 11, Cream, 152);
        brush.Ellipse(17, back - 6, 15, 21, Shade);
        Face(brush, 29, back - 10);
    }

    private static void Sheep(Brush brush)
    {
        const int Wool = 0xF3EEE0, Shade = 0xDDD5C2, Dark = 0x4A3B32;
        brush.Box(-8, -5, 3, 12, Dark, 1.5);
        brush.Box(7, -5, 3, 12, Dark, 1.5);
        brush.Ellipse(0, -17, 28, 19, Wool);
        foreach (var (x, y) in new[] { (-10.0, -24.0), (-3, -27), (5, -26), (-13, -17), (11, -21) })
        {
            brush.Ellipse(x, y, 10, 9, Wool);
        }

        brush.Ellipse(-4, -12, 12, 6, Shade);
        brush.Box(-5, -5, 3, 12, Dark, 1.5);
        brush.Box(10, -5, 3, 12, Dark, 1.5);
        brush.Ellipse(17, -21, 9.5, 11.5, Dark);
        brush.Ellipse(12, -24, 6, 3, Dark, 25);
        brush.Ellipse(15, -27.5, 10, 6.5, Wool);
        brush.Ellipse(19, -22, 2, 2.4, Cream);
    }

    private static void Dove(Brush brush)
    {
        Bird(brush, 17, 10.5, 4, 0xECEAE4, 0xCFCBC2, 0xF6F4EE, 7, 1, 3.4, 0xE2A04A, 0xD9826A, 0xCFCBC2);
    }

    private static void Lion(Brush brush)
    {
        const int Coat = 0xD9A441, Shade = 0xB9832E, Mane = 0x8A5A2B;
        brush.Box(-20, -19, 2.4, 15, Coat, 1.2, -28);
        brush.Ellipse(-24, -12, 5.5, 6.5, Mane);
        var back = FourLegs(brush, 36, 18, 12, 6, Coat, Shade);
        brush.Ellipse(17, back - 6, 23, 25, Mane);
        brush.Ellipse(14.5, back - 16, 5, 5, Coat);
        brush.Ellipse(24, back - 16.5, 5, 5, Coat);
        brush.Ellipse(20, back - 6, 15, 15, Coat);
        brush.Ellipse(25, back - 3, 8, 6.5, 0xEBC878);
        brush.Ellipse(28, back - 4.5, 3, 2.2, Eye);
        Face(brush, 21.5, back - 8.5, 2);
    }

    private static void Pig(Brush brush)
    {
        const int Coat = 0xF2B5B0, Shade = 0xDB958F;
        brush.Ellipse(-15.5, -17, 6, 6, Shade);
        brush.Ellipse(-15.5, -17, 2.4, 2.4, Coat);
        var back = FourLegs(brush, 29, 19, 6, 5, Coat, Shade);
        brush.Ellipse(15, back - 2, 16, 15, Coat);
        brush.Triangle(11, back - 10, 6, 7.5, Shade, 24);
        brush.Ellipse(22.5, back - 0.5, 6, 8.5, 0xE89A94);
        brush.Ellipse(23, back - 2, 1.2, 1.6, Shade);
        brush.Ellipse(23, back + 1.5, 1.2, 1.6, Shade);
        Face(brush, 16.5, back - 4.5, 2);
    }

    private static void Rabbit(Brush brush)
    {
        const int Coat = 0xC9B299, Shade = 0xAE967C;
        brush.Ellipse(-8.5, -8, 5.5, 5.5, Cream);
        brush.Box(5, -22, 3, 11, Shade, 1.5, -10);
        brush.Ellipse(0, -7.5, 15, 12, Coat);
        brush.Ellipse(-3, -5.5, 9, 9, Shade);
        brush.Ellipse(-3, -1.3, 10, 3, Coat);
        brush.Ellipse(5.5, -1.2, 5.5, 2.6, Coat);
        brush.Ellipse(7, -13.5, 9.5, 9, Coat);
        brush.Box(8.5, -22.5, 3.2, 11.5, Coat, 1.6, 9);
        brush.Ellipse(11, -12.5, 1.6, 1.4, 0xD98C8C);
        Face(brush, 8.5, -14.5, 1.8);
    }

    private static void Eagle(Brush brush)
    {
        const int Coat = 0x5B4030;
        var (x, y) = Bird(brush, 21, 14, 5, Coat, 0x45301F, Cream, 9.5, 2, 5.5, Gold, Gold, Cream);
        brush.Triangle(x + 7.2, y + 2.6, 3, 3.4, Gold, 180);
    }

    private static void Horse(Brush brush)
    {
        const int Coat = 0xA5673F, Shade = 0x86502E, Hair = 0x3A2A1E;
        brush.Box(-21, -27, 5, 21, Hair, 2.5, 13);
        var back = FourLegs(brush, 39, 17, 20, 4.6, Coat, Shade);
        brush.Box(17.5, back - 12, 9.5, 21, Coat, 4.5, 28);
        brush.Box(14, back - 13.5, 4, 18, Hair, 2, 28);
        brush.Triangle(21.5, back - 26.5, 3.4, 6.5, Coat, 12);
        brush.Ellipse(27, back - 20, 17, 9.5, Coat, 24);
        brush.Ellipse(33, back - 17, 6, 6, Shade);
        Face(brush, 26, back - 22.5, 2);
    }

    private static void Deer(Brush brush)
    {
        const int Coat = 0xB98556, Shade = 0x996B41, Horn = 0xE7D9B8;
        brush.Ellipse(-15.5, -27, 5, 5, Cream);
        var back = FourLegs(brush, 30, 14, 18, 3.2, Coat, Shade);
        brush.Ellipse(-1, back + 3.5, 20, 6, 0xDDBF9A);
        brush.Box(13.5, back - 9.5, 6, 15, Coat, 3, 22);
        brush.Box(15.5, back - 28, 1.6, 12, Horn, 0.8, -16);
        brush.Box(12, back - 31, 1.5, 6, Horn, 0.7, -62);
        brush.Box(20.5, back - 28, 1.6, 12, Horn, 0.8, 14);
        brush.Box(24.5, back - 30.5, 1.5, 6, Horn, 0.7, 60);
        brush.Ellipse(13, back - 19, 6, 3, Shade, 28);
        brush.Ellipse(19.5, back - 17, 11.5, 8.5, Coat);
        brush.Ellipse(24.5, back - 15.5, 2.4, 2, Eye);
        Face(brush, 20, back - 18.5, 2);
    }

    private static void Rooster(Brush brush)
    {
        const int Coat = 0xC2542D, Comb = 0xD92B2B, Tail = 0x1F4D3A;
        brush.Ellipse(-11, -19, 6, 17, Tail, -32);
        brush.Ellipse(-13.5, -15, 6, 15, 0x2D6A4F, -52);
        brush.Ellipse(-14, -10.5, 5, 12, Tail, -76);
        var (x, y) = Bird(brush, 17, 14, 6, Coat, 0x8E3A1E, 0xE9D9B0, 8, 2, 3.4, Gold, Gold, Tail);
        brush.Ellipse(x - 2, y - 4.6, 3.2, 3.6, Comb);
        brush.Ellipse(x + 0.6, y - 5.3, 3.4, 4.2, Comb);
        brush.Ellipse(x + 3, y - 4.4, 3, 3.4, Comb);
        brush.Ellipse(x + 3, y + 4.2, 2.6, 3.8, Comb);
    }

    private static void Bear(Brush brush)
    {
        const int Coat = 0x7A5236, Shade = 0x5F3E28;
        brush.Ellipse(-18.5, -22, 5, 5, Shade);
        var back = FourLegs(brush, 37, 25, 9, 9, Coat, Shade);
        brush.Ellipse(14, back - 16.5, 6.5, 6.5, Shade);
        brush.Ellipse(24, back - 17, 6.5, 6.5, Coat);
        brush.Ellipse(19.5, back - 8, 19, 18, Coat);
        brush.Ellipse(26.5, back - 4.5, 9.5, 7.5, 0xB58D68);
        brush.Ellipse(30, back - 6, 3.2, 2.4, Eye);
        Face(brush, 21.5, back - 10.5, 2.1);
    }

    private static void Goat(Brush brush)
    {
        const int Coat = 0xE8E1D2, Shade = 0xC5BCA9, Horn = 0x7A6A55;
        brush.Triangle(-14, -26.5, 4, 6, Shade, -24);
        var back = FourLegs(brush, 27, 14, 12, 3.2, Coat, Shade);
        brush.Box(12, back - 8, 6, 12, Coat, 3, 24);
        brush.Box(13, back - 20.5, 1.9, 9.5, Horn, 0.9, -30);
        brush.Box(16, back - 21.5, 1.9, 9.5, Horn, 0.9, -12);
        brush.Ellipse(11.5, back - 13.5, 6, 2.8, Shade, 24);
        brush.Ellipse(17, back - 13, 10.5, 8.5, Coat);
        brush.Triangle(19.5, back - 6, 3.4, 6.5, Shade, 180);
        brush.Ellipse(21.5, back - 11.5, 2, 1.8, 0xB98C8C);
        Face(brush, 17.5, back - 14.5, 1.9);
    }

    private static void Sparrow(Brush brush)
    {
        var (x, y) = Bird(brush, 11, 7.5, 3, 0xB58A62, 0x7A5536, 0x7A4E2D, 6, 0, 2.4, 0x4A3B32, 0x9B7B5B, 0x7A5536);
        brush.Ellipse(x + 0.6, y + 1.4, 3.4, 2.6, 0xE8DCC8);
    }

    private static void Camel(Brush brush)
    {
        const int Coat = 0xD9B77A, Shade = 0xBB9858;
        brush.Box(-20.5, -29, 2.2, 13, Shade, 1.1, 12);
        var back = FourLegs(brush, 38, 16, 20, 4.6, Coat, Shade);
        brush.Ellipse(-2, back - 8.5, 19, 15, Coat);
        brush.Box(19.5, back - 11.5, 7, 23, Coat, 3.5, 20);
        brush.Ellipse(21.5, back - 26.5, 3.6, 4, Shade);
        brush.Ellipse(27, back - 23, 14, 8.5, Coat, 10);
        brush.Ellipse(32.5, back - 21.5, 4.5, 5, Shade);
        Face(brush, 26.5, back - 24.5, 2);
    }

    private static void Squirrel(Brush brush)
    {
        const int Coat = 0xB5652F, Tail = 0xC97B3F;
        brush.Ellipse(-8, -13, 10, 18, Tail, -16);
        brush.Ellipse(-10.5, -21, 8, 8, Tail);
        brush.Ellipse(0, -7.5, 10, 13, Coat);
        brush.Ellipse(2.2, -6.5, 4.6, 8, 0xF0DDBE);
        brush.Ellipse(-1, -1.4, 8, 3, Coat);
        brush.Triangle(2.6, -22.5, 3, 4.6, Coat, -8);
        brush.Ellipse(4.5, -16.5, 8.5, 8, Coat);
        brush.Ellipse(5, -9.5, 4.4, 2.4, Coat, 30);
        brush.Ellipse(8.4, -15.6, 1.6, 1.4, Eye);
        Face(brush, 5.6, -17.5, 1.8);
    }

    private static void Duck(Brush brush)
    {
        var (x, y) = Bird(brush, 19, 11.5, 3, 0xB9A88E, 0x8D7B62, 0x2F6B4F, 8, 2, 0.1, Gold, 0xE2853A, Cream);
        brush.Ellipse(x - 1, y + 4, 6, 2, Cream);
        brush.Ellipse(x + 6, y + 1, 7.5, 3.2, Gold);
    }

    private static void Cow(Brush brush)
    {
        const int Coat = 0xF4F0E6, Shade = 0xD4CEC0, Patch = 0x3A2E28, Pink = 0xE9B4A8;
        brush.Box(-21, -24, 2.2, 15, Shade, 1.1, 8);
        brush.Ellipse(-21.8, -15.5, 4, 5.5, Patch);
        brush.Ellipse(-6, -12, 8, 5, Pink);
        var back = FourLegs(brush, 39, 22, 12, 6, Coat, Shade);
        brush.Ellipse(-7, back - 3, 13, 10, Patch);
        brush.Ellipse(7, back + 3, 9, 7.5, Patch);
        brush.Triangle(17.5, back - 14.5, 2.6, 5.5, 0xD9CBA8, -18);
        brush.Triangle(24, back - 15, 2.6, 5.5, 0xD9CBA8, 14);
        brush.Ellipse(14.5, back - 9, 6.5, 3.4, Patch, 20);
        brush.Ellipse(21.5, back - 6.5, 15, 13.5, Coat);
        brush.Ellipse(27, back - 2.5, 9, 7.5, Pink);
        brush.Ellipse(29, back - 3, 1.4, 1.8, 0xC98C80);
        Face(brush, 22.5, back - 8.5, 2.1);
    }

    private static void Wolf(Brush brush)
    {
        const int Coat = 0x8E9196, Shade = 0x6F7378, Pale = 0xC9CCD0;
        brush.Ellipse(-21, -21, 17, 6.5, Shade, 28);
        var back = FourLegs(brush, 33, 14, 14, 4, Coat, Shade);
        brush.Ellipse(10, back + 3, 12, 7, Pale);
        brush.Box(13.5, back - 5.5, 8, 12, Coat, 4, 30);
        brush.Triangle(14.5, back - 16.5, 5, 7.5, Shade, -12);
        brush.Triangle(19.5, back - 17, 5, 7.5, Coat, 8);
        brush.Ellipse(19, back - 9.5, 13, 10.5, Coat);
        brush.Ellipse(26, back - 7.5, 10, 5.5, Pale);
        brush.Ellipse(30.5, back - 8.5, 2.6, 2.2, Eye);
        Face(brush, 20.5, back - 11, 1.9);
    }

    private static void Peacock(Brush brush)
    {
        const int Blue = 0x1F5FA8, Green = 0x1F7A5A;
        brush.Ellipse(-11, -24, 32, 36, Green);
        brush.Ellipse(-11, -24, 25, 29, 0x2A9470);
        foreach (var (x, y) in new[] { (-21.0, -30.0), (-14, -38), (-5, -36), (-20, -19), (-10, -27), (-2, -25), (-12, -14) })
        {
            brush.Ellipse(x, y, 5, 6, Gold);
            brush.Ellipse(x, y + 0.4, 2.6, 3.2, Blue);
        }

        var (headX, headY) = Bird(brush, 14, 10, 7, Blue, 0x184C86, Blue, 6.5, 9, 2.8, 0xCFC6A8, 0x8C8778, Green);
        brush.Box(headX - 1.5, headY - 5.5, 0.9, 5, Blue, 0.4, -18);
        brush.Box(headX + 0.4, headY - 6, 0.9, 5, Blue, 0.4);
        brush.Box(headX + 2.3, headY - 5.5, 0.9, 5, Blue, 0.4, 18);
        brush.Ellipse(headX - 2.6, headY - 8, 1.8, 1.8, Gold);
        brush.Ellipse(headX + 0.4, headY - 8.8, 1.8, 1.8, Gold);
        brush.Ellipse(headX + 3.3, headY - 8, 1.8, 1.8, Gold);
    }

    private static void Donkey(Brush brush)
    {
        const int Coat = 0x9B948A, Shade = 0x7D766C, Pale = 0xE6DFD2, Hair = 0x4A433B;
        brush.Box(-18, -25, 2.2, 13, Shade, 1.1, 10);
        brush.Ellipse(-19, -17.5, 4, 6, Hair);
        var back = FourLegs(brush, 33, 16, 14, 4.6, Coat, Shade);
        brush.Ellipse(-1, back + 4, 20, 6.5, Pale);
        brush.Box(14.5, back - 8.5, 8.5, 15, Coat, 4, 28);
        brush.Box(11.6, back - 9.5, 3, 12, Hair, 1.5, 28);
        brush.Box(15.5, back - 24.5, 3.6, 12.5, Shade, 1.8, -12);
        brush.Box(20, back - 24.5, 3.6, 12.5, Coat, 1.8, 12);
        brush.Ellipse(22, back - 14.5, 15.5, 9.5, Coat, 22);
        brush.Ellipse(27.5, back - 11.5, 7.5, 6.5, Pale);
        Face(brush, 21.5, back - 16.5, 2);
    }

    private static void Hedgehog(Brush brush)
    {
        const int Spines = 0x6B5442, Dark = 0x4A3829, FaceColour = 0xD9B48F;
        for (var spine = 0; spine < 7; spine++)
        {
            var angle = (200 + (spine * 23)) * Math.PI / 180;
            brush.Triangle(-1 + (Math.Cos(angle) * 9.5), -7 + (Math.Sin(angle) * 6.5), 4, 6, Dark, (spine * 23) - 70);
        }

        brush.Ellipse(-1, -6.5, 19, 12.5, Spines);
        brush.Ellipse(-4, -1.2, 4.5, 2.4, FaceColour);
        brush.Ellipse(4, -1.2, 4.5, 2.4, FaceColour);
        brush.Ellipse(8, -5, 9.5, 7.5, FaceColour);
        brush.Triangle(12.6, -4.6, 5, 4, FaceColour, 90);
        brush.Ellipse(14.4, -4.6, 2.2, 2, Eye);
        Face(brush, 8.6, -6.4, 1.7);
    }

    private static void Raven(Brush brush)
    {
        const int Black = 0x23262B;
        var (x, y) = Bird(brush, 19, 11.5, 5, Black, 0x15171A, Black, 9, 1, 6, 0x3C4047, 0x3C4047, 0x15171A);
        brush.Ellipse(x + 1.3, y - 0.9, 2.6, 2.8, 0xD9D9D9);
        brush.Ellipse(x + 1.5, y - 0.9, 1.4, 1.6, 0x15171A);
    }

    private static void Badger(Brush brush)
    {
        const int Coat = 0x8A8D90, Dark = 0x2F3033;
        brush.Ellipse(-17, -9.5, 9, 5.5, Coat);
        var back = FourLegs(brush, 30, 13, 5, 5, Coat, Dark);
        brush.Box(-11.5, -2.5, 5, 8, Dark, 2.5);
        brush.Box(10.5, -2.5, 5, 8, Dark, 2.5);
        brush.Ellipse(11.5, back - 5.5, 4.4, 4.4, Dark);
        brush.Ellipse(17, back - 0.5, 14, 10.5, Cream);
        brush.Box(17, back - 1.5, 13, 3.2, Dark, 1.6, -12);
        brush.Ellipse(23.5, back + 1.2, 2.6, 2.2, Eye);
        brush.Ellipse(18.5, back - 2, 1.4, 1.5, Cream);
    }

    private static void Heron(Brush brush)
    {
        const int Coat = 0x9FB2C2;
        var (x, y) = Bird(brush, 19, 10.5, 17, Coat, 0x7C90A2, Cream, 7, 15, 10, Gold, 0xC9A74E, 0x7C90A2);
        brush.Box(x - 4.5, y - 1.5, 7, 1.3, 0x2F3B46, 0.6, 16);
    }

    private static void SaplingOf(Brush brush, bool watered)
    {
        var leaf = watered ? 0x5FAE4B : 0xB7B86A;
        var tall = watered ? 15.0 : 9.5;
        brush.Ellipse(0, -0.6, 13, 4.4, watered ? 0x5A3A26 : 0xA07A55);
        brush.Box(0, -tall / 2, 1.7, tall, 0x7A5A3A, 0.8);
        if (watered)
        {
            brush.Ellipse(-3.6, -8, 7, 3.2, leaf, 24);
            brush.Ellipse(3.6, -10, 7, 3.2, 0x4C9A3F, -24);
            brush.Ellipse(-3.2, -13.4, 6.4, 3, 0x4C9A3F, 34);
            brush.Ellipse(3.2, -15, 6.4, 3, leaf, -34);
            brush.Ellipse(0, -17, 3.4, 6, leaf);
        }
        else
        {
            brush.Ellipse(-2.6, -7.4, 5.4, 2.4, leaf, -34);
            brush.Ellipse(2.4, -8.6, 5, 2.2, leaf, 40);
        }
    }

    private static void BranchOf(Brush brush)
    {
        const int Wood = 0x7A5A3A, Bark = 0x5F4329;
        brush.Box(0, -2.4, 30, 3.6, Wood, 1.8, 8);
        brush.Box(-6, -5.6, 11, 2.2, Bark, 1.1, -32);
        brush.Box(8, -5, 9, 2, Bark, 1, 38);
        brush.Ellipse(-11, -8.8, 5, 2.4, 0xB9A25A, -30);
        brush.Ellipse(12.4, -7.4, 4.6, 2.2, 0xA9914E, 30);
    }

    private sealed class Brush
    {
        private readonly List<ThingShape> shapes = [];

        public IReadOnlyList<ThingShape> Shapes => shapes;

        public void Ellipse(double x, double y, double width, double height, int colour, double turn = 0) =>
            shapes.Add(new ThingShape(ThingShapeKind.Ellipse, x, y, width, height, turn, colour));

        public void Box(double x, double y, double width, double height, int colour, double round = 0, double turn = 0) =>
            shapes.Add(new ThingShape(ThingShapeKind.Box, x, y, width, height, turn, colour, round));

        public void Triangle(double x, double y, double width, double height, int colour, double turn = 0) =>
            shapes.Add(new ThingShape(ThingShapeKind.Triangle, x, y, width, height, turn, colour));
    }
}
