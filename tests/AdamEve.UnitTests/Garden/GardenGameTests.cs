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

        private double viewWidth;
        private double viewHeight;

        public StubBrowser(SaveGame? save = null, Rig? adam = null, double width = 800, double height = 450, Rig? woman = null, bool flat = false)
        {
            Garden = StubMap.Shipped();
            Game = new GardenGame(Garden.Map, adam ?? Garden.Adam, woman ?? Garden.Woman, Garden.Animations, save);
            input[InputBlock.Projection] = flat ? InputBlock.Flat : InputBlock.Perspective;
            Turn(width, height);
            Frame();
        }

        public GardenContent Garden { get; }

        public GardenGame Game { get; }

        public double[] List { get; } = new double[RenderList.Length];

        public List<string> Verdicts { get; } = [];

        public void Turn(double width, double height)
        {
            viewWidth = width;
            viewHeight = height;
            input[InputBlock.ViewWidth] = width;
            input[InputBlock.ViewHeight] = height;
            input[InputBlock.PixelRatio] = 2;
        }

        /// <summary>The perspective camera of the last frame, built from the numbers the render list carries: as the renderer builds its own.</summary>
        public PerspectiveCamera Camera()
        {
            var distance = List[RenderList.EyeHeight] / Math.Sin(List[RenderList.Tilt]);
            return new PerspectiveCamera(viewWidth, viewHeight, List[RenderList.EyeX], List[RenderList.EyeY] - (distance * Math.Cos(List[RenderList.Tilt])), distance);
        }

        /// <summary>The player's setting "reduce motion", as the browser reports it.</summary>
        public bool ReducedMotion { get; set; }

        public FrameEvents Frame(int held = 0, int pressed = 0, (double X, double Y)? tap = null, bool menu = false)
        {
            input[InputBlock.Still] = ReducedMotion ? 1 : 0;
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

        /// <summary>Where the middle of a tile lies on the screen: through the camera the frame was composed for.</summary>
        public (double X, double Y) ScreenOf(TilePos tile) => (int)List[RenderList.Projection] == InputBlock.Flat
            ? ((((tile.X + 0.5) * 32) - List[RenderList.CameraX]) * List[RenderList.Scale], (((tile.Y + 0.5) * 32) - List[RenderList.CameraY]) * List[RenderList.Scale])
            : Camera().Project((tile.X + 0.5) * 32, (tile.Y + 0.5) * 32);

        public IEnumerable<(int AtlasId, double X, double Y, int Character)> Entries()
        {
            for (var entry = 0; entry < (int)List[RenderList.Count]; entry++)
            {
                var at = RenderList.HeaderLength + (entry * RenderList.EntryLength);
                yield return ((int)List[at], List[at + RenderList.Transform + 4], List[at + RenderList.Transform + 5], (int)List[at + RenderList.Character]);
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
        browser.Entries().Count(entry => entry.Character == 1).ShouldBeGreaterThanOrEqualTo(8);
        browser.Entries().Count(entry => entry.Character == 2).ShouldBeGreaterThanOrEqualTo(8);
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
    public void Frame_TheRenderList_ShouldListEveryPartOfBothFiguresInTheOrderOfTheirDepthAndNoFoliage()
    {
        var browser = new StubBrowser();
        var garden = browser.Garden;
        var entries = browser.Entries().ToList();
        var atlas = browser.Game.Atlas;

        foreach (var (rig, index) in new[] { (garden.Adam, 0), (garden.Woman, 1) })
        {
            var listed = entries.Where(entry => entry.Character == index + 1).Select(entry => entry.AtlasId).ToList();
            var parts = listed.Select(id => rig.Parts[id - atlas.PartId(index, 0)]).ToList();

            parts.Select(part => part.Id).ShouldBe(rig.Parts.Where(part => part.Front is not null && part.WornWith(Covering.None)).OrderBy(part => part.Front!.Depth).Select(part => part.Id));
            parts.ShouldAllBe(part => RigStructure.AllowedColours(PartRole.Body).Contains(part.Colour) || RigStructure.AllowedColours(PartRole.Hair).Contains(part.Colour) || (part.Colour == RigStructure.Mouth && part.Id.StartsWith(RigStructure.MouthPrefix, StringComparison.Ordinal)));
        }

        entries.ShouldContain(entry => entry.AtlasId == atlas.SpriteId(SceneryKind.TreeOfLife));
        entries.ShouldAllBe(entry => entry.AtlasId >= 0 && entry.AtlasId < atlas.Count);
        var scenery = Enum.GetValues<SceneryKind>().Count(kind => kind != SceneryKind.None);
        atlas.Count.ShouldBe(garden.Adam.Parts.Count + garden.Woman.Parts.Count + scenery, "the atlas holds the parts of the two rigs and one image for each kind of scenery: no foliage cluster for a figure");
        Enumerable.Range(0, garden.Adam.Parts.Count + garden.Woman.Parts.Count).ShouldAllBe(id => atlas.KindOf(id) == SceneryKind.None);
    }

    [Test]
    public void Frame_AStandingFigureWithAndWithoutReducedMotion_ShouldBreatheOnlyWithoutItAndWalkEitherWay()
    {
        var moving = new StubBrowser();
        var still = new StubBrowser { ReducedMotion = true };
        List<(int AtlasId, double X, double Y, int Character)> Figures(StubBrowser browser) => [.. browser.Entries().Where(entry => entry.Character != 0)];
        var movingBefore = Figures(moving);
        still.Frame();
        var stillBefore = Figures(still);

        moving.Frames(40);
        still.Frames(40);

        Figures(moving).ShouldNotBe(movingBefore, "the stance breathes and the hair sways");
        Figures(still).ShouldBe(stillBefore, "under reduced motion a standing figure does not move");
        var tile = still.Game.PlayerTile;
        still.Frame(held: InputBlock.Right);
        still.Frames(60);
        still.Game.PlayerTile.ShouldNotBe(tile, "the player still walks");
        still.Verdicts.ShouldAllBe(verdict => verdict == "ok");
    }

    [Test]
    public void Frame_ACharacterBehindATree_ShouldBeDrawnBeforeTheTree()
    {
        var map = StubMap.Shipped().Map;
        var tree = Enumerable.Range(0, map.Width * map.Height)
            .Select(index => new TilePos(index % map.Width, index / map.Width))
            .First(tile => map.KindAt(tile) == TileKind.Tree && map.IsWalkable(new TilePos(tile.X, tile.Y - 1)) && map.IsWalkable(new TilePos(tile.X, tile.Y + 1)));
        var kind = new GardenScenery(map).KindAt(tree);
        var behind = new StubBrowser(SaveAt(tree.X, tree.Y - 1));
        var inFront = new StubBrowser(SaveAt(tree.X, tree.Y + 1));
        var torso = behind.Game.Atlas.PartId(0, behind.Garden.Adam.PartIndex("torso"));
        var sprite = behind.Game.Atlas.SpriteId(kind);
        double treeX = (tree.X + 0.5) * 32;

        int IndexOfTree(StubBrowser browser) => browser.Entries().ToList().FindIndex(entry => entry.AtlasId == sprite && Math.Abs(entry.X - treeX) < 0.01 && entry.Y > tree.Y * 32 && entry.Y < (tree.Y + 1) * 32);
        int IndexOfTorso(StubBrowser browser) => browser.Entries().ToList().FindIndex(entry => entry.AtlasId == torso);

        IndexOfTorso(behind).ShouldBeLessThan(IndexOfTree(behind));
        IndexOfTorso(inFront).ShouldBeGreaterThan(IndexOfTree(inFront));
    }

    [Test]
    public void Frame_ACharacterWithoutAConcealmentRecord_ShouldNotBeDrawnAtAllAndBeReportedOnEveryFrame()
    {
        var garden = StubMap.Shipped();
        var unrecorded = StubMap.With(garden.Adam, concealment: []);

        var browser = new StubBrowser(adam: unrecorded);
        browser.Frames(5);

        var entries = browser.Entries().ToList();
        browser.Game.Concealment.ShouldBe("fail:adam:structure");
        browser.Verdicts.ShouldAllBe(verdict => verdict == "fail:adam:structure");
        browser.Game.ExposedFrames.ShouldBe(6);
        browser.Game.VerdictNames[(int)browser.List[RenderList.Concealment]].ShouldBe("fail:adam:structure");
        browser.List[RenderList.ExposedFrames].ShouldBe(6);
        entries.ShouldAllBe(entry => entry.Character != 1, "a figure that does not pass is not drawn (fail closed)");
        entries.Count(entry => entry.Character == 2).ShouldBeGreaterThan(8, "the other figure passes and is drawn");
    }

    [Test]
    public void Frame_TheWomanWithoutTheHairOverHerChest_ShouldNotBeDrawnAndBeReportedForThatCharacterOnly()
    {
        var garden = StubMap.Shipped();
        var shorn = StubMap.With(garden.Woman, parts: garden.Woman.Parts.Where(part => !part.Id.StartsWith("hair-front", StringComparison.Ordinal)));

        var browser = new StubBrowser(woman: shorn);
        browser.Frames(3);

        browser.Game.Concealment.ShouldBe("fail:woman:chest");
        browser.Game.ExposedFrames.ShouldBe(4);
        browser.Entries().ShouldAllBe(entry => entry.Character != 2);
        browser.Entries().Count(entry => entry.Character == 1).ShouldBeGreaterThan(8);
    }

    [Test]
    public void Frame_ACharacterWithSomethingDrawnInThePelvicZone_ShouldNotBeDrawnAndBeReported()
    {
        var garden = StubMap.Shipped();
        var placement = new PartPlacement(0, 14, 30, 0);
        var marked = StubMap.With(garden.Adam, parts: garden.Adam.Parts.Append(new RigPart("hair-low", "head", PartShape.Ellipse, 6, 44, RigStructure.Hair, PartRole.Hair, [], placement, placement, placement, 0)));

        var browser = new StubBrowser(adam: marked);

        RigStructure.Violations(marked).ShouldBeEmpty("the rig passes the lists: the frame check is what refuses it");
        browser.Game.Concealment.ShouldBe("fail:adam:pelvis");
        browser.Entries().ShouldAllBe(entry => entry.Character != 1);
    }

    [Test]
    public void VerdictNames_TheGarden_ShouldNameOkAndEachZoneOfEachRig()
    {
        var browser = new StubBrowser();

        browser.Game.VerdictNames[0].ShouldBe("ok");
        browser.Game.VerdictNames.ShouldContain("fail:adam:pelvis");
        browser.Game.VerdictNames.ShouldContain("fail:woman:pelvis");
        browser.Game.VerdictNames.ShouldContain("fail:woman:chest");
        browser.Game.VerdictNames.ShouldContain("fail:adam:structure");
        browser.Game.VerdictNames.ShouldContain("fail:woman:structure");
        browser.Game.VerdictNames.ShouldBeUnique();
    }

    [Test]
    public void ToNumbers_TheAtlas_ShouldListOneFlatShapeForEachRigPartAndTheShapesOfEachTree()
    {
        var browser = new StubBrowser();
        var atlas = browser.Game.Atlas;

        var numbers = atlas.ToNumbers();

        numbers[0].ShouldBe(atlas.Count);
        atlas.Shapes(atlas.PartId(1, 0)).Count.ShouldBe(1);
        atlas.Shapes(atlas.SpriteId(SceneryKind.TreeOfKnowledge)).ShouldBe(PlaceholderArt.SpriteOf(SceneryKind.TreeOfKnowledge));
        atlas.SpriteId(SceneryKind.None).ShouldBe(-1);
        numbers.Length.ShouldBe(1 + atlas.Count + (7 * Enumerable.Range(0, atlas.Count).Sum(id => atlas.Shapes(id).Count)));
        atlas.ToKindNumbers().Length.ShouldBe(atlas.Count);
        atlas.ToKindNumbers()[atlas.SpriteId(SceneryKind.PalmTree)].ShouldBe((int)SceneryKind.PalmTree);
        atlas.ToKindNumbers()[atlas.PartId(1, 0)].ShouldBe(0);
    }

    [Test]
    public void Frame_TheRenderList_ShouldSayWhereEachCharacterStandsAndWhosePartEachEntryIs()
    {
        var browser = new StubBrowser();
        var garden = browser.Garden;
        var adam = garden.Map.Spawn("adam");
        var woman = garden.Map.Spawn("woman");

        var entries = browser.Entries().ToList();

        browser.List[RenderList.Anchors].ShouldBe((adam.X + 0.5) * 32);
        browser.List[RenderList.Anchors + 1].ShouldBe(((adam.Y + 0.5) * 32) + 8);
        browser.List[RenderList.Anchors + 2].ShouldBe((woman.X + 0.5) * 32);
        browser.List[RenderList.Anchors + 3].ShouldBe(((woman.Y + 0.5) * 32) + 8);
        (RenderList.Anchors + (RenderList.AnchorCount * 2)).ShouldBeLessThanOrEqualTo(RenderList.HeaderLength);
        foreach (var entry in entries)
        {
            var sprite = browser.Game.Atlas.KindOf(entry.AtlasId) != SceneryKind.None;
            entry.Character.ShouldBe(sprite ? 0 : entry.AtlasId < garden.Adam.Parts.Count ? 1 : 2);
        }

        entries.Count(entry => entry.Character == 1).ShouldBeGreaterThan(8);
        entries.Count(entry => entry.Character == 2).ShouldBeGreaterThan(8);
    }

    [TestCase(1280, 720)]
    [TestCase(412, 660)]
    [TestCase(839, 412)]
    [TestCase(390, 520)]
    public void Frame_TheRenderList_ShouldCarryThePerspectiveCameraThatFollowsThePlayer(double width, double height)
    {
        var browser = new StubBrowser(SaveAt(29, 22), width: width, height: height);
        var expected = PerspectiveCamera.Follow(width, height, 29.5 * 32, 22.5 * 32, browser.Garden.Map);

        var camera = browser.Camera();

        browser.List[RenderList.Projection].ShouldBe(InputBlock.Perspective);
        browser.List[RenderList.EyeX].ShouldBe(expected.EyeX);
        browser.List[RenderList.EyeHeight].ShouldBe(expected.EyeHeight);
        browser.List[RenderList.EyeY].ShouldBe(expected.EyeY);
        browser.List[RenderList.Tilt].ShouldBe(PerspectiveCamera.TiltRadians);
        browser.List[RenderList.FieldOfView].ShouldBe(PerspectiveCamera.FieldOfViewRadians);
        browser.List[RenderList.HazeStart].ShouldBe(expected.HazeStart);
        browser.List[RenderList.HazeEnd].ShouldBe(expected.HazeEnd);
        browser.List[RenderList.FigureDepthHeight].ShouldBe(PerspectiveCamera.FigureDepthHeight);
        browser.List[RenderList.Scale].ShouldBe(expected.Scale);
        camera.Distance.ShouldBe(expected.Distance, 1e-9);
        camera.FocusY.ShouldBe(expected.FocusY, 1e-9);
        (RenderList.FigureDepthHeight + 1).ShouldBeLessThanOrEqualTo(RenderList.HeaderLength);
    }

    [TestCase(1280, 720)]
    [TestCase(412, 660)]
    [TestCase(839, 412)]
    public void Frame_ATapOnEachTileAroundThePlayerSeenThroughThePerspectiveCamera_ShouldWalkToThatTile(double width, double height)
    {
        var open = OpenGround(StubMap.Shipped().Map);

        foreach (var (dx, dy) in new[] { (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1) })
        {
            var browser = new StubBrowser(SaveAt(open.X, open.Y), width: width, height: height);
            var goal = new TilePos(open.X + dx, open.Y + dy);

            browser.Frame(tap: browser.ScreenOf(goal));
            browser.Frames(60);

            browser.Game.PlayerTile.ShouldBe(goal);
        }
    }

    [Test]
    public void Frame_ATapNearTheTopOfThePlayArea_ShouldNameAFartherTileThanAFlatPictureWould()
    {
        var browser = new StubBrowser(SaveAt(29, 24), width: 1280, height: 720);
        var map = browser.Garden.Map;
        var camera = browser.Camera();

        var under = camera.TileAt(640, 150, map).ShouldNotBeNull();
        var flat = Camera.Follow(1280, 720, 29.5 * 32, 24.5 * 32, map).TileAt(640, 150, map).ShouldNotBeNull();

        under.X.ShouldBe(29);
        under.Y.ShouldBeLessThan(flat.Y);
    }

    [Test]
    public void Frame_TheRenderList_ShouldListTheSceneryThePerspectiveCameraSeesAndNotTheWholeMap()
    {
        var browser = new StubBrowser(SaveAt(29, 24), width: 1280, height: 720);
        var map = browser.Garden.Map;
        var camera = browser.Camera();
        var scenery = browser.Game.Scenery;
        var (firstRow, lastRow) = camera.VisibleRows(map);
        var expected = new List<(double X, double Y)>();
        for (var row = firstRow; row <= lastRow; row++)
        {
            var (firstColumn, lastColumn) = camera.VisibleColumns(row, map);
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                if (scenery.KindAt(new TilePos(column, row)) != SceneryKind.None)
                {
                    expected.Add(((column + 0.5) * 32, ((row + 1) * 32) - 6));
                }
            }
        }

        var listed = browser.Entries().Where(entry => entry.Character == 0).Select(entry => (entry.X, entry.Y)).ToList();
        var all = Enumerable.Range(0, map.Width * map.Height).Count(index => scenery.KindAt(new TilePos(index % map.Width, index / map.Width)) != SceneryKind.None);

        listed.ShouldBe(expected);
        listed.Count.ShouldBeGreaterThan(3);
        listed.Count.ShouldBeLessThan(all);
        camera.VisibleColumns(firstRow, map).Last.ShouldBeGreaterThan(camera.VisibleColumns(lastRow, map).Last, "a far row is wider than a near one");
    }

    [Test]
    public void Frame_WithTheFlatProjectionOfTheFallbackRenderer_ShouldCullAndReadATapAsAFlatPictureDoes()
    {
        var browser = new StubBrowser(SaveAt(29, 20), flat: true);
        var map = browser.Garden.Map;
        var flat = Camera.Follow(800, 450, 29.5 * 32, 20.5 * 32, map);
        var goal = new TilePos(27, 19);

        browser.List[RenderList.Projection].ShouldBe(InputBlock.Flat);
        browser.List[RenderList.CameraX].ShouldBe(flat.X);
        browser.List[RenderList.CameraY].ShouldBe(flat.Y);
        browser.List[RenderList.Scale].ShouldBe(flat.Scale);
        browser.Entries().Where(entry => entry.Character == 0).ShouldAllBe(entry => entry.Y >= flat.Y - 64 && entry.Y <= flat.Y + (450 / flat.Scale) + 128);
        browser.Frame(tap: ((((goal.X + 0.5) * 32) - flat.X) * flat.Scale, (((goal.Y + 0.5) * 32) - flat.Y) * flat.Scale));
        browser.Frames(90);
        browser.Game.PlayerTile.ShouldBe(goal);
        browser.Verdicts.ShouldAllBe(verdict => verdict == "ok");
    }

    [Test]
    public void Frame_ThePlayerWalking_ShouldMoveItsAnchorWithIt()
    {
        var open = OpenGround(StubMap.Shipped().Map);
        var browser = new StubBrowser(SaveAt(open.X, open.Y));
        var before = browser.List[RenderList.Anchors];

        browser.Frame(pressed: InputBlock.Right);
        browser.Frames(8);

        browser.Game.Moving.ShouldBeTrue();
        browser.List[RenderList.Anchors].ShouldBeGreaterThan(before);
        browser.List[RenderList.Anchors].ShouldBeLessThan(before + 32);
        browser.List[RenderList.Anchors + 1].ShouldBe(((open.Y + 0.5) * 32) + 8);
    }
}
