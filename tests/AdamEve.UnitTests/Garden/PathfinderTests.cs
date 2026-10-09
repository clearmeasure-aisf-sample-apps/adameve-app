using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

[TestFixture]
public class PathfinderTests
{
    [Test]
    public void FindPath_ATileAcrossTheWater_ShouldGoAroundTheWaterOverTheCrossing()
    {
        var map = StubMap.FromRows(
            "..~..",
            "..~..",
            "..~..",
            "..=..",
            "..~..");

        var path = Pathfinder.FindPath(map, new TilePos(1, 0), new TilePos(3, 0), null);

        path.ShouldNotBeEmpty();
        path[^1].ShouldBe(new TilePos(3, 0));
        path.ShouldContain(new TilePos(2, 3));
        path.ShouldAllBe(tile => map.IsWalkable(tile));
    }

    [Test]
    public void FindPath_EveryStep_ShouldBeToANeighbourAndNeverCutACorner()
    {
        var map = StubMap.FromRows(
            ".....",
            ".T~..",
            "..T..",
            ".....");
        var start = new TilePos(0, 0);

        var path = Pathfinder.FindPath(map, start, new TilePos(4, 3), null);

        var from = start;
        foreach (var tile in path)
        {
            Math.Abs(tile.X - from.X).ShouldBeLessThanOrEqualTo(1);
            Math.Abs(tile.Y - from.Y).ShouldBeLessThanOrEqualTo(1);
            Pathfinder.CanStep(map, from, tile.X - from.X, tile.Y - from.Y, null).ShouldBeTrue();
            from = tile;
        }

        from.ShouldBe(new TilePos(4, 3));
    }

    [Test]
    public void FindPath_AnOpenField_ShouldTakeTheDiagonal()
    {
        var map = StubMap.FromRows("....", "....", "....", "....");

        var path = Pathfinder.FindPath(map, new TilePos(0, 0), new TilePos(3, 3), null);

        path.ShouldBe([new TilePos(1, 1), new TilePos(2, 2), new TilePos(3, 3)]);
    }

    [TestCase(2, 0, Description = "water")]
    [TestCase(4, 0, Description = "beyond water without a crossing")]
    [TestCase(9, 9, Description = "outside the map")]
    [TestCase(0, 0, Description = "the tile the character stands on")]
    public void FindPath_ATileThatCannotBeReached_ShouldBeEmpty(int x, int y)
    {
        var map = StubMap.FromRows(
            "..~..",
            "..~..");

        var path = Pathfinder.FindPath(map, new TilePos(0, 0), new TilePos(x, y), null);

        path.ShouldBeEmpty();
    }

    [Test]
    public void FindPath_ATileAnotherCharacterStandsOn_ShouldGoAroundIt()
    {
        var map = StubMap.FromRows(
            "#.#",
            "...",
            "#.#");
        var other = new TilePos(1, 1);

        var blocked = Pathfinder.FindPath(map, new TilePos(1, 0), new TilePos(1, 2), other);
        var open = Pathfinder.FindPath(map, new TilePos(1, 0), new TilePos(1, 2), null);

        blocked.ShouldBeEmpty();
        open.ShouldBe([new TilePos(1, 1), new TilePos(1, 2)]);
    }

    [TestCase(1, 1, false, Description = "a diagonal between a tree and water")]
    [TestCase(1, 0, false, Description = "into a tree")]
    [TestCase(0, 1, true, Description = "onto grass")]
    [TestCase(0, 0, false, Description = "no step")]
    public void CanStep_OneStep_ShouldFollowTheMap(int dx, int dy, bool expected)
    {
        var map = StubMap.FromRows(
            ".T",
            "..");

        var allowed = Pathfinder.CanStep(map, new TilePos(0, 0), dx, dy, null);

        allowed.ShouldBe(expected);
    }
}
