using AdamEve.Core.Input;
using AdamEve.Core.Rigs;
using AdamEve.Core.Saves;
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
}

/// <summary>
/// Slice S2, "Walk the garden": the player's character walks the map, the other stands in the glade, and every
/// frame is composed into the render list with its M1 verdict. All of it runs here, without a browser: the client
/// only carries the numbers in and out.
/// </summary>
public sealed class GardenGame
{
    private const int SpriteReserve = 96;
    private const double FeetBelowCentre = 8;
    private const double SpriteBaseAboveTileBottom = 6;

    private readonly TileMap map;
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
    private Camera camera;
    private int pendingPressed;
    private bool pendingTapped;
    private double pendingTapX;
    private double pendingTapY;
    private double ambientSeconds;
    private double walkSeconds;

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

        zonesPerRig = Math.Max(1, Math.Max(adam.Zones.Count, woman.Zones.Count));
        var names = new List<string> { "ok" };
        foreach (var actor in actors)
        {
            for (var zone = 0; zone < zonesPerRig; zone++)
            {
                var rig = actor.Pose.Rig;
                names.Add($"fail:{rig.Id}:{(zone < rig.Zones.Count ? rig.Zones[zone].Id : "none")}");
            }
        }

        VerdictNames = names;
    }

    /// <summary>Every image a frame may name.</summary>
    public AtlasCatalog Atlas { get; }

    /// <summary>
    /// The M1 verdicts by the number the render list carries: "ok", then "fail:&lt;rig&gt;:&lt;zone&gt;" for each
    /// zone of each rig.
    /// </summary>
    public IReadOnlyList<string> VerdictNames { get; }

    /// <summary>Whom the player plays.</summary>
    public PlayerCharacter Character { get; }

    /// <summary>The covering variant of both characters. Slice S2 plays before Genesis 3:7.</summary>
    public Covering Covering { get; init; } = Covering.None;

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

    /// <summary>The M1 verdict of the last frame: "ok" or "fail:&lt;rig&gt;:&lt;zone&gt;".</summary>
    public string Concealment { get; private set; } = "ok";

    /// <summary>How many frames so far had a verdict other than "ok".</summary>
    public long ExposedFrames { get; private set; }

    /// <summary>The game as it is saved: where the player stands, and the story as the resumed game held it.</summary>
    public SaveGame ToSave() => resumed with { Character = Character, TileX = walker.Tile.X, TileY = walker.Tile.Y, Facing = walker.Facing };

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
        ambientSeconds += loop.FrameSeconds;
        if (input[InputBlock.Menu] != 0)
        {
            events |= FrameEvents.Menu;
        }

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
                var tap = pendingTapped ? camera.TileAt(pendingTapX, pendingTapY, map) : null;
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
        camera = Camera.Follow(viewWidth, viewHeight, (me.X + 0.5) * size, (me.Y + 0.5) * size, map);
        var deviceScale = camera.Scale * Math.Clamp(input[InputBlock.PixelRatio], 1, 2);

        var verdict = 0;
        foreach (var actor in actors)
        {
            actor.ExposedZone = checker.FirstExposedZone(actor.Pose, deviceScale);
            if (actor.ExposedZone >= 0 && verdict == 0)
            {
                verdict = 1 + (actor.RigIndex * zonesPerRig) + actor.ExposedZone;
            }
        }

        if (verdict != 0)
        {
            ExposedFrames++;
        }

        Concealment = VerdictNames[verdict];

        // Sprites and characters by depth: the trees row by row from the north, each character before the first row
        // whose trees stand nearer the viewer than its feet.
        drawOrder[0] = actors[0].Y <= actors[1].Y ? actors[0] : actors[1];
        drawOrder[1] = drawOrder[0] == actors[0] ? actors[1] : actors[0];
        var count = 0;
        var nextActor = 0;
        var firstColumn = Math.Max(0, (int)Math.Floor(camera.X / size) - 1);
        var lastColumn = Math.Min(map.Width - 1, (int)Math.Floor((camera.X + (viewWidth / camera.Scale)) / size) + 1);
        var firstRow = Math.Max(0, (int)Math.Floor(camera.Y / size) - 1);
        var lastRow = Math.Min(map.Height - 1, (int)Math.Floor((camera.Y + (viewHeight / camera.Scale)) / size) + 3);
        for (var row = firstRow; row <= lastRow; row++)
        {
            var baseline = ((row + 1) * size) - SpriteBaseAboveTileBottom;
            while (nextActor < drawOrder.Length && FeetY(drawOrder[nextActor]) <= baseline)
            {
                WriteBody(drawOrder[nextActor++], list, ref count);
            }

            for (var column = firstColumn; column <= lastColumn; column++)
            {
                var sprite = Atlas.SpriteId(map.KindAt(new TilePos(column, row)));
                if (sprite >= 0 && count < RenderList.Capacity - SpriteReserve)
                {
                    Write(list, ref count, sprite, Affine.Translation((column + 0.5) * size, baseline), 0);
                }
            }
        }

        while (nextActor < drawOrder.Length)
        {
            WriteBody(drawOrder[nextActor++], list, ref count);
        }

        // The occluder layer, after every character and sprite: the companion foliage, and the default cluster in
        // front of a character whose frame is not concealed (fail closed).
        foreach (var actor in drawOrder)
        {
            var origin = Affine.Translation((actor.X + 0.5) * size, FeetY(actor));
            foreach (var placed in actor.Pose.Parts)
            {
                if (actor.Pose.Rig.Parts[placed.PartIndex].Role == PartRole.Occluder)
                {
                    Write(list, ref count, Atlas.PartId(actor.RigIndex, placed.PartIndex), origin.Then(placed.Transform), RenderList.OccluderLayer | CharacterBits(actor));
                }
            }

            if (actor.ExposedZone >= 0)
            {
                for (var shape = 0; shape < DefaultFoliage.Shapes.Count; shape++)
                {
                    var cluster = DefaultFoliage.Shapes[shape];
                    Write(list, ref count, Atlas.DefaultFoliageId(shape), origin.Then(Affine.Translation(cluster.X, cluster.Y)), RenderList.OccluderLayer | RenderList.FailClosed | CharacterBits(actor));
                }
            }
        }

        list[RenderList.Count] = count;
        list[RenderList.CameraX] = camera.X;
        list[RenderList.CameraY] = camera.Y;
        list[RenderList.Scale] = camera.Scale;
        list[RenderList.PlayerTileX] = walker.Tile.X;
        list[RenderList.PlayerTileY] = walker.Tile.Y;
        list[RenderList.Concealment] = verdict;
        list[RenderList.Facing] = (int)walker.Facing;
        list[RenderList.ExposedFrames] = ExposedFrames;
        list[RenderList.Moving] = walker.Moving ? 1 : 0;
        foreach (var actor in actors)
        {
            list[RenderList.Anchors + (actor.RigIndex * 2)] = (actor.X + 0.5) * size;
            list[RenderList.Anchors + (actor.RigIndex * 2) + 1] = FeetY(actor);
        }
    }

    // Whose part an entry is, for a renderer with depth: the number of the character, from 1.
    private static int CharacterBits(Actor actor) => (actor.RigIndex + 1) << RenderList.CharacterShift;

    private double FeetY(Actor actor) => ((actor.Y + 0.5) * map.TileSize) + FeetBelowCentre;

    private void WriteBody(Actor actor, Span<double> list, ref int count)
    {
        var origin = Affine.Translation((actor.X + 0.5) * map.TileSize, FeetY(actor));
        foreach (var placed in actor.Pose.Parts)
        {
            if (actor.Pose.Rig.Parts[placed.PartIndex].Role != PartRole.Occluder)
            {
                Write(list, ref count, Atlas.PartId(actor.RigIndex, placed.PartIndex), origin.Then(placed.Transform), CharacterBits(actor));
            }
        }
    }

    private static void Write(Span<double> list, ref int count, int atlasId, in Affine transform, int flags)
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
        entry[RenderList.Flags] = flags;
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
