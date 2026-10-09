using AdamEve.Content.Garden;
using AdamEve.Core.Game;
using AdamEve.Core.Input;
using AdamEve.Core.Rigs;
using AdamEve.Core.Saves;
using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

[TestFixture]
public class GardenGameTests
{
    /// <summary>The game with the block of numbers a browser would fill, and a clock of 60 frames a second.</summary>
    private sealed class StubBrowser
    {
        private readonly double[] input = new double[InputBlock.Length];
        private double now;

        public StubBrowser(SaveGame? save = null, Rig? adam = null, double width = 800, double height = 450)
        {
            Garden = StubMap.Shipped();
            Game = new GardenGame(Garden.Map, adam ?? Garden.Adam, Garden.Woman, Garden.Animations, save);
            Turn(width, height);
            Frame();
        }

        public GardenContent Garden { get; }

        public GardenGame Game { get; }

        public double[] List { get; } = new double[RenderList.Length];

        public List<string> Verdicts { get; } = [];

        public void Turn(double width, double height)
        {
            input[InputBlock.ViewWidth] = width;
            input[InputBlock.ViewHeight] = height;
            input[InputBlock.PixelRatio] = 2;
        }

        public FrameEvents Frame(int held = 0, int pressed = 0, (double X, double Y)? tap = null, bool menu = false)
        {
            input[InputBlock.Held] = held;
            input[InputBlock.Pressed] = pressed;
            input[InputBlock.Menu] = menu ? 1 : 0;
            input[InputBlock.Tapped] = tap is null ? 0 : 1;
            input[InputBlock.TapX] = tap?.X ?? 0;
            input[InputBlock.TapY] = tap?.Y ?? 0;
            now += 1000.0 / 60;
            var events = Game.Frame(now, input, List);
            Verdicts.Add(Game.Concealment);
            return events;
        }

        public FrameEvents Frames(int count)
        {
            var events = FrameEvents.None;
            for (var frame = 0; frame < count; frame++)
            {
                events |= Frame();
            }

            return events;
        }

        public (double X, double Y) ScreenOf(TilePos tile) => (
            (((tile.X + 0.5) * 32) - List[RenderList.CameraX]) * List[RenderList.Scale],
            (((tile.Y + 0.5) * 32) - List[RenderList.CameraY]) * List[RenderList.Scale]);

        public IEnumerable<(int AtlasId, double X, double Y, int Flags)> Entries()
        {
            for (var entry = 0; entry < (int)List[RenderList.Count]; entry++)
            {
                var at = RenderList.HeaderLength + (entry * RenderList.EntryLength);
                yield return ((int)List[at], List[at + RenderList.Transform + 4], List[at + RenderList.Transform + 5], (int)List[at + RenderList.Flags]);
            }
        }
    }

    private static SaveGame SaveAt(int x, int y, PlayerCharacter character = PlayerCharacter.Adam, Facing facing = Facing.S) =>
        new() { Character = character, TileX = x, TileY = y, Facing = facing };

    /// <summary>A tile of the glade with a free tile on each of its eight sides.</summary>
    private static TilePos OpenGround(TileMap map)
    {
        var glade = map.Regions.Single(region => region.Id == "central-glade");
        for (var y = glade.Y + 1; y < glade.Y + glade.Height - 1; y++)
        {
            for (var x = glade.X + 1; x < glade.X + glade.Width - 1; x++)
            {
                var around = Enumerable.Range(-1, 3).SelectMany(dy => Enumerable.Range(-1, 3).Select(dx => new TilePos(x + dx, y + dy)));
                if (around.All(tile => map.IsWalkable(tile) && tile != map.Spawn("adam") && tile != map.Spawn("woman")))
                {
                    return new TilePos(x, y);
                }
            }
        }

        throw new InvalidOperationException("The glade has no open ground.");
    }

