using AdamEve.Core.Game;
using AdamEve.Core.Input;
using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

[TestFixture]
public class CameraAndLoopTests
{
    private static TileMap Garden() => StubMap.Shipped().Map;

    [TestCase(1366, 768, 15, 8.4, Description = "desktop, landscape")]
    [TestCase(915, 412, 15, 6.8, Description = "Pixel 7, landscape")]
    [TestCase(412, 660, 9, 14.4, Description = "Pixel 7, portrait, above the controls")]
    [TestCase(360, 480, 9, 12, Description = "the smallest size, portrait, above the controls")]
    public void Follow_APlayArea_ShouldShowAbout15TilesAcrossInLandscapeAnd9InPortrait(double width, double height, double across, double down)
    {
        var map = Garden();

        var camera = Camera.Follow(width, height, 30 * 32, 24 * 32, map);

        (width / camera.Scale / map.TileSize).ShouldBe(across, 0.1);
        (height / camera.Scale / map.TileSize).ShouldBe(down, 0.1);
    }

    [Test]
    public void Follow_AnyPlayArea_ShouldCentreOnThePlayer()
    {
        var map = Garden();

        var camera = Camera.Follow(800, 450, 30.5 * 32, 24.5 * 32, map);

        camera.TileAt(400, 225, map).ShouldBe(new TilePos(30, 24));
    }

    [Test]
    public void Follow_APlayerInACornerOfTheMap_ShouldStopAtTheEdgeOfTheMap()
    {
        var map = Garden();

        var northWest = Camera.Follow(800, 450, 0, 0, map);
        var southEast = Camera.Follow(800, 450, map.Width * 32, map.Height * 32, map);

        northWest.X.ShouldBe(0);
        northWest.Y.ShouldBe(0);
        (southEast.X + (800 / southEast.Scale)).ShouldBe(map.Width * 32, 1e-6);
        (southEast.Y + (450 / southEast.Scale)).ShouldBe(map.Height * 32, 1e-6);
    }

    [Test]
    public void TileAt_APointOutsideThePlayArea_ShouldBeNoTile()
    {
        var map = Garden();
        var camera = Camera.Follow(800, 450, 30 * 32, 24 * 32, map);

        camera.TileAt(-1, 10, map).ShouldBeNull();
        camera.TileAt(10, 451, map).ShouldBeNull();
    }

    [Test]
    public void Follow_ARotation_ShouldKeepThePlayerInTheMiddle()
    {
        var map = Garden();

        var portrait = Camera.Follow(412, 660, 26.5 * 32, 22.5 * 32, map);
        var landscape = Camera.Follow(915, 412, 26.5 * 32, 22.5 * 32, map);

        portrait.TileAt(206, 330, map).ShouldBe(new TilePos(26, 22));
        landscape.TileAt(457.5, 206, map).ShouldBe(new TilePos(26, 22));
    }

    [Test]
    public void Advance_FramesAtSixtyAndAtThirtyASecond_ShouldRunTheSameStepsOfTheWorld()
    {
        var sixty = new GameLoop();
        var thirty = new GameLoop();
        var stepsAtSixty = 0;
        var stepsAtThirty = 0;

        for (var frame = 0; frame <= 60; frame++)
        {
            stepsAtSixty += sixty.Advance(frame * 1000.0 / 60);
        }

        for (var frame = 0; frame <= 30; frame++)
        {
            stepsAtThirty += thirty.Advance(frame * 1000.0 / 30);
        }

        stepsAtSixty.ShouldBe(60);
        stepsAtThirty.ShouldBe(60);
    }

    [Test]
    public void Advance_ATabThatWasHidden_ShouldNotMakeTheWorldJump()
    {
        var loop = new GameLoop();
        loop.Advance(0);

        var steps = loop.Advance(60_000);

        steps.ShouldBe((int)Math.Round(GameLoop.LongestFrameSeconds / GameLoop.StepSeconds));
    }

    [Test]
    public void Advance_AFrameBetweenTwoSteps_ShouldSayHowFarBetween()
    {
        var loop = new GameLoop();
        loop.Advance(0);

        var steps = loop.Advance(25);

        steps.ShouldBe(1);
        loop.Alpha.ShouldBe(0.5, 0.01);
    }

    [TestCase(InputBlock.Up, 0, -1)]
    [TestCase(InputBlock.Down | InputBlock.Right, 1, 1)]
    [TestCase(InputBlock.Left | InputBlock.Right, 0, 0)]
    [TestCase(InputBlock.Left | InputBlock.Up, -1, -1)]
    public void Direction_DirectionBits_ShouldBeTheDirectionAsked(int bits, int x, int y)
    {
        var direction = InputBlock.Direction(bits);

        direction.X.ShouldBe(x);
        direction.Y.ShouldBe(y);
    }
}
