namespace AdamEve.Core.World;

/// <summary>
/// The flat camera of the fallback renderer (the canvas, where WebGL is not to be had): about 15 by 9 tiles in
/// landscape and 9 tiles across in portrait (design, section 6), centred on the player and kept inside the map.
/// The garden is drawn in perspective otherwise (<see cref="PerspectiveCamera"/>).
/// </summary>
/// <param name="Scale">Play-area pixels for one logical pixel.</param>
/// <param name="X">The logical pixel of the map at the left edge of the play area.</param>
/// <param name="Y">The logical pixel of the map at the top edge of the play area.</param>
/// <param name="ViewWidth">The width of the play area.</param>
/// <param name="ViewHeight">The height of the play area.</param>
public readonly record struct Camera(double Scale, double X, double Y, double ViewWidth, double ViewHeight)
{
    /// <summary>The tiles across the long side of the play area.</summary>
    public const double LongSideTiles = 15;

    /// <summary>The tiles across the short side of the play area, at least.</summary>
    public const double ShortSideTiles = 9;

    /// <summary>The camera that follows a point of the map.</summary>
    /// <param name="viewWidth">The width of the play area.</param>
    /// <param name="viewHeight">The height of the play area.</param>
    /// <param name="focusX">The logical pixel to centre on.</param>
    /// <param name="focusY">The logical pixel to centre on.</param>
    /// <param name="map">The map.</param>
    public static Camera Follow(double viewWidth, double viewHeight, double focusX, double focusY, TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        viewWidth = Math.Max(1, viewWidth);
        viewHeight = Math.Max(1, viewHeight);
        var tilePixels = Math.Max(Math.Min(viewWidth, viewHeight) / ShortSideTiles, Math.Max(viewWidth, viewHeight) / LongSideTiles);
        var scale = tilePixels / map.TileSize;
        return new Camera(
            scale,
            Clamp(focusX - (viewWidth / scale / 2), (map.Width * map.TileSize) - (viewWidth / scale)),
            Clamp(focusY - (viewHeight / scale / 2), (map.Height * map.TileSize) - (viewHeight / scale)),
            viewWidth,
            viewHeight);
    }

    /// <summary>The tile under a point of the play area; null outside the map.</summary>
    /// <param name="screenX">Pixels from the left edge of the play area.</param>
    /// <param name="screenY">Pixels from the top edge of the play area.</param>
    /// <param name="map">The map.</param>
    public TilePos? TileAt(double screenX, double screenY, TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (Scale <= 0 || screenX < 0 || screenY < 0 || screenX > ViewWidth || screenY > ViewHeight)
        {
            return null;
        }

        var tile = new TilePos(
            (int)Math.Floor((X + (screenX / Scale)) / map.TileSize),
            (int)Math.Floor((Y + (screenY / Scale)) / map.TileSize));
        return map.Contains(tile) ? tile : null;
    }

    // A map smaller than the play area is centred; otherwise the camera stops at the map's edge.
    private static double Clamp(double origin, double most) => most < 0 ? most / 2 : Math.Clamp(origin, 0, most);
}
