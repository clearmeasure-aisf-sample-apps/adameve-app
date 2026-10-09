using System.Numerics;
using AdamEve.Core.World;

namespace AdamEve.Core.Input;

/// <summary>
/// What the player asks in one step of the simulation, whatever the device: the keyboard, the D-pad and
/// tap-to-move all become this (design, section 7.2).
/// </summary>
/// <param name="Move">The direction to walk: each axis -1, 0 or 1; y grows southward.</param>
/// <param name="Action">Talk, pick up, advance.</param>
/// <param name="Menu">Open or close the menu.</param>
/// <param name="Tap">The tile to walk to, or null.</param>
public readonly record struct InputSnapshot(Vector2 Move, bool Action, bool Menu, TilePos? Tap);
