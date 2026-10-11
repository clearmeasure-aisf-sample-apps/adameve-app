namespace AdamEve.Core.Game;

/// <summary>
/// A thing of the garden that is not scenery and not a person: an animal, the sapling, the fallen branch. It
/// stands on the ground at a place, may walk to another, and blocks nobody's way.
/// </summary>
public sealed class GardenThing
{
    /// <summary>Makes a thing that stands at a place.</summary>
    /// <param name="key">What the thing is, for whoever placed it: it tells the things apart.</param>
    /// <param name="art">The picture (<see cref="ThingArt.Images"/>).</param>
    /// <param name="x">The column of its place; a whole number is the middle of a tile.</param>
    /// <param name="y">The row of its place.</param>
    public GardenThing(string key, int art, double x, double y)
    {
        Key = key;
        Art = art;
        X = TargetX = x;
        Y = TargetY = y;
    }

    /// <summary>What the thing is, for whoever placed it.</summary>
    public string Key { get; }

    /// <summary>The picture.</summary>
    public int Art { get; set; }

    /// <summary>The column it stands at now.</summary>
    public double X { get; private set; }

    /// <summary>The row it stands at now.</summary>
    public double Y { get; private set; }

    /// <summary>The column it walks to.</summary>
    public double TargetX { get; set; }

    /// <summary>The row it walks to.</summary>
    public double TargetY { get; set; }

    /// <summary>Whether it looks west; otherwise east, as its picture is drawn.</summary>
    public bool LooksWest { get; set; }

    /// <summary>Whether it is between two places.</summary>
    public bool Moving => X != TargetX || Y != TargetY;

    internal void Advance(double seconds)
    {
        var dx = TargetX - X;
        var dy = TargetY - Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        var step = GardenThings.TilesPerSecond * seconds;
        if (distance <= step)
        {
            X = TargetX;
            Y = TargetY;
            return;
        }

        if (Math.Abs(dx) > 0.05)
        {
            LooksWest = dx < 0;
        }

        X += dx / distance * step;
        Y += dy / distance * step;
    }
}

/// <summary>
/// The things of the garden of one game, and how a frame lists them: after the entries of the render list, in a
/// part of its own (<see cref="RenderList.Things"/>), the farthest first. A renderer that knows nothing of them
/// draws the garden as before.
/// </summary>
public sealed class GardenThings
{
    /// <summary>How fast a thing walks.</summary>
    public const double TilesPerSecond = 5;

    private readonly List<GardenThing> items = [];

    /// <summary>The things, in the order they were placed.</summary>
    public IReadOnlyList<GardenThing> Items => items;

    /// <summary>The thing with a key, or null.</summary>
    /// <param name="key">The key.</param>
    public GardenThing? Find(string key) => items.FirstOrDefault(item => item.Key == key);

    /// <summary>Places a thing. A thing with the same key is replaced.</summary>
    /// <param name="thing">The thing.</param>
    public void Place(GardenThing thing)
    {
        ArgumentNullException.ThrowIfNull(thing);
        Remove(thing.Key);
        items.Add(thing);
    }

    /// <summary>Takes a thing away.</summary>
    /// <param name="key">Its key.</param>
    public void Remove(string key) => items.RemoveAll(item => item.Key == key);

    /// <summary>Takes every thing away.</summary>
    public void Clear() => items.Clear();

    /// <summary>Lets the things that walk go on for a while.</summary>
    /// <param name="seconds">The time.</param>
    public void Advance(double seconds)
    {
        foreach (var item in items)
        {
            item.Advance(seconds);
        }
    }

    /// <summary>
    /// Writes the things into the render list: how many, and for each its picture, the logical pixel of the map
    /// under its foot, the way it looks and how far a step lifts it. The farthest (the most northern) first.
    /// </summary>
    /// <param name="list">The render list.</param>
    /// <param name="tileSize">The size of a tile in logical pixels.</param>
    /// <param name="feetBelowCentre">How far below the middle of its tile a thing stands, as a person does.</param>
    /// <param name="seconds">The clock of what moves by itself.</param>
    public void Write(Span<double> list, int tileSize, double feetBelowCentre, double seconds)
    {
        var count = 0;
        foreach (var item in items.OrderBy(item => item.Y).ThenBy(item => item.X))
        {
            if (count >= RenderList.ThingCapacity)
            {
                break;
            }

            var entry = list.Slice(RenderList.Things + (count * RenderList.EntryLength), RenderList.EntryLength);
            entry[RenderList.ThingArt] = item.Art;
            entry[RenderList.ThingX] = (item.X + 0.5) * tileSize;
            entry[RenderList.ThingY] = ((item.Y + 0.5) * tileSize) + feetBelowCentre;
            entry[RenderList.ThingLooks] = item.LooksWest ? -1 : 1;
            entry[RenderList.ThingLift] = item.Moving ? Math.Abs(Math.Sin(seconds * 11)) * 2.5 : 0;
            count++;
        }

        list[RenderList.ThingCount] = count;
    }
}
