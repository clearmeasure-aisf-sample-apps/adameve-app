using AdamEve.Core.Input;
using AdamEve.Core.Rigs;
using AdamEve.Core.Saves;
using AdamEve.Core.Story;
using AdamEve.Core.World;

namespace AdamEve.Core.Game;

/// <summary>What a frame changed that the page outside the canvas must know.</summary>
[Flags]
public enum FrameEvents
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>The player pressed the menu key.</summary>
    Menu = 1,

    /// <summary>The player walked into another region.</summary>
    RegionChanged = 2,

    /// <summary>The player arrived on a tile and stays: the position is to be saved.</summary>
    Stopped = 4,

    /// <summary>The player pressed the action key (Space or Enter) outside a control of the page.</summary>
    Action = 8,
}

/// <summary>
/// Slice S2, "Walk the garden": the player's character walks the map, the other stands in the glade, and every
/// frame is composed into the render list with its camera and its M1 verdict. All of it runs here, without a
/// browser: the client only carries the numbers in and out. The frame is composed for the projection the renderer
/// asks for: the perspective camera (decision D18), or the flat camera of the fallback renderer.
/// </summary>
public sealed class GardenGame
{
    private const int SpriteReserve = 96;
    private const double FeetBelowCentre = 8;
    private const double SpriteBaseAboveTileBottom = 6;

    /// <summary>Where the anchor of a character lies that is not in the garden: far outside every map.</summary>
    public const double Nowhere = -100000;

    private readonly TileMap map;
    private readonly GardenScenery scenery;
    private readonly Actor[] actors;
    private readonly Actor[] drawOrder;
    private readonly RigAnimation idle;
    private readonly RigAnimation walk;
    private readonly ConcealmentChecker checker = new();
    private readonly GameLoop loop = new();
    private readonly Walker walker;
    private readonly int player;
    private readonly int zonesPerRig;
    private readonly SaveGame resumed;
    private readonly bool[] sound;
    private Camera flatCamera;
    private PerspectiveCamera camera;
    private bool flat;
    private int pendingPressed;
    private bool pendingTapped;
    private double pendingTapX;
    private double pendingTapY;
    private double ambientSeconds;
    private double walkSeconds;
    private bool otherPresent = true;

    /// <summary>Starts the garden.</summary>
    /// <param name="map">The map.</param>
    /// <param name="adam">The rig of Adam.</param>
    /// <param name="woman">The rig of the woman.</param>
    /// <param name="animations">The animations: "idle" and "walk".</param>
    /// <param name="save">The saved game to resume, or null for a new game.</param>
    public GardenGame(TileMap map, Rig adam, Rig woman, IReadOnlyList<RigAnimation> animations, SaveGame? save)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(adam);
        ArgumentNullException.ThrowIfNull(woman);
        ArgumentNullException.ThrowIfNull(animations);
        this.map = map;
        scenery = new GardenScenery(map);
        resumed = save ?? new SaveGame();
        idle = animations.First(animation => animation.Id == "idle");
        walk = animations.First(animation => animation.Id == "walk");
        Atlas = new AtlasCatalog([adam, woman]);
        actors = [new Actor(new RigPose(adam), 0, map.Spawn("adam")), new Actor(new RigPose(woman), 1, map.Spawn("woman"))];
        drawOrder = [actors[0], actors[1]];

        Character = save?.Character ?? PlayerCharacter.Adam;
        player = Character == PlayerCharacter.Adam ? 0 : 1;
        var other = actors[1 - player];
        var start = save is null ? actors[player].Tile : new TilePos(save.TileX, save.TileY);
        if (!map.IsWalkable(start) || start == other.Tile)
        {
            start = actors[player].Tile;
        }

        walker = new Walker(map, start, save?.Facing ?? Facing.S) { Obstacle = other.Tile };
        RegionId = map.RegionAt(start)?.Id;

        // A rig that holds what the amended rule does not allow (RigStructure) fails every frame, whatever it shows.
        sound = [RigStructure.Violations(adam).Count == 0, RigStructure.Violations(woman).Count == 0];
        zonesPerRig = Math.Max(1, Math.Max(adam.Zones.Count, woman.Zones.Count));
        var names = new List<string> { StillFigure.Ok };
        foreach (var actor in actors)
        {
            var rig = actor.Pose.Rig;
            for (var zone = 0; zone < zonesPerRig; zone++)
            {
                names.Add($"fail:{rig.Id}:{(zone < rig.Zones.Count ? rig.Zones[zone].Id : "none")}");
            }

            names.Add($"fail:{rig.Id}:{StillFigure.Structure}");
        }

