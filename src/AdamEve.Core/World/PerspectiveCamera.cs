namespace AdamEve.Core.World;

/// <summary>
/// The camera of the garden since decision D18 (design, sections 6 and 12): a perspective camera that follows the
/// player. Its direction and its tilt are fixed: it looks north and down, it cannot be turned, and it cannot be
/// zoomed. It is the one projection model of the game: the rules ask it what the play area shows (culling), which
/// point of the ground lies under a tap, where a point of the garden lies on the screen and at which scale a
/// figure stands there, and the renderer builds its own camera from the very numbers of this one (the eye, the
/// tilt, the field of view), which the render list carries.
/// <para>
/// The garden in space: x grows eastward and y southward on the ground, both in logical pixels of the map, and a
/// height grows upward from the ground. The screen: pixels of the play area from its left and top edges.
/// </para>
/// </summary>
/// <param name="ViewWidth">The width of the play area.</param>
/// <param name="ViewHeight">The height of the play area.</param>
/// <param name="FocusX">The point of the ground in the middle of the play area.</param>
/// <param name="FocusY">The point of the ground in the middle of the play area.</param>
/// <param name="Distance">How far the eye is from that point.</param>
public readonly record struct PerspectiveCamera(double ViewWidth, double ViewHeight, double FocusX, double FocusY, double Distance)
{
    /// <summary>How far the camera looks down from the horizontal, in degrees. Fixed.</summary>
    public const double TiltDegrees = 42;

    /// <summary>The field of view from the top to the bottom of the play area, in degrees. Fixed.</summary>
    public const double FieldOfViewDegrees = 40;

    /// <summary>
    /// Where the haze closes, as a share of the height of the play area from its top: above this line of the
    /// screen the ground is lost in haze, and the far layer (hills and sky) stands there.
    /// </summary>
    public const double HazeLineShare = 0.16;

    /// <summary>The haze begins at this many times the distance of the player.</summary>
    public const double HazeStartFactor = 1.12;

    /// <summary>
    /// A figure is a flat cut-out that faces the camera; for hiding and being hidden by scenery it has one depth,
    /// that of the point this high above its feet: the top of a figure that stands upright.
    /// </summary>
    public const double FigureDepthHeight = 56;

    private static readonly double Sin = Math.Sin(TiltDegrees * Math.PI / 180);
    private static readonly double Cos = Math.Cos(TiltDegrees * Math.PI / 180);
    private static readonly double HalfViewTangent = Math.Tan(FieldOfViewDegrees * Math.PI / 360);

    /// <summary>The tilt in radians, as the render list carries it.</summary>
    public static double TiltRadians => TiltDegrees * Math.PI / 180;

    /// <summary>The field of view in radians, as the render list carries it.</summary>
    public static double FieldOfViewRadians => FieldOfViewDegrees * Math.PI / 180;

    /// <summary>The eye: its place over the ground.</summary>
    public double EyeX => FocusX;

    /// <summary>The eye: its place over the ground, south of what it looks at.</summary>
    public double EyeY => FocusY + (Distance * Cos);

    /// <summary>The eye: its height above the ground.</summary>
    public double EyeHeight => Distance * Sin;

    /// <summary>Pixels of the play area for one unit across the view at a depth of one unit.</summary>
    public double FocalLength => ViewHeight / 2 / HalfViewTangent;

    /// <summary>
    /// Play-area pixels for one logical pixel where the player stands: the scale of a figure there, and of the
    /// ground along the player's row.
    /// </summary>
    public double Scale => FocalLength / Distance;

    /// <summary>The depth at which the haze begins.</summary>
    public double HazeStart => Distance * HazeStartFactor;

    /// <summary>The line of the screen above which the ground is lost in haze, in pixels from the top.</summary>
    public double HazeLine => ViewHeight * HazeLineShare;

    /// <summary>The depth at which the haze closes: the depth of the ground under <see cref="HazeLine"/>.</summary>
    public double HazeEnd
    {
        get
        {
            var ground = GroundAt(ViewWidth / 2, HazeLine);
            return ground is null ? Distance * 4 : DepthOf(ground.Value.X, ground.Value.Y);
        }
    }

    /// <summary>
    /// The camera that follows a point of the map. Along the player's row the play area shows what the design asks
    /// (section 6): about 15 tiles across in landscape and 9 in portrait. Nearer rows are larger and farther rows
    /// smaller. At the edges of the map the camera stops: the left and right edges of the play area on the player's
    /// row and the ground under the bottom edge stay on the map. To the north it does not stop before the edge
    /// itself: the haze closes before the picture ends, what lies beyond the map there is thicket, and the player
    /// is never pushed up into the haze.
    /// </summary>
    /// <param name="viewWidth">The width of the play area.</param>
    /// <param name="viewHeight">The height of the play area.</param>
    /// <param name="focusX">The logical pixel to look at.</param>
    /// <param name="focusY">The logical pixel to look at.</param>
    /// <param name="map">The map.</param>
    public static PerspectiveCamera Follow(double viewWidth, double viewHeight, double focusX, double focusY, TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        viewWidth = Math.Max(1, viewWidth);
        viewHeight = Math.Max(1, viewHeight);
        var tilePixels = Math.Max(Math.Min(viewWidth, viewHeight) / Camera.ShortSideTiles, Math.Max(viewWidth, viewHeight) / Camera.LongSideTiles);
        var scale = tilePixels / map.TileSize;
        var distance = viewHeight / 2 / HalfViewTangent / scale;
        var free = new PerspectiveCamera(viewWidth, viewHeight, focusX, focusY, distance);
        var halfWidth = viewWidth / scale / 2;
        var south = (free.GroundAt(viewWidth / 2, viewHeight)?.Y ?? focusY) - focusY;
        return free with
        {
            FocusX = Clamp(focusX, halfWidth, (map.Width * map.TileSize) - halfWidth),
            FocusY = Clamp(focusY, 0, (map.Height * map.TileSize) - south),
        };
    }

    /// <summary>How far a point lies from the eye along the line of sight of the camera.</summary>
    /// <param name="x">The point on the ground.</param>
    /// <param name="y">The point on the ground.</param>
    /// <param name="height">Its height above the ground.</param>
    public double DepthOf(double x, double y, double height = 0) => ((EyeHeight - height) * Sin) + ((EyeY - y) * Cos);

    /// <summary>Where a point of the garden lies on the screen.</summary>
    /// <param name="x">The point on the ground.</param>
    /// <param name="y">The point on the ground.</param>
    /// <param name="height">Its height above the ground.</param>
    /// <returns>Pixels from the left and from the top edge of the play area.</returns>
    public (double X, double Y) Project(double x, double y, double height = 0)
    {
        var depth = DepthOf(x, y, height);
        var up = ((height - EyeHeight) * Cos) + ((EyeY - y) * Sin);
        return ((ViewWidth / 2) + (FocalLength * (x - EyeX) / depth), (ViewHeight / 2) - (FocalLength * up / depth));
    }

    /// <summary>
    /// Play-area pixels for one logical pixel of a figure whose feet stand on a point of the ground. A figure is a
    /// flat cut-out in a plane that faces the camera squarely, so one scale holds for the whole of it: on the
    /// screen it is the figure the modesty check judged, larger or smaller and nothing else.
    /// </summary>
    /// <param name="x">The feet.</param>
    /// <param name="y">The feet.</param>
    public double ScaleAt(double x, double y) => FocalLength / DepthOf(x, y);

    /// <summary>
    /// A point of the plane of a figure, in space: the plane stands on the feet and faces the camera squarely (it
    /// leans back by the tilt of the camera).
    /// </summary>
    /// <param name="feetX">The feet.</param>
    /// <param name="feetY">The feet.</param>
    /// <param name="across">How far the point lies to the right of the feet, in the figure's own logical pixels.</param>
    /// <param name="up">How far the point lies above the feet, in the figure's own logical pixels.</param>
    /// <returns>The point on the ground, and its height.</returns>
    public static (double X, double Y, double Height) FigurePoint(double feetX, double feetY, double across, double up) =>
        (feetX + across, feetY - (up * Sin), up * Cos);

    /// <summary>The point of the ground under a point of the screen; null where the screen shows no ground.</summary>
    /// <param name="screenX">Pixels from the left edge of the play area.</param>
    /// <param name="screenY">Pixels from the top edge of the play area.</param>
    public (double X, double Y)? GroundAt(double screenX, double screenY)
    {
        // The ray from the eye through that point of the screen, followed down to the ground.
        var across = screenX - (ViewWidth / 2);
        var up = (ViewHeight / 2) - screenY;
        var falls = (FocalLength * Sin) - (up * Cos);
        if (falls <= 1e-9)
        {
            return null;
        }

        var steps = EyeHeight / falls;
        return (EyeX + (steps * across), EyeY - (steps * ((FocalLength * Cos) + (up * Sin))));
    }

    /// <summary>The tile under a point of the play area; null outside the play area and outside the map.</summary>
    /// <param name="screenX">Pixels from the left edge of the play area.</param>
    /// <param name="screenY">Pixels from the top edge of the play area.</param>
    /// <param name="map">The map.</param>
    public TilePos? TileAt(double screenX, double screenY, TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (screenX < 0 || screenY < 0 || screenX > ViewWidth || screenY > ViewHeight || GroundAt(screenX, screenY) is not { } ground)
        {
            return null;
        }

        var tile = new TilePos((int)Math.Floor(ground.X / map.TileSize), (int)Math.Floor(ground.Y / map.TileSize));
        return map.Contains(tile) ? tile : null;
    }

    /// <summary>
    /// The rows of the map that can show something that stands on them: from the line where the haze closes (and a
    /// few rows beyond it, whose trees still rise out of the haze) to the bottom edge of the play area (and a few
    /// rows nearer, whose trees still reach into it).
    /// </summary>
    /// <param name="map">The map.</param>
    public (int First, int Last) VisibleRows(TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var far = GroundAt(ViewWidth / 2, HazeLine)?.Y ?? 0;
        var near = GroundAt(ViewWidth / 2, ViewHeight)?.Y ?? (map.Height * map.TileSize);
        return (
            Math.Max(0, (int)Math.Floor(far / map.TileSize) - 3),
            Math.Min(map.Height - 1, (int)Math.Floor(near / map.TileSize) + 3));
    }

    /// <summary>
    /// The columns of a row that the play area can show: wider for a far row than for a near one, with a tile to
    /// spare on each side for the crown of a tree.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="map">The map.</param>
    public (int First, int Last) VisibleColumns(int row, TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var half = ViewWidth / 2 * Math.Max(0, DepthOf(EyeX, row * map.TileSize)) / FocalLength;
        return (
            Math.Max(0, (int)Math.Floor((EyeX - half) / map.TileSize) - 1),
            Math.Min(map.Width - 1, (int)Math.Floor((EyeX + half) / map.TileSize) + 1));
    }

    // A map smaller than the play area is centred; otherwise the camera stops at the map's edge.
    private static double Clamp(double focus, double least, double most) => most < least ? (least + most) / 2 : Math.Clamp(focus, least, most);
}
