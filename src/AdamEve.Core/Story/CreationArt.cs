using AdamEve.Core.World;

namespace AdamEve.Core.Story;

/// <summary>
/// The shapes of the picture of the days of creation, made by code in the manner of the garden (design, section
/// 5.2): cut-paper shapes, light from above, the same greens, golds and blues. One composition serves all seven
/// days: a sea under a horizon, land that rises on the left, the firmament above. Each layer draws what its verses
/// tell and nothing else: no figure of God (decision D5), no serpent, no person but the two far figures of light,
/// which are not drawn here (they are the rigs, under rule M1).
/// </summary>
internal static class CreationArt
{
    private const double Wide = CreationPicture.Width;
    private const double High = CreationPicture.Height;
    private const double Horizon = 118;
    private const double FirmamentTop = 36;
    private const int White = 0xFFFFFF;

    // The top of the land, from the left edge of the picture to the shore on the right.
    private static readonly double[] Ridge = [0, 128, 30, 120, 70, 112, 110, 118, 150, 128, 190, 136, 225, 150, 245, 166];

    /// <summary>The shapes of a layer, the farthest first.</summary>
    public static IReadOnlyList<PictureShape> Of(SceneLayer layer)
    {
        var brush = new PictureBrush();
        switch (layer)
        {
            case SceneLayer.Darkness: Darkness(brush); break;
            case SceneLayer.Waters: Waters(brush); break;
            case SceneLayer.Deep: Deep(brush); break;
            case SceneLayer.Firmament: Firmament(brush); break;
            case SceneLayer.Light: Light(brush); break;
            case SceneLayer.Day: Day(brush); break;
            case SceneLayer.Stars: Stars(brush); break;
            case SceneLayer.Sun: Sun(brush); break;
            case SceneLayer.Moon: Moon(brush); break;
            case SceneLayer.Land: Land(brush); break;
            case SceneLayer.Grass: Grass(brush); break;
            case SceneLayer.Trees: Trees(brush); break;
            case SceneLayer.Whales: Whales(brush); break;
            case SceneLayer.Fish: Fish(brush); break;
            case SceneLayer.Birds: Birds(brush); break;
            case SceneLayer.Animals: Animals(brush); break;
            case SceneLayer.Presence: Presence(brush); break;
        }

        return brush.Shapes;
    }

    // A number in [0, 1) from two whole numbers: the same picture every time.
    private static double Chance(int index, int salt) => GardenScenery.Chance(index, salt, 17);

    private static PaintStop Stop(double at, int colour, double opacity = 1) => new(at, colour, opacity);

    // A long thin shape with pointed ends: a crest of a wave, a line of foam, a streak of light.
    private static void Lens(PictureBrush brush, double x, double y, double halfWidth, double halfHeight, int colour, double opacity) =>
        brush.Curve(Paint.Flat(colour), opacity, x - halfWidth, y, x, y - halfHeight, x + halfWidth, y, x, y + halfHeight);

    // Genesis 1:2: darkness.
    private static void Darkness(PictureBrush brush) =>
        brush.Polygon(Paint.Glow(160, 90, 230, Stop(0, 0x161F40), Stop(1, 0x04060D)), 1, 0, 0, Wide, 0, Wide, High, 0, High);

    // Genesis 1:2: the face of the deep, in darkness.
    private static void Deep(PictureBrush brush)
    {
        brush.Curve(Paint.Down(118, High, Stop(0, 0x16264F), Stop(1, 0x05080F)), 1, -20, 124, 60, 121, 160, 125, 260, 121, 340, 124, 340, 230, -20, 230);
        for (var wave = 0; wave < 9; wave++)
        {
            Lens(brush, 20 + (Chance(wave, 1) * 280), 132 + (wave * 7.5), 16 + (Chance(wave, 2) * 22), 0.9, 0x34508E, 0.55);
        }
    }

