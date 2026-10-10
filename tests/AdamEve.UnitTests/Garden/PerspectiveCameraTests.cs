using AdamEve.Core.Rigs;
using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

/// <summary>
/// The projection model of the garden (design, section 6 and decision D18): what the perspective camera shows,
/// which ground lies under a tap, where the camera stops, and that a figure on the screen is the figure the
/// modesty check judged.
/// </summary>
[TestFixture]
public class PerspectiveCameraTests
{
    // The play areas of the three device profiles of the design, upright and on their side, and the smallest size.
    private static readonly (string Name, double Width, double Height)[] PlayAreas =
    [
        ("Desktop Chrome, landscape", 1280, 720),
        ("Desktop Chrome, portrait", 720, 1080),
        ("Pixel 7, portrait, above the controls", 412, 660),
        ("Pixel 7, landscape", 839, 412),
        ("iPhone 13, portrait, above the controls", 390, 500),
        ("iPhone 13, landscape", 664, 390),
        ("the smallest size, portrait, above the controls", 360, 480),
    ];

    private static readonly (double X, double Y)[] ScreenShares = [(0.5, 0.5), (0, 1), (1, 1), (0, 0.2), (1, 0.2), (0.25, 0.75), (0.75, 0.35)];

    private static TileMap Garden() => StubMap.Shipped().Map;

    private static IEnumerable<TestCaseData> EveryPlayArea() => PlayAreas.Select(area => new TestCaseData(area.Width, area.Height).SetArgDisplayNames(area.Name));

    [TestCaseSource(nameof(EveryPlayArea))]
    public void Follow_APlayArea_ShouldShowAbout15TilesAcrossInLandscapeAnd9InPortraitOnThePlayersRow(double width, double height)
    {
        var map = Garden();

        var camera = PerspectiveCamera.Follow(width, height, 30.5 * 32, 24.5 * 32, map);
        var left = camera.GroundAt(0, height / 2).ShouldNotBeNull();
        var right = camera.GroundAt(width, height / 2).ShouldNotBeNull();

        ((right.X - left.X) / map.TileSize).ShouldBe(width > height ? Camera.LongSideTiles : Camera.ShortSideTiles, 0.01);
        right.Y.ShouldBe(24.5 * 32, 1e-6);
        left.Y.ShouldBe(24.5 * 32, 1e-6);
        camera.Scale.ShouldBe(Camera.Follow(width, height, 30.5 * 32, 24.5 * 32, map).Scale, 1e-9);
    }

    [TestCaseSource(nameof(EveryPlayArea))]
    public void Project_ThePlayerInTheOpen_ShouldBeTheMiddleOfThePlayArea(double width, double height)
    {
        var camera = PerspectiveCamera.Follow(width, height, 30.5 * 32, 24.5 * 32, Garden());

        var (x, y) = camera.Project(30.5 * 32, 24.5 * 32);

        x.ShouldBe(width / 2, 1e-9);
        y.ShouldBe(height / 2, 1e-9);
    }

    [TestCaseSource(nameof(EveryPlayArea))]
    public void GroundAt_TheCornersTheCentreAndPointsBetween_ShouldBeThePointOfTheGroundThatProjectsBackThere(double width, double height)
    {
        var camera = PerspectiveCamera.Follow(width, height, 30.5 * 32, 24.5 * 32, Garden());

        foreach (var share in ScreenShares)
        {
            var ground = camera.GroundAt(share.X * width, share.Y * height).ShouldNotBeNull();
            var (x, y) = camera.Project(ground.X, ground.Y);

            x.ShouldBe(share.X * width, 1e-6);
            y.ShouldBe(share.Y * height, 1e-6);
        }
    }

    [TestCaseSource(nameof(EveryPlayArea))]
    public void TileAt_TheMiddleOfEveryTileThePlayAreaShows_ShouldBeThatTile(double width, double height)
    {
        var map = Garden();
        var camera = PerspectiveCamera.Follow(width, height, 30.5 * 32, 24.5 * 32, map);
        var seen = 0;

        for (var row = 0; row < map.Height; row++)
        {
            for (var column = 0; column < map.Width; column++)
            {
                var (x, y) = camera.Project((column + 0.5) * 32, (row + 0.5) * 32);
                if (x < 0 || y < 0 || x > width || y > height || camera.DepthOf((column + 0.5) * 32, (row + 0.5) * 32) <= 0)
                {
                    continue;
                }

                camera.TileAt(x, y, map).ShouldBe(new TilePos(column, row));
                seen++;
            }
        }

        seen.ShouldBeGreaterThan(100);
    }