    [Test]
    public void GardenGame_ANewGame_ShouldPutAdamOnHisStartingTileInTheCentralGlade()
    {
        var browser = new StubBrowser();

        browser.Game.Character.ShouldBe(PlayerCharacter.Adam);
        browser.Game.PlayerTile.ShouldBe(browser.Garden.Map.Spawn("adam"));
        browser.Game.RegionId.ShouldBe("central-glade");
        browser.Game.Concealment.ShouldBe("ok");
    }

    [Test]
    public void Frame_ADirectionPressedForLessThanAFrame_ShouldWalkOneTileAndAskToSaveOnce()
    {
        var browser = new StubBrowser();
        var start = browser.Game.PlayerTile;

        var events = browser.Frame(pressed: InputBlock.Right);
        var stops = 0;
        for (var frame = 0; frame < 40; frame++)
        {
            stops += (browser.Frame() & FrameEvents.Stopped) != 0 ? 1 : 0;
        }

        events.ShouldBe(FrameEvents.None);
        browser.Game.PlayerTile.ShouldBe(new TilePos(start.X + 1, start.Y));
        browser.Game.Moving.ShouldBeFalse();
        stops.ShouldBe(1);
        browser.Game.ToSave().ShouldBe(SaveAt(start.X + 1, start.Y, facing: Facing.E));
    }

    [Test]
    public void Frame_ADirectionPressedThreeTimes_ShouldWalkThreeTiles()
    {
        var browser = new StubBrowser();
        var start = browser.Game.PlayerTile;

        for (var press = 0; press < 3; press++)
        {
            browser.Frame(pressed: InputBlock.Right);
            browser.Frames(30);
        }

        browser.Game.PlayerTile.ShouldBe(new TilePos(start.X + 3, start.Y));
        browser.List[RenderList.PlayerTileX].ShouldBe(start.X + 3);
        browser.List[RenderList.PlayerTileY].ShouldBe(start.Y);
    }

    [Test]
    public void Frame_ADirectionPressedAgainWhileWalking_ShouldWalkTwoTilesAndNoMore()
    {
        var browser = new StubBrowser();
        var start = browser.Game.PlayerTile;

        browser.Frame(pressed: InputBlock.Right);
        browser.Frames(4);
        browser.Frame(pressed: InputBlock.Right);
        browser.Frames(90);

        browser.Game.PlayerTile.ShouldBe(new TilePos(start.X + 2, start.Y));
        browser.Game.Moving.ShouldBeFalse();
    }

    [Test]
    public void Frame_ADirectionHeldAndReleasedBetweenTwoTiles_ShouldStopOnTheNextTile()
    {
        var browser = new StubBrowser();
        var start = browser.Game.PlayerTile;

        browser.Frame(held: InputBlock.Right, pressed: InputBlock.Right);
        for (var frame = 0; frame < 20; frame++)
        {
            browser.Frame(held: InputBlock.Right);
        }

        browser.Frames(60);

        browser.Game.PlayerTile.ShouldBe(new TilePos(start.X + 2, start.Y));
    }

    [TestCase(PlayerCharacter.Adam)]
    [TestCase(PlayerCharacter.Woman)]
    public void Frame_AWalkInAllEightFacings_ShouldHaveTheVerdictOkOnEveryFrame(PlayerCharacter character)
    {
        var open = OpenGround(StubMap.Shipped().Map);
        var browser = new StubBrowser(SaveAt(open.X, open.Y, character));
        (int Bits, Facing Facing)[] walk =
        [
            (InputBlock.Up, Facing.N), (InputBlock.Down, Facing.S),
            (InputBlock.Right, Facing.E), (InputBlock.Left, Facing.W),
            (InputBlock.Up | InputBlock.Right, Facing.NE), (InputBlock.Down | InputBlock.Left, Facing.SW),
            (InputBlock.Down | InputBlock.Right, Facing.SE), (InputBlock.Up | InputBlock.Left, Facing.NW),
        ];

        foreach (var (bits, facing) in walk)
        {
            var before = browser.Game.PlayerTile;
            browser.Frame(pressed: bits);
            browser.Frames(40);

            browser.Game.PlayerFacing.ShouldBe(facing);
            browser.Game.PlayerTile.ShouldBe(new TilePos(before.X + facing.DeltaX(), before.Y + facing.DeltaY()));
            browser.List[RenderList.Facing].ShouldBe((int)facing);
        }

        browser.Game.PlayerTile.ShouldBe(open);
        browser.Verdicts.Count.ShouldBeGreaterThan(320);
        browser.Verdicts.ShouldAllBe(verdict => verdict == "ok");
        browser.Game.ExposedFrames.ShouldBe(0);
        browser.List[RenderList.Concealment].ShouldBe(0);
        browser.Entries().ShouldAllBe(entry => (entry.Flags & RenderList.FailClosed) == 0);
    }