    // Genesis 1:3: light, breaking from the darkness.
    private static void Light(PictureBrush brush)
    {
        const double x = 160, y = 84;
        brush.Ellipse(x, y, 150, 108, Paint.Oval(x, y, 150, 108, Stop(0, 0xFFFFF4), Stop(0.16, 0xFFF3C0, 0.95), Stop(0.45, 0xFFD98A, 0.5), Stop(1, 0xFFC060, 0)));
        for (var ray = 0; ray < 16; ray++)
        {
            var angle = ((ray + (0.35 * Chance(ray, 3))) / 16 * Math.PI * 2) + 0.2;
            var reach = 84 + (Chance(ray, 4) * 56);
            double cos = Math.Cos(angle), sin = Math.Sin(angle), across = 0.035 + (0.03 * Chance(ray, 5));
            brush.Polygon(Paint.Glow(x, y, reach, Stop(0, 0xFFF8DC, 0.5), Stop(1, 0xFFE9B0, 0)), 1,
                x + (cos * 10), y + (sin * 10), x + (Math.Cos(angle - across) * reach), y + (Math.Sin(angle - across) * reach), x + (Math.Cos(angle + across) * reach), y + (Math.Sin(angle + across) * reach));
        }

        brush.Ellipse(x, y, 17, 17, Paint.Glow(x, y, 17, Stop(0, White), Stop(0.6, 0xFFFDF0), Stop(1, 0xFFF3C0, 0)));
    }

    // Genesis 1:4 and 1:5: the light divided from the darkness: Day on one side, Night on the other.
    private static void Day(PictureBrush brush)
    {
        brush.Polygon(Paint.Across(0, 214, Stop(0, 0xFFF6D4), Stop(0.5, 0xFFF0C4, 0.97), Stop(0.8, 0xFFE2A4, 0.6), Stop(1, 0xFFD890, 0)), 1, 0, 0, 214, 0, 214, High, 0, High);
        brush.Ellipse(46, 78, 120, 96, Paint.Oval(46, 78, 120, 96, Stop(0, White, 0.75), Stop(1, White, 0)));
        for (var wave = 0; wave < 7; wave++)
        {
            Lens(brush, 14 + (Chance(wave, 6) * 120), 134 + (wave * 9), 14 + (Chance(wave, 7) * 18), 0.9, 0xE0B870, 0.5);
        }
    }

    // Genesis 1:2 and 1:6: the waters.
    private static void Waters(PictureBrush brush)
    {
        brush.Polygon(Paint.Down(0, High, Stop(0, 0x3F88BA), Stop(0.55, 0x23619A), Stop(1, 0x143C68)), 1, 0, 0, Wide, 0, Wide, High, 0, High);
        for (var wave = 0; wave < 34; wave++)
        {
            var y = 6 + (wave * 5.8) + (Chance(wave, 8) * 3);
            var light = wave % 3 != 2;
            Lens(brush, Chance(wave, 9) * Wide, y, 12 + (Chance(wave, 10) * 26), 0.8 + (Chance(wave, 11) * 0.7), light ? 0x8CC4E2 : 0x0E3156, light ? 0.5 : 0.35);
        }
    }

    // Genesis 1:6 to 1:8: the firmament in the midst of the waters, dividing the waters from the waters. Heaven.
    private static void Firmament(PictureBrush brush)
    {
        var edge = new List<double>();
        for (var step = 0; step <= 8; step++)
        {
            edge.AddRange([step * 40, FirmamentTop + (3 * Math.Sin(step * 1.9))]);
        }

        for (var step = 8; step >= 0; step--)
        {
            edge.AddRange([step * 40, Horizon + (1.6 * Math.Sin((step * 2.3) + 1))]);
        }

        brush.Polygon(Paint.Down(FirmamentTop, Horizon, Stop(0, 0x86BDEC), Stop(0.55, 0xC6E2F2), Stop(1, 0xF6E7BE)), 1, [.. edge]);
        // The waters above the firmament end in a bright rim; the waters under it take its light.
        for (var crest = 0; crest < 8; crest++)
        {
            Lens(brush, 22 + (crest * 40), FirmamentTop + 1.5 + (3 * Math.Sin((crest + 0.5) * 1.9)), 17, 1.1, White, 0.6);
            Lens(brush, 12 + (Chance(crest, 12) * 296), Horizon + 5 + (Chance(crest, 13) * 14), 14 + (Chance(crest, 14) * 16), 0.9, 0xFFF3CF, 0.5);
        }
    }