    [TestCaseSource(nameof(EveryPlayArea))]
    public void TileAt_TheFourCornersAndTheCentreOfThePlayArea_ShouldBeTheTilesUnderThem(double width, double height)
    {
        var map = Garden();
        var camera = PerspectiveCamera.Follow(width, height, 30.5 * 32, 24.5 * 32, map);
        var player = new TilePos(30, 24);

        var centre = camera.TileAt(width / 2, height / 2, map).ShouldNotBeNull();
        var bottomLeft = camera.TileAt(0.5, height - 0.5, map).ShouldNotBeNull();
        var bottomRight = camera.TileAt(width - 0.5, height - 0.5, map).ShouldNotBeNull();
        var topLeft = camera.TileAt(0.5, 0.5, map);
        var topRight = camera.TileAt(width - 0.5, 0.5, map);

        centre.ShouldBe(player);
        bottomLeft.Y.ShouldBe(bottomRight.Y);
        bottomLeft.Y.ShouldBeGreaterThan(player.Y);
        (player.X - bottomLeft.X).ShouldBe(bottomRight.X - player.X);
        // A far row is wider than a near one: the top corners lie farther from the player's column than the bottom ones.
        foreach (var (corner, side) in new[] { (topLeft, -1), (topRight, 1) })
        {
            if (corner is { } tile)
            {
                tile.Y.ShouldBeLessThan(player.Y);
                ((tile.X - player.X) * side).ShouldBeGreaterThan(bottomRight.X - player.X);
            }
        }

        camera.TileAt(-1, height / 2, map).ShouldBeNull();
        camera.TileAt(width / 2, height + 1, map).ShouldBeNull();
    }

    [TestCaseSource(nameof(EveryPlayArea))]
    public void ScaleAt_ANearerAndAFartherPlace_ShouldBeLargerAndSmallerThanWhereThePlayerStands(double width, double height)
    {
        var camera = PerspectiveCamera.Follow(width, height, 30.5 * 32, 24.5 * 32, Garden());

        var here = camera.ScaleAt(30.5 * 32, 24.5 * 32);
        var bottom = camera.GroundAt(width / 2, height).ShouldNotBeNull();
        var haze = camera.GroundAt(width / 2, camera.HazeLine).ShouldNotBeNull();
        var nearer = camera.ScaleAt(bottom.X, bottom.Y);
        var farther = camera.ScaleAt(haze.X, haze.Y);

        here.ShouldBe(camera.Scale, 1e-9);
        nearer.ShouldBeGreaterThan(here * 1.25, "at the bottom edge things are a quarter larger than where the player stands");
        farther.ShouldBeLessThan(here * 0.8, "where the haze closes they are a fifth smaller");
        camera.ScaleAt(30.5 * 32, 25.5 * 32).ShouldBeGreaterThan(here);
        camera.ScaleAt(30.5 * 32, 23.5 * 32).ShouldBeLessThan(here);
    }

    [TestCaseSource(nameof(EveryPlayArea))]
    public void Project_TwoPostsOneBehindTheOther_ShouldMoveTheNearerOneFartherAcrossTheScreenWhenTheCameraSlides(double width, double height)
    {
        var map = Garden();
        var before = PerspectiveCamera.Follow(width, height, 30.5 * 32, 24.5 * 32, map);
        var after = PerspectiveCamera.Follow(width, height, 31.5 * 32, 24.5 * 32, map);

        var bottom = before.GroundAt(width / 2, height - 1).ShouldNotBeNull();
        var haze = before.GroundAt(width / 2, before.HazeLine).ShouldNotBeNull();

        var near = before.Project(bottom.X, bottom.Y).X - after.Project(bottom.X, bottom.Y).X;
        var middle = before.Project(30.5 * 32, 24.5 * 32).X - after.Project(30.5 * 32, 24.5 * 32).X;
        var far = before.Project(haze.X, haze.Y).X - after.Project(haze.X, haze.Y).X;

        near.ShouldBeGreaterThan(middle * 1.25);
        middle.ShouldBeGreaterThan(far * 1.25);
        far.ShouldBeGreaterThan(0);
    }

    [TestCaseSource(nameof(EveryPlayArea))]
    public void Follow_APlayerInACornerOfTheMap_ShouldStopAtTheEdgeOfTheMap(double width, double height)
    {
        var map = Garden();
        var size = map.TileSize;

        var northWest = PerspectiveCamera.Follow(width, height, 0, 0, map);
        var southEast = PerspectiveCamera.Follow(width, height, map.Width * size, map.Height * size, map);

        northWest.GroundAt(0, height / 2).ShouldNotBeNull().X.ShouldBe(0, 1e-6);
        northWest.FocusY.ShouldBe(0, "to the north the camera goes to the edge itself: the haze closes before the picture ends");
        southEast.GroundAt(width, height / 2).ShouldNotBeNull().X.ShouldBe(map.Width * size, 1e-6);
        southEast.GroundAt(width / 2, height).ShouldNotBeNull().Y.ShouldBe(map.Height * size, 1e-6);
        southEast.GroundAt(0, height).ShouldNotBeNull().X.ShouldBeGreaterThan(0);
        southEast.GroundAt(width, height).ShouldNotBeNull().X.ShouldBeLessThan(map.Width * size);
    }