    [Test]
    public void Frame_ATapOnATileAcrossTheRiver_ShouldWalkAroundTheWaterAndArrive()
    {
        var browser = new StubBrowser(SaveAt(29, 20));
        var map = browser.Garden.Map;
        var goal = new TilePos(34, 20);
        map.KindAt(new TilePos(31, 20)).ShouldBe(TileKind.Water);
        var walked = new HashSet<TilePos>();

        browser.Frame(tap: browser.ScreenOf(goal));
        for (var frame = 0; frame < 1200 && browser.Game.PlayerTile != goal; frame++)
        {
            browser.Frame();
            walked.Add(browser.Game.PlayerTile);
        }

        browser.Frames(30);
        browser.Game.PlayerTile.ShouldBe(goal);
        browser.Game.Moving.ShouldBeFalse();
        walked.ShouldAllBe(tile => map.IsWalkable(tile));
        walked.ShouldContain(tile => map.KindAt(tile) == TileKind.Crossing);
        browser.Verdicts.ShouldAllBe(verdict => verdict == "ok");
    }

    [Test]
    public void Frame_ATapOnWater_ShouldStay()
    {
        var browser = new StubBrowser(SaveAt(29, 20));

        browser.Frame(tap: browser.ScreenOf(new TilePos(31, 20)));
        browser.Frames(30);

        browser.Game.PlayerTile.ShouldBe(new TilePos(29, 20));
    }

    [Test]
    public void Frame_TowardTheOtherCharacter_ShouldNotStepOnTheirTile()
    {
        var map = StubMap.Shipped().Map;
        var woman = map.Spawn("woman");
        var browser = new StubBrowser(SaveAt(woman.X + 1, woman.Y));

        browser.Frame(pressed: InputBlock.Left);
        browser.Frames(30);

        browser.Game.PlayerTile.ShouldBe(new TilePos(woman.X + 1, woman.Y));
        browser.Game.PlayerFacing.ShouldBe(Facing.W);
    }

    [Test]
    public void Frame_WhileTheMenuIsOpen_ShouldNotWalkAndShouldStillJudgeTheFrame()
    {
        var browser = new StubBrowser();
        var start = browser.Game.PlayerTile;
        browser.Game.Paused = true;

        browser.Frame(held: InputBlock.Right, pressed: InputBlock.Right, tap: (10, 10));
        browser.Frames(30);
        browser.Game.Paused = false;
        browser.Frames(30);

        browser.Game.PlayerTile.ShouldBe(start);
        browser.Verdicts.Count.ShouldBe(62);
        browser.Verdicts.ShouldAllBe(verdict => verdict == "ok");
    }

    [Test]
    public void Frame_TheMenuKey_ShouldSayThatTheMenuWasAsked()
    {
        var browser = new StubBrowser();

        var events = browser.Frame(menu: true);

        events.ShouldBe(FrameEvents.Menu);
    }

    [Test]
    public void Frame_AStepIntoAnotherRegion_ShouldSayThatTheRegionChanged()
    {
        var browser = new StubBrowser(SaveAt(27, 16));
        browser.Game.RegionId.ShouldBe("central-glade");

        browser.Frame(pressed: InputBlock.Up);
        var events = browser.Frames(40);

        (events & FrameEvents.RegionChanged).ShouldBe(FrameEvents.RegionChanged);
        browser.Game.RegionId.ShouldBe("spring-of-eden");
    }