    // The land as one closed outline, from its top (Ridge) down to the lower edge of the picture; `sink` lowers the
    // top and draws the shore in, so that the grass leaves a strand of sand along the water.
    private static double[] LandOutline(double sink, double draw)
    {
        var points = new List<double>();
        for (var index = 0; index < Ridge.Length; index += 2)
        {
            var share = (double)index / (Ridge.Length - 2);
            points.AddRange([Ridge[index] - (draw * share), Ridge[index + 1] + sink]);
        }

        points.AddRange([232 - draw, 183, 206 - draw, 197, 188 - draw, 214, -20, 214, -20, 128 + sink]);
        return [.. points];
    }

    // Genesis 1:9 and 1:10: the dry land appears; Earth and Seas.
    private static void Land(PictureBrush brush)
    {
        brush.Curve(Paint.Down(96, 130, Stop(0, 0xDCC195), Stop(1, 0xC4A473)), 1, -20, 122, 20, 108, 62, 98, 104, 103, 140, 112, 172, 121, 150, 136, -20, 140);
        brush.Curve(Paint.Down(110, High, Stop(0, 0xE4CF9C), Stop(0.3, 0xC9A66C), Stop(1, 0x8B6740)), 1, LandOutline(0, 0));
        // Where the land meets the seas: foam.
        (double X, double Y)[] shore = [(203, 142), (228, 154), (243, 169), (233, 185), (211, 197)];
        foreach (var (x, y) in shore)
        {
            Lens(brush, x + 7, y + 2, 9, 1.1, White, 0.7);
        }
    }

    // Genesis 1:11 and 1:12: grass, and the herb yielding seed.
    private static void Grass(PictureBrush brush)
    {
        brush.Curve(Paint.Down(96, 132, Stop(0, 0xC4DE94), Stop(1, 0x9DC873)), 1, -20, 123, 20, 109, 62, 99, 104, 104, 140, 113, 164, 121, 146, 134, -20, 138);
        brush.Curve(Paint.Down(112, High, Stop(0, 0xB4D974), Stop(0.4, 0x7DB85F), Stop(1, 0x4E9150)), 1, LandOutline(2.5, 13));
        for (var herb = 0; herb < 46; herb++)
        {
            var x = 6 + (Chance(herb, 15) * 200);
            var top = RidgeAt(x) + 5;
            var y = top + (Chance(herb, 16) * (190 - top));
            if (x > 236 - ((y - 150) * 0.9) - 22)
            {
                continue;
            }

            var tall = 3 + (Chance(herb, 17) * 3.5);
            brush.Polygon(Paint.Flat(herb % 2 == 0 ? 0x3F8A47 : 0x5FA058), 1, x - 1.2, y, x + 1.2, y, x + 0.4, y - tall);
            brush.Polygon(Paint.Flat(0x4E9A4A), 1, x + 0.4, y, x + 2.6, y, x + 2.8, y - (tall * 0.7));
            if (herb % 3 == 0)
            {
                brush.Ellipse(x + 0.4, y - tall - 0.6, 1, 1, Paint.Flat(herb % 2 == 0 ? 0xF7E08A : 0xFFF6DC));
            }
        }
    }

