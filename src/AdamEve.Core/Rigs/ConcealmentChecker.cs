namespace AdamEve.Core.Rigs;

/// <summary>
/// The modesty check of rule M1 as decision D18 amended it (design, sections 5.6 and 12), the same for the build's
/// asset check and for every frame of the running game. For each concealment zone of a composed frame it reads the
/// zone's concealment record and requires one of three things:
/// <list type="bullet">
/// <item>covered: every pixel of the zone, inset by one pixel, is covered with alpha 0.95 or more by the parts the
/// record names (hair, apron or coat parts nearer the viewer than the zone): the woman's chest by her hair;</item>
/// <item>plain: the zone is the body's own smooth shape. The body part it lies on covers every pixel of it, and no
/// other part reaches into the zone, except a body part that lies beneath that one or has its very colour: the
/// pelvic region of both figures before Genesis 3:7;</item>
/// <item>turned away: the zone lies on the far side of its body part in the depth order, and that part covers it.</item>
/// </list>
/// A zone without a valid record is not concealed.
/// </summary>
public sealed class ConcealmentChecker
{
    /// <summary>The alpha a zone pixel's cover must reach.</summary>
    public const double RequiredAlpha = 0.95;

    private const int Samples = 4;

    // Two outlines that only touch do not reach into each other.
    private const double Touch = 1e-9;

    // The columns of the zone on each row of pixels: first and last, and a scratch copy for the inset.
    private int[] first = new int[128];
    private int[] last = new int[128];
    private int[] scratchFirst = new int[128];
    private int[] scratchLast = new int[128];
    private Cover[] covers = new Cover[16];
    private double[] polygon = new double[16];

    /// <summary>How many zone pixels the last check judged, over all zones it reached.</summary>
    public int CheckedPixels { get; private set; }