    [Test]
    public void Frame_AfterTheViewTurns_ShouldKeepThePlayerOnTheSameTileInTheMiddleOfThePlayArea()
    {
        var browser = new StubBrowser(SaveAt(29, 24), width: 412, height: 660);
        browser.Frame(pressed: InputBlock.Up);
        browser.Frames(40);
        var tile = browser.Game.PlayerTile;
        var portrait = browser.ScreenOf(tile);

        browser.Turn(915, 412);
        browser.Frames(2);
        var landscape = browser.ScreenOf(tile);

        browser.Game.PlayerTile.ShouldBe(tile);
        browser.Game.PlayerFacing.ShouldBe(Facing.N);
        portrait.X.ShouldBe(206, 1);
        portrait.Y.ShouldBe(330, 1);
        landscape.X.ShouldBe(457.5, 1);
        landscape.Y.ShouldBe(206, 1);
        browser.Verdicts.ShouldAllBe(verdict => verdict == "ok");
    }

    [Test]
    public void GardenGame_ASavedGame_ShouldResumeOnItsTileWithItsFacingAndCharacter()
    {
        var save = SaveAt(22, 20, PlayerCharacter.Woman, Facing.NW);

        var browser = new StubBrowser(save);

        browser.Game.ToSave().ShouldBe(save);
        browser.Game.Character.ShouldBe(PlayerCharacter.Woman);
        browser.List[RenderList.PlayerTileX].ShouldBe(22);
        browser.List[RenderList.PlayerTileY].ShouldBe(20);
    }

    [Test]
    public void Frame_TheRenderList_ShouldDrawTheOccluderLayerAfterEveryCharacterAndSprite()
    {
        var browser = new StubBrowser();
        var garden = browser.Garden;
        var entries = browser.Entries().ToList();
        var firstOccluder = entries.FindIndex(entry => (entry.Flags & RenderList.OccluderLayer) != 0);

        var foliage = garden.Adam.Parts.Count(part => part.Role == PartRole.Occluder) + garden.Woman.Parts.Count(part => part.Role == PartRole.Occluder);

        firstOccluder.ShouldBeGreaterThan(0);
        entries.Skip(firstOccluder).ShouldAllBe(entry => (entry.Flags & RenderList.OccluderLayer) != 0);
        entries.Count(entry => (entry.Flags & RenderList.OccluderLayer) != 0).ShouldBe(foliage);
        entries.ShouldContain(entry => entry.AtlasId == browser.Game.Atlas.SpriteId(TileKind.TreeOfLife));
        entries.ShouldAllBe(entry => entry.AtlasId >= 0 && entry.AtlasId < browser.Game.Atlas.Count);
    }

    [Test]
    public void Frame_ACharacterBehindATree_ShouldBeDrawnBeforeTheTree()
    {
        var map = StubMap.Shipped().Map;
        var tree = Enumerable.Range(0, map.Width * map.Height)
            .Select(index => new TilePos(index % map.Width, index / map.Width))
            .First(tile => map.KindAt(tile) == TileKind.Tree && map.IsWalkable(new TilePos(tile.X, tile.Y - 1)) && map.IsWalkable(new TilePos(tile.X, tile.Y + 1)));
        var behind = new StubBrowser(SaveAt(tree.X, tree.Y - 1));
        var inFront = new StubBrowser(SaveAt(tree.X, tree.Y + 1));
        var torso = behind.Game.Atlas.PartId(0, behind.Garden.Adam.PartIndex("torso"));
        var sprite = behind.Game.Atlas.SpriteId(TileKind.Tree);
        double treeX = (tree.X + 0.5) * 32;

        int IndexOfTree(StubBrowser browser) => browser.Entries().ToList().FindIndex(entry => entry.AtlasId == sprite && Math.Abs(entry.X - treeX) < 0.01 && entry.Y > tree.Y * 32 && entry.Y < (tree.Y + 1) * 32);
        int IndexOfTorso(StubBrowser browser) => browser.Entries().ToList().FindIndex(entry => entry.AtlasId == torso);

        IndexOfTorso(behind).ShouldBeLessThan(IndexOfTree(behind));
        IndexOfTorso(inFront).ShouldBeGreaterThan(IndexOfTree(inFront));
    }