    // The height of the top of the land at a place across the picture.
    private static double RidgeAt(double x)
    {
        for (var index = 0; index < Ridge.Length - 2; index += 2)
        {
            if (x <= Ridge[index + 2])
            {
                var share = (x - Ridge[index]) / (Ridge[index + 2] - Ridge[index]);
                return Ridge[index + 1] + ((Ridge[index + 3] - Ridge[index + 1]) * Math.Clamp(share, 0, 1));
            }
        }

        return Ridge[^1];
    }

    // Genesis 1:11 and 1:12: the fruit tree yielding fruit.
    private static void Trees(PictureBrush brush)
    {
        (double X, double Y, double Size, int Seed)[] trees = [(22, 131, 0.8, 1), (52, 123, 1.15, 2), (92, 121, 1.3, 3), (128, 129, 1.0, 4), (160, 138, 0.75, 5)];
        foreach (var (x, y, size, seed) in trees)
        {
            brush.Polygon(Paint.Flat(0x6E4F33), 1, x - (1.8 * size), y, x + (1.8 * size), y, x + (1.1 * size), y - (13 * size), x - (1.1 * size), y - (13 * size));
            Crown(brush, x, y - (20 * size), 12 * size, 10 * size, 0x2F7B4F, seed);
            Crown(brush, x - (2.5 * size), y - (23 * size), 9.5 * size, 7.5 * size, 0x4E9F4C, seed + 20);
            Crown(brush, x - (5 * size), y - (26 * size), 5.5 * size, 4 * size, 0x86C063, seed + 40);
            for (var fruit = 0; fruit < 6; fruit++)
            {
                var around = Chance(fruit, 20 + seed) * Math.PI * 2;
                var away = 0.35 + (0.5 * Chance(fruit, 30 + seed));
                brush.Ellipse(x + (Math.Cos(around) * 10 * size * away), y - (20 * size) + (Math.Sin(around) * 8 * size * away), 1.25 * size, 1.25 * size, Paint.Flat(fruit % 2 == 0 ? 0xF29A3E : 0xE0563A));
            }
        }
    }

    // A mass of leaves: a many-sided shape with no two sides alike, as if cut from paper.
    private static void Crown(PictureBrush brush, double x, double y, double radiusX, double radiusY, int colour, int seed)
    {
        const int sides = 9;
        var points = new double[sides * 2];
        for (var side = 0; side < sides; side++)
        {
            var angle = (side + (0.5 * Chance(side, 50 + seed))) / sides * Math.PI * 2;
            var reach = 0.82 + (0.3 * Chance(side, 60 + seed));
            points[side * 2] = x + (Math.Cos(angle) * radiusX * reach);
            points[(side * 2) + 1] = y + (Math.Sin(angle) * radiusY * reach);
        }

        brush.Polygon(Paint.Flat(colour), 1, points);
    }

    // Genesis 1:16: the greater light to rule the day.
    private static void Sun(PictureBrush brush)
    {
        const double x = 262, y = 62;
        brush.Ellipse(x, y, 50, 50, Paint.Glow(x, y, 50, Stop(0, 0xFFF0B8, 0.75), Stop(0.5, 0xFFE19A, 0.3), Stop(1, 0xFFD98A, 0)));
        for (var ray = 0; ray < 12; ray++)
        {
            var angle = ray / 12.0 * Math.PI * 2;
            var reach = ray % 2 == 0 ? 27 : 23;
            brush.Polygon(Paint.Flat(0xFBD566), 0.85, x + (Math.Cos(angle - 0.09) * 18), y + (Math.Sin(angle - 0.09) * 18), x + (Math.Cos(angle + 0.09) * 18), y + (Math.Sin(angle + 0.09) * 18), x + (Math.Cos(angle) * reach), y + (Math.Sin(angle) * reach));
        }

        brush.Ellipse(x, y, 15, 15, Paint.Glow(x - 4, y - 4, 22, Stop(0, 0xFFF9D6), Stop(0.6, 0xFBD35C), Stop(1, 0xF2B93A)));
    }