    /// <summary>Judges a composed frame.</summary>
    /// <param name="pose">The frame.</param>
    /// <param name="scale">Device pixels for one logical pixel: 1 for 1x, 2 for 2x.</param>
    /// <param name="insetPixels">
    /// How far each zone is inset before it is judged, in device pixels: 1 by the design. A negative number judges
    /// a zone grown by that many pixels, which a test uses to prove a margin.
    /// </param>
    /// <returns>The index of the first zone that is not concealed; -1 when every zone is.</returns>
    public int FirstExposedZone(RigPose pose, double scale, int insetPixels = 1)
    {
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
        CheckedPixels = 0;
        var rig = pose.Rig;
        for (var index = 0; index < rig.Zones.Count; index++)
        {
            var zone = rig.Zones[index];
            var record = rig.RecordFor(zone, pose.Facing, pose.Covering);
            if (record is null)
            {
                return index;
            }

            if (record.Plain)
            {
                if (!Plain(pose, zone, index, scale, insetPixels))
                {
                    return index;
                }

                continue;
            }

            var depth = zone.DepthIn(pose.View);
            var count = 0;
            foreach (var placed in pose.Parts)
            {
                var part = rig.Parts[placed.PartIndex];
                var covering = record.FacingAway
                    ? string.Equals(part.Id, zone.On, StringComparison.Ordinal)
                    : record.By.Contains(part.Role);
                if (covering && placed.Depth > depth)
                {
                    AddCover(ref count, placed.Transform, part);
                }
            }

            if (count == 0 || !Covered(zone, pose.ZoneTransform(index), count, scale, insetPixels))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Whether a part of a composed frame reaches into a zone: the outline of the part and the polygon of the zone
    /// have a point in common. The polygon of a zone is convex (<see cref="RigStructure"/> refuses another).
    /// </summary>
    /// <param name="pose">The frame.</param>
    /// <param name="zoneIndex">The index of the zone in the rig.</param>
    /// <param name="placed">The part, as the frame placed it.</param>
    public bool ReachesInto(RigPose pose, int zoneIndex, in PlacedPart placed)
    {
        ArgumentNullException.ThrowIfNull(pose);
        var zone = pose.Rig.Zones[zoneIndex];
        var part = pose.Rig.Parts[placed.PartIndex];
        var points = zone.Points.Count / 2;
        if (polygon.Length < zone.Points.Count)
        {
            polygon = new double[zone.Points.Count];
        }

        // The zone in the space of the part, where the part is a rectangle about the origin or the unit circle. A
        // rounded rectangle is taken as the whole rectangle it is cut from: it reaches no farther than that one.
        var toPart = placed.Transform.Invert().Then(pose.ZoneTransform(zoneIndex));
        var unitX = part.Shape == PartShape.Ellipse ? part.Width / 2 : 1;
        var unitY = part.Shape == PartShape.Ellipse ? part.Height / 2 : 1;
        for (var point = 0; point < points; point++)
        {
            polygon[point * 2] = toPart.ApplyX(zone.Points[point * 2], zone.Points[(point * 2) + 1]) / unitX;
            polygon[(point * 2) + 1] = toPart.ApplyY(zone.Points[point * 2], zone.Points[(point * 2) + 1]) / unitY;
        }

        return part.Shape == PartShape.Ellipse
            ? PolygonMeetsUnitCircle(points)
            : PolygonMeetsRectangle(points, part.Width / 2, part.Height / 2);
    }

    private bool Plain(RigPose pose, ConcealmentZone zone, int zoneIndex, double scale, int insetPixels)
    {
        var rig = pose.Rig;
        var carrier = -1;
        for (var index = 0; index < pose.Parts.Length; index++)
        {
            if (string.Equals(rig.Parts[pose.Parts[index].PartIndex].Id, zone.On, StringComparison.Ordinal))
            {
                carrier = index;
            }
        }

        if (carrier < 0)
        {
            return false;
        }

        var on = pose.Parts[carrier];
        var body = rig.Parts[on.PartIndex];
        for (var index = 0; index < pose.Parts.Length; index++)
        {
            if (index == carrier)
            {
                continue;
            }

            var placed = pose.Parts[index];
            var part = rig.Parts[placed.PartIndex];
            var smooth = part.Role == PartRole.Body && (placed.Depth < on.Depth || part.Colour == body.Colour);
            if (!smooth && ReachesInto(pose, zoneIndex, placed))
            {
                return false;
            }
        }

        var count = 0;
        AddCover(ref count, on.Transform, body);
        return Covered(zone, pose.ZoneTransform(zoneIndex), count, scale, insetPixels);
    }

    private bool PolygonMeetsRectangle(int points, double halfWidth, double halfHeight)
    {
        // Two convex shapes are apart exactly when a line along a side of one of them separates them.
        double left = double.PositiveInfinity, right = double.NegativeInfinity, top = double.PositiveInfinity, bottom = double.NegativeInfinity;
        for (var point = 0; point < points; point++)
        {
            left = Math.Min(left, polygon[point * 2]);
            right = Math.Max(right, polygon[point * 2]);
            top = Math.Min(top, polygon[(point * 2) + 1]);
            bottom = Math.Max(bottom, polygon[(point * 2) + 1]);
        }

        if (left >= halfWidth - Touch || right <= Touch - halfWidth || top >= halfHeight - Touch || bottom <= Touch - halfHeight)
        {
            return false;
        }

        for (int current = 0, previous = points - 1; current < points; previous = current++)
        {
            var normalX = polygon[(current * 2) + 1] - polygon[(previous * 2) + 1];
            var normalY = polygon[previous * 2] - polygon[current * 2];
            var length = Math.Sqrt((normalX * normalX) + (normalY * normalY));
            if (length == 0)
            {
                continue;
            }

            double least = double.PositiveInfinity, most = double.NegativeInfinity;
            for (var point = 0; point < points; point++)
            {
                var along = ((polygon[point * 2] * normalX) + (polygon[(point * 2) + 1] * normalY)) / length;
                least = Math.Min(least, along);
                most = Math.Max(most, along);
            }

            var reach = ((Math.Abs(normalX) * halfWidth) + (Math.Abs(normalY) * halfHeight)) / length;
            if (least >= reach - Touch || most <= Touch - reach)
            {
                return false;
            }
        }

        return true;
    }

    private bool PolygonMeetsUnitCircle(int points)
    {
        // The circle and the polygon meet when the centre lies in the polygon or a side comes nearer than the radius.
        var inside = true;
        var sign = 0;
        for (int current = 0, previous = points - 1; current < points; previous = current++)
        {
            double ax = polygon[previous * 2], ay = polygon[(previous * 2) + 1];
            double bx = polygon[current * 2], by = polygon[(current * 2) + 1];
            double edgeX = bx - ax, edgeY = by - ay;
            var cross = (edgeX * -ay) - (edgeY * -ax);
            if (cross != 0)
            {
                if (sign != 0 && Math.Sign(cross) != sign)
                {
                    inside = false;
                }

                sign = Math.Sign(cross);
            }

            var squared = (edgeX * edgeX) + (edgeY * edgeY);
            var along = squared > 0 ? Math.Clamp(-((ax * edgeX) + (ay * edgeY)) / squared, 0, 1) : 0;
            var nearX = ax + (edgeX * along);
            var nearY = ay + (edgeY * along);
            if (Math.Sqrt((nearX * nearX) + (nearY * nearY)) < 1 - Touch)
            {
                return true;
            }
        }

        return inside;
    }

    private void AddCover(ref int count, in Affine transform, RigPart part)
    {
        if (count == covers.Length)
        {
            Array.Resize(ref covers, count * 2);
        }

        covers[count++] = new Cover(transform.Invert(), part.Shape, part.Width / 2, part.Height / 2, part.Round);
    }

    private bool Covered(ConcealmentZone zone, in Affine transform, int coverCount, double scale, int insetPixels)
    {
        var points = zone.Points.Count / 2;
        if (polygon.Length < zone.Points.Count)
        {
            polygon = new double[zone.Points.Count];
        }

        double top = double.PositiveInfinity, bottom = double.NegativeInfinity;
        for (var point = 0; point < points; point++)
        {
            var x = transform.ApplyX(zone.Points[point * 2], zone.Points[(point * 2) + 1]) * scale;
            var y = transform.ApplyY(zone.Points[point * 2], zone.Points[(point * 2) + 1]) * scale;
            polygon[point * 2] = x;
            polygon[(point * 2) + 1] = y;
            top = Math.Min(top, y);
            bottom = Math.Max(bottom, y);
        }

        // The zone as rows of pixels: on each row, the pixels whose centre lies between the outermost edges of
        // the polygon. For a polygon that is not convex this takes more pixels than the polygon has, never fewer.
        var pad = Math.Abs(insetPixels) + 1;
        var originY = (int)Math.Floor(top) - pad;
        var height = (int)Math.Ceiling(bottom) - originY + pad;
        if (first.Length < height)
        {
            first = new int[height];
            last = new int[height];
            scratchFirst = new int[height];
            scratchLast = new int[height];
        }

        for (var row = 0; row < height; row++)
        {
            RowOfPolygon(points, originY + row + 0.5, out first[row], out last[row]);
        }

        for (var pass = 0; pass < Math.Abs(insetPixels); pass++)
        {
            Morph(height, grow: insetPixels < 0);
        }

        for (var row = 0; row < height; row++)
        {
            if (first[row] > last[row])
            {
                continue;
            }

            CheckedPixels += last[row] - first[row] + 1;
            if (!RowCovered(first[row], last[row], originY + row, coverCount, scale))
            {
                return false;
            }
        }

        return true;
    }

    private void RowOfPolygon(int points, double y, out int firstColumn, out int lastColumn)
    {
        double left = double.PositiveInfinity, right = double.NegativeInfinity;
        for (int current = 0, previous = points - 1; current < points; previous = current++)
        {
            var currentY = polygon[(current * 2) + 1];
            var previousY = polygon[(previous * 2) + 1];
            if ((currentY > y) == (previousY > y))
            {
                continue;
            }

            var x = polygon[current * 2] + ((polygon[previous * 2] - polygon[current * 2]) * (y - currentY) / (previousY - currentY));
            left = Math.Min(left, x);
            right = Math.Max(right, x);
        }

        if (left > right)
        {
            firstColumn = 0;
            lastColumn = -1;
            return;
        }

        // The pixels whose centre (column + 0.5) lies in [left, right).
        firstColumn = (int)Math.Ceiling(left - 0.5);
        lastColumn = (int)Math.Ceiling(right - 0.5) - 1;
    }

    // One pixel in or out, by the eight neighbours of each pixel: a pixel stays when it and all eight are of the
    // zone; a pixel joins when one of the nine is.
    private void Morph(int height, bool grow)
    {
        for (var row = 0; row < height; row++)
        {
            var from = grow ? int.MaxValue : int.MinValue;
            var to = grow ? int.MinValue : int.MaxValue;
            var any = false;
            var all = true;
            for (var near = row - 1; near <= row + 1; near++)
            {
                if (near < 0 || near >= height || first[near] > last[near])
                {
                    all = false;
                    continue;
                }

                any = true;
                from = grow ? Math.Min(from, first[near] - 1) : Math.Max(from, first[near] + 1);
                to = grow ? Math.Max(to, last[near] + 1) : Math.Min(to, last[near] - 1);
            }

            var kept = grow ? any : all;
            scratchFirst[row] = kept ? from : 0;
            scratchLast[row] = kept ? to : -1;
        }

        (first, scratchFirst) = (scratchFirst, first);
        (last, scratchLast) = (scratchLast, last);
    }

    private bool RowCovered(int firstColumn, int lastColumn, int pixelY, int coverCount, double scale)
    {
        // Every shape is convex: a run of pixels whose four corners lie in one shape lies in it whole, with alpha 1.
        var left = firstColumn / scale;
        var right = (lastColumn + 1) / scale;
        var top = pixelY / scale;
        var bottom = (pixelY + 1) / scale;
        for (var index = 0; index < coverCount; index++)
        {
            ref readonly var cover = ref covers[index];
            if (cover.Contains(left, top) && cover.Contains(right, top) && cover.Contains(left, bottom) && cover.Contains(right, bottom))
            {
                return true;
            }
        }

        for (var column = firstColumn; column <= lastColumn; column++)
        {
            if (!PixelCovered(column, pixelY, coverCount, scale))
            {
                return false;
            }
        }

        return true;
    }

    private bool PixelCovered(int pixelX, int pixelY, int coverCount, double scale)
    {
        // Every shape is convex: a pixel whose four corners lie in one shape lies in it whole, with alpha 1.
        for (var index = 0; index < coverCount; index++)
        {
            ref readonly var cover = ref covers[index];
            if (cover.Contains(pixelX / scale, pixelY / scale) && cover.Contains((pixelX + 1) / scale, pixelY / scale)
                && cover.Contains(pixelX / scale, (pixelY + 1) / scale) && cover.Contains((pixelX + 1) / scale, (pixelY + 1) / scale))
            {
                return true;
            }
        }

        // Otherwise the alpha of each shape over the pixel is the share of the pixel it covers, and the shapes are
        // composed one over the other as the canvas composes them.
        var uncovered = 1.0;
        for (var index = 0; index < coverCount; index++)
        {
            ref readonly var cover = ref covers[index];
            var inside = 0;
            for (var sampleY = 0; sampleY < Samples; sampleY++)
            {
                for (var sampleX = 0; sampleX < Samples; sampleX++)
                {
                    if (cover.Contains((pixelX + ((sampleX + 0.5) / Samples)) / scale, (pixelY + ((sampleY + 0.5) / Samples)) / scale))
                    {
                        inside++;
                    }
                }
            }

            uncovered *= 1 - ((double)inside / (Samples * Samples));
        }

        return 1 - uncovered >= RequiredAlpha;
    }

    private readonly record struct Cover(Affine Inverse, PartShape Shape, double HalfWidth, double HalfHeight, double Round)
    {
        public bool Contains(double x, double y)
        {
            var localX = Inverse.ApplyX(x, y);
            var localY = Inverse.ApplyY(x, y);
            if (Shape == PartShape.Rectangle)
            {
                return Math.Abs(localX) <= HalfWidth && Math.Abs(localY) <= HalfHeight;
            }

            if (Shape == PartShape.Rounded)
            {
                // Inside the rectangle, and not in the part of a corner the rounding cuts away.
                var beyondX = Math.Abs(localX) - (HalfWidth - Round);
                var beyondY = Math.Abs(localY) - (HalfHeight - Round);
                return Math.Abs(localX) <= HalfWidth && Math.Abs(localY) <= HalfHeight
                    && (beyondX <= 0 || beyondY <= 0 || (beyondX * beyondX) + (beyondY * beyondY) <= Round * Round);
            }

            var unitX = localX / HalfWidth;
            var unitY = localY / HalfHeight;
            return (unitX * unitX) + (unitY * unitY) <= 1;
        }
    }
}