        VerdictNames = names;
    }

    /// <summary>The planting of the garden: what stands on each tile, and where the flowers lie.</summary>
    public GardenScenery Scenery => scenery;

    /// <summary>Every image a frame may name.</summary>
    public AtlasCatalog Atlas { get; }

    /// <summary>
    /// The M1 verdicts by the number the render list carries: "ok", then for each rig "fail:&lt;rig&gt;:&lt;zone&gt;"
    /// for each zone and "fail:&lt;rig&gt;:structure".
    /// </summary>
    public IReadOnlyList<string> VerdictNames { get; }

    /// <summary>Whom the player plays.</summary>
    public PlayerCharacter Character { get; }

    /// <summary>The covering variant of both characters. Slice S2 plays before Genesis 3:7.</summary>
    public Covering Covering { get; init; } = Covering.None;

    /// <summary>
    /// Whether the character the player does not play is in the garden. On the man's path the woman is not made
    /// before Genesis 2:22: she is then not drawn, not judged and in nobody's way.
    /// </summary>
    public bool OtherPresent
    {
        get => otherPresent;
        set
        {
            otherPresent = value;
            walker.Obstacle = value ? actors[1 - player].Tile : null;
        }
    }

    /// <summary>The things of the garden that are not scenery and not a person: animals, the sapling, the branch.</summary>
    public GardenThings Things { get; } = new();

    /// <summary>A card, a choice or the menu is open: the world waits, the ambient animation goes on.</summary>
    public bool Paused { get; set; }

    /// <summary>The tile the player stands on, or last left.</summary>
    public TilePos PlayerTile => walker.Tile;

    /// <summary>The way the player faces.</summary>
    public Facing PlayerFacing => walker.Facing;

    /// <summary>Whether the player is between two tiles.</summary>
    public bool Moving => walker.Moving;

    /// <summary>The region the player is in, or null.</summary>
    public string? RegionId { get; private set; }

    /// <summary>The M1 verdict of the last frame: "ok", "fail:&lt;rig&gt;:&lt;zone&gt;" or "fail:&lt;rig&gt;:structure".</summary>
    public string Concealment { get; private set; } = "ok";

    /// <summary>How many frames so far had a verdict other than "ok".</summary>
    public long ExposedFrames { get; private set; }

    /// <summary>The game as it is saved: where the player stands, and the story as the resumed game held it.</summary>
    public SaveGame ToSave() => resumed with { Character = Character, TileX = walker.Tile.X, TileY = walker.Tile.Y, Facing = walker.Facing };

    /// <summary>The game as it is saved: where the player stands, and the story as it stands now.</summary>
    /// <param name="story">The state of the story.</param>
    public SaveGame ToSave(StoryState story) => SaveGame.Of(story, walker.Tile) with { Facing = walker.Facing };

    /// <summary>
    /// One frame: reads the input block, advances the world in fixed steps, and writes the render list with the
    /// camera and the M1 verdict of exactly what is drawn.
    /// </summary>
    /// <param name="nowMilliseconds">The time of the frame, as the browser gives it.</param>
    /// <param name="input">The input block (<see cref="InputBlock"/>).</param>
    /// <param name="list">The render list (<see cref="RenderList"/>).</param>
    /// <returns>What changed that the page outside the canvas must know.</returns>
    public FrameEvents Frame(double nowMilliseconds, ReadOnlySpan<double> input, Span<double> list)
    {
        var events = FrameEvents.None;
        var steps = loop.Advance(nowMilliseconds);
        if (input[InputBlock.Still] == 0)
        {
            // The stance of a standing figure breathes, unless the player asks for reduced motion.
            ambientSeconds += loop.FrameSeconds;
        }

        if (input[InputBlock.Menu] != 0)
        {
            events |= FrameEvents.Menu;
        }

        if (input[InputBlock.Action] != 0)
        {
            events |= FrameEvents.Action;
        }

        Things.Advance(loop.FrameSeconds);

        if (Paused)
        {
            pendingPressed = 0;
            pendingTapped = false;
        }
        else
        {
            pendingPressed |= (int)input[InputBlock.Pressed];
            if (input[InputBlock.Tapped] != 0)
            {
                pendingTapped = true;
                pendingTapX = input[InputBlock.TapX];
                pendingTapY = input[InputBlock.TapY];
            }

            var tileBefore = walker.Tile;
            for (var step = 0; step < steps; step++)
            {
                // A direction pressed while the character is between two tiles waits for the next tile: two quick
                // taps on the D-pad are two tiles.
                var bits = (int)input[InputBlock.Held] | pendingPressed;
                // The tile under the tap, through the camera of the frame the player saw.
                var tap = !pendingTapped ? null : flat ? flatCamera.TileAt(pendingTapX, pendingTapY, map) : camera.TileAt(pendingTapX, pendingTapY, map);
                pendingTapped = false;
                var wasMoving = walker.Moving;
                var begun = walker.StepsBegun;
                var snapshot = new InputSnapshot(InputBlock.Direction(bits), input[InputBlock.Action] != 0, false, tap);
                if (walker.Step(snapshot, GameLoop.StepSeconds))
                {
                    events |= FrameEvents.Stopped;
                }

                if (!wasMoving || walker.StepsBegun != begun)
                {
                    pendingPressed = 0;
                }

                if (walker.Moving)
                {
                    walkSeconds += GameLoop.StepSeconds;
                }
            }

            if (walker.Tile != tileBefore)
            {
                var region = map.RegionAt(walker.Tile)?.Id;
                if (region != RegionId)
                {
                    RegionId = region;
                    events |= FrameEvents.RegionChanged;
                }
            }
        }

        Compose(input, list);
        return events;
    }

    private void Compose(ReadOnlySpan<double> input, Span<double> list)
    {
        var alpha = Paused ? 1 : loop.Alpha;
        var me = actors[player];
        me.Tile = walker.Tile;
        me.X = walker.PreviousX + ((walker.X - walker.PreviousX) * alpha);
        me.Y = walker.PreviousY + ((walker.Y - walker.PreviousY) * alpha);
        me.Pose.Sample(walker.Moving ? walk : idle, walker.Moving ? walkSeconds : ambientSeconds, walker.Facing, Covering);
        var other = actors[1 - player];
        other.Pose.Sample(idle, ambientSeconds + 0.7, Facing.S, Covering);

        var size = map.TileSize;
        var viewWidth = input[InputBlock.ViewWidth] > 0 ? input[InputBlock.ViewWidth] : 360;
        var viewHeight = input[InputBlock.ViewHeight] > 0 ? input[InputBlock.ViewHeight] : 640;
        var focusX = (me.X + 0.5) * size;
        var focusY = (me.Y + 0.5) * size;
        flat = (int)input[InputBlock.Projection] == InputBlock.Flat;
        flatCamera = Camera.Follow(viewWidth, viewHeight, focusX, focusY, map);
        camera = PerspectiveCamera.Follow(viewWidth, viewHeight, focusX, focusY, map);
        var pixelRatio = Math.Clamp(input[InputBlock.PixelRatio], 1, 2);

        // The verdict of exactly what is drawn: each figure at the scale it has on the screen, which under the
        // perspective camera is the scale of the place it stands on.
        var verdict = 0;
        foreach (var actor in actors)
        {
            if (Absent(actor))
            {
                actor.ExposedZone = -1;
                continue;
            }

            // A figure far outside the picture (beside or behind the eye) is not seen; it is judged all the same,
            // at a scale a figure can be seen at.
            var scale = flat ? flatCamera.Scale : camera.ScaleAt((actor.X + 0.5) * size, FeetY(actor));
            scale = scale > 0 ? Math.Clamp(scale, camera.Scale / 4, camera.Scale * 4) : camera.Scale;
            actor.ExposedZone = sound[actor.RigIndex] ? checker.FirstExposedZone(actor.Pose, scale * pixelRatio) : zonesPerRig;
            if (actor.ExposedZone >= 0 && verdict == 0)
            {
                verdict = 1 + (actor.RigIndex * (zonesPerRig + 1)) + actor.ExposedZone;
            }
        }

        if (verdict != 0)
        {
            ExposedFrames++;
        }

        Concealment = VerdictNames[verdict];

        // Sprites and characters by depth: what stands on the tiles row by row from the north, each character before the first row
        // whose trees stand nearer the viewer than its feet. The canvas draws in this order; a renderer with depth
        // keeps the order only among the parts of one character.
        drawOrder[0] = actors[0].Y <= actors[1].Y ? actors[0] : actors[1];
        drawOrder[1] = drawOrder[0] == actors[0] ? actors[1] : actors[0];
        var count = 0;
        var nextActor = 0;
        var (firstRow, lastRow) = flat
            ? (Math.Max(0, (int)Math.Floor(flatCamera.Y / size) - 1), Math.Min(map.Height - 1, (int)Math.Floor((flatCamera.Y + (viewHeight / flatCamera.Scale)) / size) + 3))
            : camera.VisibleRows(map);
        for (var row = firstRow; row <= lastRow; row++)
        {
            var baseline = ((row + 1) * size) - SpriteBaseAboveTileBottom;
            while (nextActor < drawOrder.Length && FeetY(drawOrder[nextActor]) <= baseline)
            {
                WriteFigure(drawOrder[nextActor++], list, ref count);
            }

            var (firstColumn, lastColumn) = flat
                ? (Math.Max(0, (int)Math.Floor(flatCamera.X / size) - 1), Math.Min(map.Width - 1, (int)Math.Floor((flatCamera.X + (viewWidth / flatCamera.Scale)) / size) + 1))
                : camera.VisibleColumns(row, map);
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                var sprite = Atlas.SpriteId(scenery.KindAt(new TilePos(column, row)));
                if (sprite >= 0 && count < RenderList.Capacity - SpriteReserve)
                {
                    Write(list, ref count, sprite, Affine.Translation((column + 0.5) * size, baseline), 0);
                }
            }
        }

        while (nextActor < drawOrder.Length)
        {
            WriteFigure(drawOrder[nextActor++], list, ref count);
        }

        list[RenderList.Count] = count;
        list[RenderList.CameraX] = flatCamera.X;
        list[RenderList.CameraY] = flatCamera.Y;
        list[RenderList.Scale] = flat ? flatCamera.Scale : camera.Scale;
        list[RenderList.PlayerTileX] = walker.Tile.X;
        list[RenderList.PlayerTileY] = walker.Tile.Y;
        list[RenderList.Concealment] = verdict;
        list[RenderList.Facing] = (int)walker.Facing;
        list[RenderList.ExposedFrames] = ExposedFrames;
        list[RenderList.Moving] = walker.Moving ? 1 : 0;
        foreach (var actor in actors)
        {
            // Who is not in the garden has no place in it: the anchor lies far outside the map.
            list[RenderList.Anchors + (actor.RigIndex * 2)] = Absent(actor) ? Nowhere : (actor.X + 0.5) * size;
            list[RenderList.Anchors + (actor.RigIndex * 2) + 1] = Absent(actor) ? Nowhere : FeetY(actor);
        }

        Things.Write(list, size, FeetBelowCentre, ambientSeconds);

        list[RenderList.Projection] = flat ? InputBlock.Flat : InputBlock.Perspective;
        list[RenderList.EyeX] = camera.EyeX;
        list[RenderList.EyeHeight] = camera.EyeHeight;
        list[RenderList.EyeY] = camera.EyeY;
        list[RenderList.Tilt] = PerspectiveCamera.TiltRadians;
        list[RenderList.FieldOfView] = PerspectiveCamera.FieldOfViewRadians;
        list[RenderList.HazeStart] = camera.HazeStart;
        list[RenderList.HazeEnd] = camera.HazeEnd;
        list[RenderList.FigureDepthHeight] = PerspectiveCamera.FigureDepthHeight;
    }

    private bool Absent(Actor actor) => !otherPresent && actor != actors[player];

    private double FeetY(Actor actor) => ((actor.Y + 0.5) * map.TileSize) + FeetBelowCentre;

    // A figure whose frame did not pass is not drawn at all (fail closed): the frame is counted, and the page says so.
    private void WriteFigure(Actor actor, Span<double> list, ref int count)
    {
        if (actor.ExposedZone >= 0 || Absent(actor))
        {
            return;
        }

        var origin = Affine.Translation((actor.X + 0.5) * map.TileSize, FeetY(actor));
        foreach (var placed in actor.Pose.Parts)
        {
            Write(list, ref count, Atlas.PartId(actor.RigIndex, placed.PartIndex), origin.Then(placed.Transform), actor.RigIndex + 1);
        }
    }

    private static void Write(Span<double> list, ref int count, int atlasId, in Affine transform, int character)
    {
        if (count >= RenderList.Capacity)
        {
            return;
        }

        var entry = list.Slice(RenderList.HeaderLength + (count * RenderList.EntryLength), RenderList.EntryLength);
        entry[RenderList.AtlasId] = atlasId;
        entry[RenderList.Transform] = transform.A;
        entry[RenderList.Transform + 1] = transform.B;
        entry[RenderList.Transform + 2] = transform.C;
        entry[RenderList.Transform + 3] = transform.D;
        entry[RenderList.Transform + 4] = transform.E;
        entry[RenderList.Transform + 5] = transform.F;
        entry[RenderList.Character] = character;
        count++;
    }

    private sealed class Actor(RigPose pose, int rigIndex, TilePos tile)
    {
        public RigPose Pose { get; } = pose;

        public int RigIndex { get; } = rigIndex;

        public TilePos Tile { get; set; } = tile;

        public double X { get; set; } = tile.X;

        public double Y { get; set; } = tile.Y;

        public int ExposedZone { get; set; } = -1;
    }
}