    // Genesis 1:16: the lesser light to rule the night; the night it rules is the side of the firmament it stands in.
    private static void Moon(PictureBrush brush)
    {
        const double x = 62, y = 60;
        brush.Polygon(Paint.Across(0, 196, Stop(0, 0x17224F, 0.86), Stop(0.55, 0x22346C, 0.6), Stop(1, 0x2B4480, 0)), 1, 0, FirmamentTop + 3.5, 196, FirmamentTop + 3.5, 196, Horizon - 2, 0, Horizon - 2);
        brush.Ellipse(x, y, 30, 30, Paint.Glow(x, y, 30, Stop(0, White, 0.5), Stop(1, White, 0)));
        brush.Ellipse(x, y, 11, 11, Paint.Glow(x - 3, y - 3, 16, Stop(0, 0xFFFDF2), Stop(1, 0xDDE3EE)));
        brush.Ellipse(x + 3.2, y + 2.4, 2.4, 2, Paint.Flat(0xC5CCDC), 0.7);
        brush.Ellipse(x - 3.6, y + 3.4, 1.5, 1.3, Paint.Flat(0xC5CCDC), 0.7);
        brush.Ellipse(x - 1, y - 4.4, 1.8, 1.5, Paint.Flat(0xC5CCDC), 0.6);
    }

    // Genesis 1:16: he made the stars also.
    private static void Stars(PictureBrush brush)
    {
        for (var star = 0; star < 46; star++)
        {
            var x = 6 + (Chance(star, 70) * 178);
            var y = FirmamentTop + 8 + (Chance(star, 71) * (Horizon - FirmamentTop - 22));
            if (Math.Abs(x - 62) < 19 && Math.Abs(y - 60) < 19)
            {
                continue;
            }

            var size = 1.3 + (Chance(star, 72) * 2.3);
            var thin = size * 0.28;
            brush.Polygon(Paint.Flat(star % 4 == 0 ? 0xFFF1BE : White), Math.Clamp(1.25 - (x / 190), 0.3, 1) * (0.75 + (0.25 * Chance(star, 73))),
                x, y - size, x + thin, y - thin, x + size, y, x + thin, y + thin, x, y + size, x - thin, y + thin, x - size, y, x - thin, y - thin);
        }
    }

    // Genesis 1:21: great whales.
    private static void Whales(PictureBrush brush)
    {
        Whale(brush, 272, 171, 1.15, 1);
        Whale(brush, 297, 147, 0.62, -1);
    }

    private static void Whale(PictureBrush brush, double x, double y, double size, int way)
    {
        double X(double at) => x + (at * size * way);
        double Y(double at) => y + (at * size);
        brush.Curve(Paint.Down(Y(-10), Y(9), Stop(0, 0x5A82AB), Stop(1, 0x2D4B70)), 1,
            X(-25), Y(1), X(-14), Y(-8), X(4), Y(-9), X(19), Y(-3), X(28), Y(-10), X(27), Y(-1), X(30), Y(6), X(19), Y(2), X(6), Y(8), X(-15), Y(8));
        brush.Curve(Paint.Flat(0xB7D0E2), 0.85, X(-23), Y(3.4), X(-8), Y(5), X(8), Y(4.6), X(4), Y(7.6), X(-14), Y(7.8));
        brush.Polygon(Paint.Flat(0x2D4B70), 1, X(-4), Y(4), X(2), Y(4.5), X(-5), Y(11));
        brush.Ellipse(X(-17), Y(-1.6), 1.1 * size, 1.1 * size, Paint.Flat(0x16243A));
        brush.Ellipse(X(-17.4), Y(-2), 0.4 * size, 0.4 * size, Paint.Flat(White));
    }