    [Test]
    public void Frame_ACharacterWithoutAConcealmentRecord_ShouldDrawTheDefaultFoliageInFrontAndReport()
    {
        var garden = StubMap.Shipped();
        var unrecorded = StubMap.With(garden.Adam, concealment: []);

        var browser = new StubBrowser(adam: unrecorded);
        browser.Frames(5);

        var entries = browser.Entries().ToList();
        var failClosed = entries.Where(entry => (entry.Flags & RenderList.FailClosed) != 0).ToList();
        var feetX = (browser.Game.PlayerTile.X + 0.5) * 32;
        browser.Game.Concealment.ShouldBe("fail:adam:pelvis");
        browser.Verdicts.ShouldAllBe(verdict => verdict == "fail:adam:pelvis");
        browser.Game.ExposedFrames.ShouldBe(6);
        browser.Game.VerdictNames[(int)browser.List[RenderList.Concealment]].ShouldBe("fail:adam:pelvis");
        browser.List[RenderList.ExposedFrames].ShouldBe(6);
        failClosed.Select(entry => entry.AtlasId).ShouldBe(Enumerable.Range(0, DefaultFoliage.Shapes.Count).Select(browser.Game.Atlas.DefaultFoliageId));
        failClosed.ShouldAllBe(entry => (entry.Flags & RenderList.OccluderLayer) != 0);
        failClosed.Select(entry => entry.X - feetX).ShouldBe(DefaultFoliage.Shapes.Select(shape => shape.X));
        entries.FindLastIndex(entry => (entry.Flags & RenderList.OccluderLayer) == 0).ShouldBeLessThan(entries.IndexOf(failClosed[0]));
    }

    [Test]
    public void Frame_ACharacterWhoseFoliageIsMissing_ShouldDrawTheDefaultFoliageForThatCharacterOnly()
    {
        var garden = StubMap.Shipped();
        var bare = StubMap.With(garden.Adam, parts: garden.Adam.Parts.Where(part => part.Role != PartRole.Occluder));

        var browser = new StubBrowser(adam: bare);

        browser.Game.Concealment.ShouldBe("fail:adam:pelvis");
        browser.Entries().Count(entry => (entry.Flags & RenderList.FailClosed) != 0).ShouldBe(DefaultFoliage.Shapes.Count);
    }

    [Test]
    public void VerdictNames_TheGarden_ShouldNameOkAndEachZoneOfEachRig()
    {
        var browser = new StubBrowser();

        browser.Game.VerdictNames[0].ShouldBe("ok");
        browser.Game.VerdictNames.ShouldContain("fail:adam:pelvis");
        browser.Game.VerdictNames.ShouldContain("fail:woman:pelvis");
        browser.Game.VerdictNames.ShouldContain("fail:woman:chest");
    }

    [Test]
    public void ToNumbers_TheAtlas_ShouldListOneFlatShapeForEachRigPartAndTheShapesOfEachTree()
    {
        var browser = new StubBrowser();
        var atlas = browser.Game.Atlas;

        var numbers = atlas.ToNumbers();

        numbers[0].ShouldBe(atlas.Count);
        atlas.Shapes(atlas.PartId(1, 0)).Count.ShouldBe(1);
        atlas.Shapes(atlas.SpriteId(TileKind.TreeOfKnowledge)).ShouldBe(PlaceholderArt.SpriteOf(TileKind.TreeOfKnowledge));
        atlas.SpriteId(TileKind.Grass).ShouldBe(-1);
        numbers.Length.ShouldBe(1 + atlas.Count + (6 * Enumerable.Range(0, atlas.Count).Sum(id => atlas.Shapes(id).Count)));
    }
}
