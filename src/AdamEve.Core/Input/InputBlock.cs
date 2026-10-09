using System.Numerics;

namespace AdamEve.Core.Input;

/// <summary>
/// The small block of numbers the browser fills before each frame (design, section 7.1): which directions are
/// held, which were pressed since the last frame, a tap and the size of the play area. Input events are never sent
/// one by one.
/// </summary>
public static class InputBlock
{
    /// <summary>The directions held now, as bits.</summary>
    public const int Held = 0;

    /// <summary>The directions pressed since the last frame, as bits: a press shorter than a frame still counts.</summary>
    public const int Pressed = 1;

    /// <summary>1 when the action key or button was pressed since the last frame.</summary>
    public const int Action = 2;

    /// <summary>1 when the menu key was pressed since the last frame.</summary>
    public const int Menu = 3;

    /// <summary>1 when the play area was tapped or clicked since the last frame.</summary>
    public const int Tapped = 4;

    /// <summary>The tap, in pixels from the left edge of the play area.</summary>
    public const int TapX = 5;

    /// <summary>The tap, in pixels from the top edge of the play area.</summary>
    public const int TapY = 6;

    /// <summary>The width of the play area in CSS pixels.</summary>
    public const int ViewWidth = 7;

    /// <summary>The height of the play area in CSS pixels.</summary>
    public const int ViewHeight = 8;

    /// <summary>Device pixels for one CSS pixel, capped at 2 by the renderer.</summary>
    public const int PixelRatio = 9;

    /// <summary>The length of the block.</summary>
    public const int Length = 16;

    /// <summary>The bit of "up" (north).</summary>
    public const int Up = 1;

    /// <summary>The bit of "down" (south).</summary>
    public const int Down = 2;

    /// <summary>The bit of "left" (west).</summary>
    public const int Left = 4;

    /// <summary>The bit of "right" (east).</summary>
    public const int Right = 8;

    /// <summary>The direction a set of direction bits asks for. Opposite directions cancel.</summary>
    /// <param name="bits">The bits.</param>
    public static Vector2 Direction(int bits)
    {
        var x = ((bits & Right) != 0 ? 1 : 0) - ((bits & Left) != 0 ? 1 : 0);
        var y = ((bits & Down) != 0 ? 1 : 0) - ((bits & Up) != 0 ? 1 : 0);
        return new Vector2(x, y);
    }
}