    // Genesis 1:20 and 1:21: the moving creature that hath life, which the waters brought forth abundantly.
    private static void Fish(PictureBrush brush)
    {
        (double X, double Y, double Size, int Colour, int Way)[] school =
        [
            (251, 188, 1.0, 0xF29A3E, 1), (264, 194, 0.85, 0xF7C948, 1), (240, 195, 0.7, 0xF08A7B, 1), (305, 188, 0.9, 0xF7C948, -1), (291, 195, 0.75, 0xF29A3E, -1),
            (311, 170, 0.7, 0xF08A7B, -1), (262, 128, 0.55, 0xF7C948, 1), (275, 133, 0.5, 0xF29A3E, 1), (252, 134, 0.45, 0xF08A7B, 1), (226, 193, 0.55, 0xF7C948, 1),
        ];
        foreach (var (x, y, size, colour, way) in school)
        {
            brush.Polygon(Paint.Flat(colour), 1, x - (5 * size * way), y, x - (9 * size * way), y - (3 * size), x - (9 * size * way), y + (3 * size));
            Lens(brush, x, y, 6.5 * size, 2.7 * size, colour, 1);
            Lens(brush, x + (1 * size * way), y - (0.6 * size), 3.4 * size, 1 * size, 0xFFF1C4, 0.6);
            brush.Ellipse(x + (3.6 * size * way), y - (0.5 * size), 0.7 * size, 0.7 * size, Paint.Flat(0x2A2A32));
        }
    }

    // Genesis 1:20 and 1:21: fowl that fly above the earth in the open firmament of heaven.
    private static void Birds(PictureBrush brush)
    {
        (double X, double Y, double Span, int Colour)[] flock =
        [
            (150, 84, 7.5, 0x3C4A60), (170, 72, 6, White), (188, 88, 8.5, 0x3C4A60), (206, 76, 5.5, 0x3C4A60), (222, 96, 7, White),
            (136, 100, 5, 0x3C4A60), (196, 104, 4.5, 0x3C4A60), (176, 56, 4.5, 0x3C4A60), (214, 58, 4, White),
        ];
        foreach (var (x, y, span, colour) in flock)
        {
            // Two wings in one line that dips at the body: seen from afar, in flight.
            brush.Curve(Paint.Flat(colour), colour == White ? 0.95 : 0.9,
                x - span, y - (span * 0.1), x - (span * 0.5), y - (span * 0.42), x, y, x + (span * 0.5), y - (span * 0.42), x + span, y - (span * 0.1),
                x + (span * 0.5), y - (span * 0.2), x, y + (span * 0.2), x - (span * 0.5), y - (span * 0.2));
        }
    }

