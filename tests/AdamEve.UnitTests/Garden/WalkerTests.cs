using System.Numerics;
using AdamEve.Core.Game;
using AdamEve.Core.Input;
using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

[TestFixture]
public class WalkerTests
{
    private static readonly InputSnapshot Nothing = new(Vector2.Zero, false, false, null);

    private static InputSnapshot Direction(int x, int y) => new(new Vector2(x, y), false, false, null);

    private static int StepsUntilArrival(Walker walker, InputSnapshot input)
    {
        var steps = 1;
        walker.Step(input, GameLoop.StepSeconds);
        while (walker.Moving)
        {
            walker.Step(Nothing, GameLoop.StepSeconds);
            steps++;
        }

        return steps;
    }

    [Test]
    public void Step_ADirectionPressedOnce_ShouldWalkExactlyOneTileAndStop()
    {
        var walker = new Walker(StubMap.FromRows("....."), new TilePos(1, 0), Facing.S);

        var steps = StepsUntilArrival(walker, Direction(1, 0));

        walker.Tile.ShouldBe(new TilePos(2, 0));
        walker.Facing.ShouldBe(Facing.E);
        walker.Moving.ShouldBeFalse();
        steps.ShouldBe((int)Math.Round(60 / Walker.TilesPerSecond));
    }

    [Test]
    public void Step_ADiagonal_ShouldTakeLongerByTheSquareRootOfTwoSoTheSpeedIsTheSame()
    {
        var map = StubMap.FromRows("...", "...", "...");
        var straight = StepsUntilArrival(new Walker(map, new TilePos(0, 0), Facing.S), Direction(1, 0));

        var diagonal = StepsUntilArrival(new Walker(map, new TilePos(0, 0), Facing.S), Direction(1, 1));

        var straightSpeed = 1 / (straight * GameLoop.StepSeconds);
        var diagonalSpeed = Math.Sqrt(2) / (diagonal * GameLoop.StepSeconds);
        diagonalSpeed.ShouldBe(straightSpeed, straightSpeed * 0.05);
        diagonal.ShouldBeGreaterThan(straight);
    }

    [TestCase(0, -1, Facing.N)]
    [TestCase(1, -1, Facing.NE)]
    [TestCase(1, 0, Facing.E)]
    [TestCase(1, 1, Facing.SE)]
    [TestCase(0, 1, Facing.S)]
    [TestCase(-1, 1, Facing.SW)]
    [TestCase(-1, 0, Facing.W)]
    [TestCase(-1, -1, Facing.NW)]
    public void Step_EachOfTheEightDirections_ShouldWalkAndFaceThatWay(int dx, int dy, Facing facing)
    {
        var walker = new Walker(StubMap.FromRows("...", "...", "..."), new TilePos(1, 1), Facing.S);

        StepsUntilArrival(walker, Direction(dx, dy));

        walker.Tile.ShouldBe(new TilePos(1 + dx, 1 + dy));
        walker.Facing.ShouldBe(facing);
    }

    [Test]
    public void Step_IntoWater_ShouldTurnAndStay()
    {
        var walker = new Walker(StubMap.FromRows(".~."), new TilePos(0, 0), Facing.S);

        var arrived = walker.Step(Direction(1, 0), GameLoop.StepSeconds);

        arrived.ShouldBeFalse();
        walker.Moving.ShouldBeFalse();
        walker.Tile.ShouldBe(new TilePos(0, 0));
        walker.Facing.ShouldBe(Facing.E);
    }

    [Test]
    public void Step_ADiagonalAlongWater_ShouldSlideAlongIt()
    {
        var walker = new Walker(StubMap.FromRows("..", "~~"), new TilePos(0, 0), Facing.S);

        StepsUntilArrival(walker, Direction(1, 1));

        walker.Tile.ShouldBe(new TilePos(1, 0));
    }

    [Test]
    public void Step_ATapAcrossTheWater_ShouldWalkAroundItAndArrive()
    {
        var map = StubMap.FromRows(
            "..~..",
            "..~..",
            "..=..");
        var walker = new Walker(map, new TilePos(1, 0), Facing.S);
        var arrivals = 0;

        walker.Step(new InputSnapshot(Vector2.Zero, false, false, new TilePos(3, 0)), GameLoop.StepSeconds);
        for (var step = 0; step < 600 && walker.Tile != new TilePos(3, 0); step++)
        {
            map.IsWalkable(walker.Tile).ShouldBeTrue();
            arrivals += walker.Step(Nothing, GameLoop.StepSeconds) ? 1 : 0;
        }

        walker.Tile.ShouldBe(new TilePos(3, 0));
        walker.Moving.ShouldBeFalse();
        arrivals.ShouldBe(1);
    }

    [Test]
    public void Step_ADirectionDuringATapWalk_ShouldEndTheTapWalkAtTheNextTile()
    {
        var walker = new Walker(StubMap.FromRows("......", "......"), new TilePos(0, 0), Facing.S);
        walker.Step(new InputSnapshot(Vector2.Zero, false, false, new TilePos(5, 0)), GameLoop.StepSeconds);

        walker.Step(Direction(0, 1), GameLoop.StepSeconds);
        for (var step = 0; step < 120; step++)
        {
            walker.Step(Nothing, GameLoop.StepSeconds);
        }

        walker.Tile.ShouldBe(new TilePos(1, 0));
        walker.Moving.ShouldBeFalse();
    }

    [Test]
    public void Step_TowardATileAnotherCharacterStandsOn_ShouldNotStepOnIt()
    {
        var walker = new Walker(StubMap.FromRows("..."), new TilePos(0, 0), Facing.S) { Obstacle = new TilePos(1, 0) };

        walker.Step(Direction(1, 0), GameLoop.StepSeconds);

        walker.Moving.ShouldBeFalse();
        walker.Tile.ShouldBe(new TilePos(0, 0));
    }

    [Test]
    public void Step_BetweenTwoTiles_ShouldKeepThePlaceBeforeTheStepForInterpolation()
    {
        var walker = new Walker(StubMap.FromRows("..."), new TilePos(0, 0), Facing.S);
        walker.Step(Direction(1, 0), GameLoop.StepSeconds);
        var after = walker.X;

        walker.Step(Nothing, GameLoop.StepSeconds);

        walker.PreviousX.ShouldBe(after);
        walker.X.ShouldBeGreaterThan(after);
        walker.X.ShouldBeLessThan(1);
    }
}