    [TestCaseSource(nameof(EveryPlayArea))]
    public void Follow_EveryTileAPlayerCanStandOn_ShouldKeepThePlayerInsideThePlayArea(double width, double height)
    {
        var map = Garden();

        for (var row = 0; row < map.Height; row++)
        {
            for (var column = 0; column < map.Width; column++)
            {
                if (!map.IsWalkable(new TilePos(column, row)))
                {
                    continue;
                }

                var camera = PerspectiveCamera.Follow(width, height, (column + 0.5) * 32, (row + 0.5) * 32, map);
                var (x, y) = camera.Project((column + 0.5) * 32, (row + 0.5) * 32);
                var head = camera.Project((column + 0.5) * 32, (row + 0.5) * 32, 48);

                x.ShouldBeInRange(0, width, $"tile {column},{row}");
                y.ShouldBeInRange(camera.HazeLine, height, $"tile {column},{row}");
                head.Y.ShouldBeGreaterThan(0, $"tile {column},{row}");
                camera.DepthOf((column + 0.5) * 32, (row + 0.5) * 32).ShouldBeLessThan(camera.HazeStart, $"tile {column},{row}: the player is never in the haze");
            }
        }
    }

    [TestCaseSource(nameof(EveryPlayArea))]
    public void VisibleRowsAndColumns_EveryTreeThatReachesIntoThePlayAreaThisSideOfTheHaze_ShouldBeListed(double width, double height)
    {
        var map = Garden();
        const double TallestTree = 75;
        const double WidestCrown = 27;

        foreach (var focus in new[] { new TilePos(30, 24), new TilePos(3, 17), new TilePos(27, 4), new TilePos(42, 32), new TilePos(22, 20) })
        {
            var camera = PerspectiveCamera.Follow(width, height, (focus.X + 0.5) * 32, (focus.Y + 0.5) * 32, map);
            var (firstRow, lastRow) = camera.VisibleRows(map);

            for (var row = 0; row < map.Height; row++)
            {
                var (firstColumn, lastColumn) = camera.VisibleColumns(row, map);
                for (var column = 0; column < map.Width; column++)
                {
                    // A tree as a box about its trunk: is any corner of it inside the play area and nearer than the haze's end?
                    var footX = (column + 0.5) * 32;
                    var footY = ((row + 1) * 32) - 6;
                    var reaches = false;
                    foreach (var dx in new[] { -WidestCrown, 0, WidestCrown })
                    {
                        foreach (var up in new[] { 0, TallestTree / 2, TallestTree })
                        {
                            var depth = camera.DepthOf(footX + dx, footY, up);
                            var (x, y) = camera.Project(footX + dx, footY, up);
                            reaches |= depth > 0 && depth < camera.HazeEnd && x >= 0 && x <= width && y >= 0 && y <= height;
                        }
                    }

                    if (reaches)
                    {
                        row.ShouldBeInRange(firstRow, lastRow, $"focus {focus}, tile {column},{row}");
                        column.ShouldBeInRange(firstColumn, lastColumn, $"focus {focus}, tile {column},{row}");
                    }
                }
            }

            (lastRow - firstRow).ShouldBeLessThan(map.Height, "the camera does not list the whole map");
        }
    }

    [TestCaseSource(nameof(EveryPlayArea))]
    public void Haze_TheLineWhereItCloses_ShouldLieAboveThePlayerAndTheGroundThereShouldHaveTheDepthOfItsEnd(double width, double height)
    {
        var camera = PerspectiveCamera.Follow(width, height, 30.5 * 32, 24.5 * 32, Garden());

        var line = camera.GroundAt(width / 2, camera.HazeLine).ShouldNotBeNull();
        var corner = camera.GroundAt(0, camera.HazeLine).ShouldNotBeNull();

        camera.HazeLine.ShouldBe(height * PerspectiveCamera.HazeLineShare);
        camera.DepthOf(line.X, line.Y).ShouldBe(camera.HazeEnd, 1e-6);
        camera.DepthOf(corner.X, corner.Y).ShouldBe(camera.HazeEnd, 1e-6, "the haze closes along one straight line of the screen");
        camera.HazeStart.ShouldBeGreaterThan(camera.Distance);
        camera.HazeEnd.ShouldBeGreaterThan(camera.HazeStart * 1.1);
    }

