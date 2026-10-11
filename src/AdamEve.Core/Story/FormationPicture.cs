namespace AdamEve.Core.Story;

/// <summary>
/// A layer of the picture of beat B8, Genesis 2:4 to 2:7. Each is what a verse tells. None is a figure of God, who
/// is never shown as one (design, decision D5): the breath of life is <see cref="Breath"/>, a light. No layer shows
/// a body being shaped: the dust is dust in the air, and the man is there, whole, when the verse has been read.
/// </summary>
public enum FormationLayer
{
    /// <summary>The heavens (Genesis 2:4).</summary>
    Heavens,

    /// <summary>The earth, bare: no plant of the field is in it yet (2:4, 2:5).</summary>
    Earth,

    /// <summary>The mist that went up from the earth (2:6).</summary>
    Mist,

    /// <summary>The dust of the ground (2:7), red-brown, in the air.</summary>
    Dust,

    /// <summary>The breath of life (2:7): a warm light. Never a figure.</summary>
    Breath,

    /// <summary>The man (2:7): the rig of the garden, standing, whole, judged by the M1 check. It has no shapes here.</summary>
    Man,
}

/// <summary>
/// The picture of beat B8 (design, section 2.2): what is seen with each of its four cards. Art made by code: the
/// shapes are outlines and gradients from numbers, in the manner of the pictures of the days of creation.
/// </summary>
public static class FormationPicture
{
    /// <summary>The width of the picture, in its own units.</summary>
    public const int Width = 320;

    /// <summary>The height of the picture, in its own units.</summary>
    public const int Height = 200;

    /// <summary>Where the man stands in the picture: the place of his feet.</summary>
    public const int ManX = 160;

    /// <inheritdoc cref="ManX"/>
    public const int ManY = 168;

    private const double Ground = 132;

    private static readonly Dictionary<FormationLayer, IReadOnlyList<PictureShape>> Shapes = [];

    /// <summary>The layers seen with a card of the beat, the farthest first.</summary>
    /// <param name="card">The card, from 0: Genesis 2:4, 2:5, 2:6, 2:7.</param>
    public static IReadOnlyList<FormationLayer> Layers(int card) => card switch
    {
        < 2 => [FormationLayer.Heavens, FormationLayer.Earth],
        2 => [FormationLayer.Heavens, FormationLayer.Earth, FormationLayer.Mist],
        _ => [FormationLayer.Heavens, FormationLayer.Earth, FormationLayer.Mist, FormationLayer.Dust, FormationLayer.Breath, FormationLayer.Man],
    };

    /// <summary>The shapes of a layer, the farthest first; none for <see cref="FormationLayer.Man"/>.</summary>
    /// <param name="layer">The layer.</param>
    public static IReadOnlyList<PictureShape> ShapesOf(FormationLayer layer)
    {
        lock (Shapes)
        {
            if (!Shapes.TryGetValue(layer, out var shapes))
            {
                var brush = new PictureBrush();
                switch (layer)
                {
                    case FormationLayer.Heavens: Heavens(brush); break;
                    case FormationLayer.Earth: Earth(brush); break;
                    case FormationLayer.Mist: Mist(brush); break;
                    case FormationLayer.Dust: Dust(brush); break;
                    case FormationLayer.Breath: Breath(brush); break;
                }

                Shapes[layer] = shapes = brush.Shapes;
            }

            return shapes;
        }
    }

    private static PaintStop Stop(double at, int colour, double opacity = 1) => new(at, colour, opacity);

    private static double Chance(int index, int salt) => World.GardenScenery.Chance(index, salt, 29);

    private static void Heavens(PictureBrush brush)
    {
        brush.Polygon(Paint.Down(0, Ground, Stop(0, 0x6F93B8), Stop(0.6, 0xC9D8D2), Stop(1, 0xF2E3BE)), 1, 0, 0, Width, 0, Width, Ground + 4, 0, Ground + 4);
        for (var cloud = 0; cloud < 4; cloud++)
        {
            var x = 30 + (cloud * 78) + (Chance(cloud, 1) * 24);
            var y = 22 + (Chance(cloud, 2) * 36);
            brush.Ellipse(x, y, 30 + (Chance(cloud, 3) * 16), 5 + (Chance(cloud, 4) * 3), Paint.Flat(0xFFFFFF), 0.35);
        }
    }

    // The earth before any plant of the field: bare ridges and bare ground, in the red-brown of its dust.
    private static void Earth(PictureBrush brush)
    {
        brush.Curve(Paint.Down(96, 150, Stop(0, 0xB98F6C), Stop(1, 0xA67652)), 1, -20, 150, 20, 118, 70, 108, 120, 122, 170, 112, 230, 104, 290, 116, 340, 150);
        brush.Curve(Paint.Down(110, 160, Stop(0, 0xA87450), Stop(1, 0x94603F)), 1, -20, 170, 40, 128, 110, 134, 180, 126, 250, 132, 340, 124, 340, 170);
        brush.Polygon(Paint.Down(Ground, Height, Stop(0, 0x9C6642), Stop(1, 0x7A4A2E)), 1, 0, Ground + 8, Width, Ground + 8, Width, Height, 0, Height);
        for (var stone = 0; stone < 14; stone++)
        {
            var x = 8 + (Chance(stone, 5) * (Width - 16));
            var y = Ground + 16 + (Chance(stone, 6) * 48);
            brush.Ellipse(x, y, 3 + (Chance(stone, 7) * 5), 1.2 + (Chance(stone, 8) * 1.4), Paint.Flat(0x6B3F27), 0.45);
        }
    }

    // Genesis 2:6: bands of mist over the face of the ground.
    private static void Mist(PictureBrush brush)
    {
        for (var band = 0; band < 7; band++)
        {
            var x = 20 + (band * 46) + (Chance(band, 9) * 20);
            var y = Ground - 4 + (Chance(band, 10) * 44);
            var radius = 44 + (Chance(band, 11) * 30);
            brush.Ellipse(x, y, radius, 7 + (Chance(band, 12) * 5), Paint.Oval(x, y, radius, 9, Stop(0, 0xFFFFFF, 0.7), Stop(1, 0xFFFFFF, 0)));
        }
    }

    // Genesis 2:7: the dust of the ground, in the air about the place where the man stands. Motes, not a body.
    private static void Dust(PictureBrush brush)
    {
        for (var mote = 0; mote < 46; mote++)
        {
            var angle = Chance(mote, 13) * Math.PI * 2;
            var reach = 14 + (Chance(mote, 14) * 54);
            var x = ManX + (Math.Cos(angle) * reach);
            var y = ManY - 30 + (Math.Sin(angle) * reach * 0.62);
            brush.Ellipse(x, y, 0.9 + (Chance(mote, 15) * 1.7), 0.9 + (Chance(mote, 16) * 1.3), Paint.Flat(Chance(mote, 17) < 0.5 ? 0x9A5B38 : 0xB9774C), 0.75);
        }
    }

    // Genesis 2:7: the breath of life, as a warm light about the man. A light, never a figure.
    private static void Breath(PictureBrush brush)
    {
        brush.Ellipse(ManX, ManY - 34, 92, 78, Paint.Oval(ManX, ManY - 34, 92, 78, Stop(0, 0xFFF6D8, 0.95), Stop(0.45, 0xFFE7A8, 0.5), Stop(1, 0xFFE7A8, 0)));
        brush.Ellipse(ManX, ManY + 1, 30, 5, Paint.Oval(ManX, ManY + 1, 30, 5, Stop(0, 0x5A3320, 0.4), Stop(1, 0x5A3320, 0)));
    }
}