    // Genesis 1:24 and 1:25: cattle, and creeping thing, and beast of the earth. Plain friendly shapes; no serpent.
    private static void Animals(PictureBrush brush)
    {
        // An ox.
        Legs(brush, 66, 163, 16, 6.5, 0x7C5A3C);
        brush.Ellipse(66, 157, 12, 6.8, Paint.Down(150, 164, Stop(0, 0xB98F62), Stop(1, 0x94704A)));
        brush.Ellipse(79.5, 152.5, 4.6, 4.1, Paint.Flat(0xA9825A));
        Lens(brush, 77, 148.4, 2.6, 0.7, 0xF4EEDD, 1);
        Lens(brush, 82.4, 148.2, 2.6, 0.7, 0xF4EEDD, 1);
        brush.Ellipse(82.6, 154, 2, 1.7, Paint.Flat(0xD9BE9C));
        brush.Ellipse(80.6, 151.6, 0.6, 0.6, Paint.Flat(0x2B1D16));

        // A sheep.
        Legs(brush, 112, 174, 10, 5, 0x4A4038);
        foreach (var (x, y, radius) in new[] { (107.0, 169.0, 4.6), (112.0, 167.4, 5.2), (117.0, 169.0, 4.6), (110.0, 171.6, 4.4), (115.0, 171.6, 4.4) })
        {
            brush.Ellipse(x, y, radius, radius, Paint.Flat(0xF6F0E0));
        }

        brush.Ellipse(120.4, 167, 2.7, 3, Paint.Flat(0x4A4038));
        brush.Ellipse(121.2, 166.4, 0.5, 0.5, Paint.Flat(White));

        // A deer.
        Legs(brush, 150, 160, 10, 8.5, 0x9A7446);
        brush.Ellipse(150, 153.5, 8.4, 4.4, Paint.Down(149, 158, Stop(0, 0xD3A76C), Stop(1, 0xB5884F)));
        brush.Polygon(Paint.Flat(0xC99A5E), 1, 154.5, 153, 158.6, 151.5, 160.6, 143.6, 157.6, 143.2);
        brush.Ellipse(160.2, 142.6, 2.9, 2.2, Paint.Flat(0xD3A76C));
        brush.Polygon(Paint.Flat(0x7C5A3C), 1, 158.6, 141, 159.4, 141, 157.6, 135.2, 155.4, 137.6, 156, 138, 157.4, 136.8);
        brush.Polygon(Paint.Flat(0x7C5A3C), 1, 160.6, 141, 161.4, 141, 163.6, 135.4, 165.6, 138, 165, 138.4, 163.6, 137);
        brush.Ellipse(161.4, 142.2, 0.5, 0.5, Paint.Flat(0x2B1D16));
        brush.Ellipse(141.8, 152.6, 1.3, 1.5, Paint.Flat(0xFFF6DC));

        // A creeping thing: a tortoise.
        brush.Ellipse(183.6, 180.6, 2, 1.5, Paint.Flat(0xA9B77C));
        brush.Ellipse(196.4, 180.2, 2.6, 2, Paint.Flat(0xA9B77C));
        brush.Curve(Paint.Down(172, 181, Stop(0, 0x86AD5C), Stop(1, 0x55803F)), 1, 182, 180.4, 184.4, 175.2, 189.4, 173.2, 194, 175.6, 195.6, 180.4);
        brush.Polygon(Paint.Flat(0xA6C870), 0.8, 186, 176.4, 189.4, 174.4, 192.4, 176.6, 189.2, 178.6);
        brush.Ellipse(197.4, 179.8, 0.45, 0.45, Paint.Flat(0x2B1D16));

        // A beast of the earth: a hare.
        brush.Ellipse(34, 175, 5.6, 4, Paint.Flat(0xBDB4A6));
        brush.Ellipse(39.4, 171.6, 2.8, 2.6, Paint.Flat(0xC9C1B4));
        Lens(brush, 38.6, 166.4, 1, 3.4, 0xBDB4A6, 1);
        Lens(brush, 40.8, 166.8, 1, 3.2, 0xC9C1B4, 1);
        brush.Ellipse(28.6, 175, 1.5, 1.5, Paint.Flat(0xF6F0E0));
        brush.Ellipse(40.4, 171.2, 0.45, 0.45, Paint.Flat(0x2B1D16));
    }

    private static void Legs(PictureBrush brush, double x, double footY, double spread, double length, int colour)
    {
        foreach (var at in new[] { -0.5, -0.3, 0.3, 0.5 })
        {
            var legX = x + (at * spread);
            brush.Polygon(Paint.Flat(colour), 1, legX - 0.9, footY - length, legX + 0.9, footY - length, legX + 0.8, footY, legX - 0.8, footY);
        }
    }

    // The presence of God: a warm light above the earth while his voice speaks. Light only, never a figure.
    private static void Presence(PictureBrush brush)
    {
        const double x = 160, y = 34;
        brush.Ellipse(x, y, 96, 32, Paint.Oval(x, y, 96, 32, Stop(0, 0xFFD98A, 0.6), Stop(1, 0xFFD98A, 0)));
        brush.Ellipse(x, y, 60, 21, Paint.Oval(x, y, 60, 21, Stop(0, 0xFFE9B0, 0.7), Stop(1, 0xFFE9B0, 0)));
        brush.Ellipse(x, y, 30, 11, Paint.Oval(x, y, 30, 11, Stop(0, 0xFFF9E6, 0.9), Stop(1, 0xFFF6D8, 0)));
    }
}