    [Test]
    public void Camera_ItsDirection_ShouldBeFixedLookingDownAndNeverAtTheHorizon()
    {
        PerspectiveCamera.TiltDegrees.ShouldBeInRange(30, 60);
        (PerspectiveCamera.TiltDegrees - (PerspectiveCamera.FieldOfViewDegrees / 2)).ShouldBeGreaterThan(10, "every point of the play area shows ground: a tap always names a place");
        PerspectiveCamera.TiltRadians.ShouldBe(PerspectiveCamera.TiltDegrees * Math.PI / 180);
        PerspectiveCamera.FieldOfViewRadians.ShouldBe(PerspectiveCamera.FieldOfViewDegrees * Math.PI / 180);
        typeof(PerspectiveCamera).GetProperties().Select(property => property.Name).ShouldNotContain("Yaw");
        typeof(PerspectiveCamera).GetProperties().Select(property => property.Name).ShouldNotContain("Zoom");
    }

    // What rule M1 rests on under perspective: the plane of a figure faces the camera squarely, so the camera maps it
    // to the screen by one scale and a shift, and nothing else. The figure on the screen is the figure the check
    // judged: its parts keep their places and their order, and the plane is never seen edge-on.
    [TestCaseSource(nameof(EveryPlayArea))]
    public void Project_EveryPointOfEveryPartOfBothFiguresWhereverTheyStand_ShouldBeTheFigureOfTheCheckAtOneScale(double width, double height)
    {
        var garden = StubMap.Shipped();
        var camera = PerspectiveCamera.Follow(width, height, 30.5 * 32, 24.5 * 32, garden.Map);
        var walk = garden.Animations.Single(animation => animation.Id == "walk");
        (double X, double Y)[] corners = [(-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5), (0, 0)];

        foreach (var rig in new[] { garden.Adam, garden.Woman })
        {
            var pose = new RigPose(rig);
            foreach (var facing in Facings.All)
            {
                pose.Sample(walk, 0.1, facing, Covering.None);
                foreach (var feet in new[] { (X: 30.5 * 32, Y: (24.5 * 32) + 8), (X: 24.5 * 32, Y: 27.5 * 32), (X: 37.0 * 32, Y: 20.25 * 32) })
                {
                    var scale = camera.ScaleAt(feet.X, feet.Y);
                    var anchor = camera.Project(feet.X, feet.Y);
                    var depth = camera.DepthOf(feet.X, feet.Y);
                    foreach (var placed in pose.Parts)
                    {
                        var part = rig.Parts[placed.PartIndex];
                        foreach (var corner in corners)
                        {
                            // The point in the figure's own flat space, as the check sees it: x to the right, y downward, from the feet.
                            var flatX = placed.Transform.ApplyX(corner.X * part.Width, corner.Y * part.Height);
                            var flatY = placed.Transform.ApplyY(corner.X * part.Width, corner.Y * part.Height);

                            var inSpace = PerspectiveCamera.FigurePoint(feet.X, feet.Y, flatX, -flatY);
                            var (x, y) = camera.Project(inSpace.X, inSpace.Y, inSpace.Height);

                            x.ShouldBe(anchor.X + (scale * flatX), 1e-6);
                            y.ShouldBe(anchor.Y + (scale * flatY), 1e-6);
                            camera.DepthOf(inSpace.X, inSpace.Y, inSpace.Height).ShouldBe(depth, 1e-6, "every point of a figure lies at one depth: its plane is parallel to the picture");
                        }
                    }

                    scale.ShouldBeGreaterThan(0.2);
                }
            }
        }
    }

    [TestCaseSource(nameof(EveryPlayArea))]
    public void FigureDepth_AFigureAndATreeOneTileNorthAndOneTileSouthOfIt_ShouldStandBeforeTheOneAndBehindTheCrownOfTheOther(double width, double height)
    {
        var camera = PerspectiveCamera.Follow(width, height, 30.5 * 32, 24.5 * 32, Garden());
        var feetY = (24.5 * 32) + 8;

        var figure = camera.DepthOf(30.5 * 32, feetY, PerspectiveCamera.FigureDepthHeight);
        var crownToTheNorth = camera.DepthOf(30.5 * 32, (24 * 32) - 6 + 11.4, 34);
        var crownToTheSouth = camera.DepthOf(30.5 * 32, (26 * 32) - 6 + 11.4, 34);

        figure.ShouldBeLessThan(crownToTheNorth);
        figure.ShouldBeGreaterThan(crownToTheSouth);
        figure.ShouldBeLessThan(camera.DepthOf(30.5 * 32, feetY));
    }
}
