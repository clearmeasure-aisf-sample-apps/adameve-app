using AdamEve.Core.Game;
using AdamEve.Core.Rigs;
using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

/// <summary>
/// The planting of the garden (design, sections 5.1 and 5.2): a fact of the world, made from the map and a fixed
/// seed. It may not change a rule: whatever has height stands where nobody can walk, the crossings stay clear, and
/// the two trees in the midst of the garden stand where the map puts them.
/// </summary>
[TestFixture]
public class GardenSceneryTests
{
    private static readonly SceneryKind[] PlantedTrees = [SceneryKind.BroadTree, SceneryKind.TallTree, SceneryKind.FruitTree, SceneryKind.PalmTree];

    private static IEnumerable<TilePos> Tiles(TileMap map) =>
        Enumerable.Range(0, map.Width * map.Height).Select(index => new TilePos(index % map.Width, index / map.Width));

    [Test]
    public void KindAt_TheSameMapTwice_ShouldPlantTheSameGarden()
    {
        var map = StubMap.Shipped().Map;
        var first = new GardenScenery(map);
        var second = new GardenScenery(StubMap.Shipped().Map);

        Tiles(map).ShouldAllBe(tile => first.KindAt(tile) == second.KindAt(tile) && first.CoverAt(tile) == second.CoverAt(tile));
        first.ToCoverNumbers().ShouldBe(second.ToCoverNumbers());
        GardenScenery.Seed.ShouldBe(20261010);
        GardenScenery.Chance(3, 5, 1).ShouldBe(GardenScenery.Chance(3, 5, 1));
        GardenScenery.Chance(3, 5, 1).ShouldBeInRange(0, 1);
    }

    [Test]
    public void KindAt_EveryTileACharacterCanWalkOn_ShouldHaveNothingStandingOnIt()
    {
        var map = StubMap.Shipped().Map;
        var scenery = new GardenScenery(map);

        Tiles(map).Where(map.IsWalkable).ShouldAllBe(tile => scenery.KindAt(tile) == SceneryKind.None);
        scenery.KindAt(new TilePos(-1, 0)).ShouldBe(SceneryKind.None);
        scenery.KindAt(new TilePos(0, map.Height)).ShouldBe(SceneryKind.None);
    }

    [Test]
    public void KindAt_TheTwoTreesInTheMidstOfTheGardenAndTheFigTree_ShouldStandOnceEachWhereTheMapPutsThemInTheCentralGlade()
    {
        var map = StubMap.Shipped().Map;
        var scenery = new GardenScenery(map);
        var glade = map.Regions.Single(region => region.Id == "central-glade");

        foreach (var (kind, tile) in new[] { (SceneryKind.TreeOfLife, TileKind.TreeOfLife), (SceneryKind.TreeOfKnowledge, TileKind.TreeOfKnowledge), (SceneryKind.FigTree, TileKind.FigTree) })
        {
            var where = Tiles(map).Where(each => scenery.KindAt(each) == kind).ToList();

            where.Count.ShouldBe(1, kind.ToString());
            map.KindAt(where[0]).ShouldBe(tile);
            glade.Contains(where[0]).ShouldBeTrue(kind.ToString());
            map.IsWalkable(where[0]).ShouldBeFalse("the tree of life is shown and is not interactive (decision D14): nobody stands on either tree");
        }
    }

    [Test]
    public void KindAt_EveryTreeTile_ShouldBearOneOfTheFourKindsOfTreeAndAllFourShouldGrowInTheGarden()
    {
        var map = StubMap.Shipped().Map;
        var scenery = new GardenScenery(map);

        var planted = Tiles(map).Where(tile => map.KindAt(tile) == TileKind.Tree).Select(scenery.KindAt).ToList();

        planted.ShouldAllBe(kind => PlantedTrees.Contains(kind));
        foreach (var kind in PlantedTrees)
        {
            planted.Count(each => each == kind).ShouldBeGreaterThan(1, kind.ToString());
        }

        Tiles(map).Where(tile => PlantedTrees.Contains(scenery.KindAt(tile))).ShouldAllBe(tile => map.KindAt(tile) == TileKind.Tree);
    }

    [Test]
    public void KindAt_TheThicketAndTheRiver_ShouldBearShrubsRocksReedsAndForestOnlyWhereTheyBelongAndLeaveTheCrossingsClear()
    {
        var map = StubMap.Shipped().Map;
        var scenery = new GardenScenery(map);
        (int X, int Y)[] beside = [(0, -1), (-1, 0), (1, 0), (0, 1)];

        foreach (var tile in Tiles(map))
        {
            var kind = scenery.KindAt(tile);
            var ground = map.KindAt(tile);
            switch (kind)
            {
                case SceneryKind.Shrub or SceneryKind.ForestTree:
                    ground.ShouldBe(TileKind.Thicket, $"{kind} at {tile}");
                    break;
                case SceneryKind.Reeds:
                    ground.ShouldBe(TileKind.Water, $"{kind} at {tile}");
                    break;
                case SceneryKind.Rock:
                    ground.ShouldBeOneOf(TileKind.Thicket, TileKind.Water);
                    break;
            }

            if (ground == TileKind.Crossing)
            {
                kind.ShouldBe(SceneryKind.None);
                beside.ShouldAllBe(step => scenery.KindAt(new TilePos(tile.X + step.X, tile.Y + step.Y)) == SceneryKind.None, $"beside the crossing at {tile}");
            }
        }

        foreach (var kind in new[] { SceneryKind.Shrub, SceneryKind.ForestTree, SceneryKind.Reeds, SceneryKind.Rock })
        {
            Tiles(map).Count(tile => scenery.KindAt(tile) == kind).ShouldBeGreaterThan(5, kind.ToString());
        }
    }

    [Test]
    public void CoverAt_TheFlowers_ShouldLieThickOnEveryFlowerTileThinBesideItAndNowhereElseInDriftsOfOneColour()
    {
        var map = StubMap.Shipped().Map;
        var scenery = new GardenScenery(map);
        var sameAsNeighbour = 0;
        var pairs = 0;

        foreach (var tile in Tiles(map))
        {
            var cover = scenery.CoverAt(tile);
            var ground = map.KindAt(tile);
            if (ground == TileKind.Flowers)
            {
                (cover & GardenScenery.Dense).ShouldBe(GardenScenery.Dense, tile.ToString());
            }
            else if (cover != 0)
            {
                ground.ShouldBe(TileKind.Grass, tile.ToString());
                (cover & GardenScenery.Dense).ShouldBe(0);
                Enumerable.Range(-1, 3).SelectMany(dy => Enumerable.Range(-1, 3).Select(dx => map.KindAt(new TilePos(tile.X + dx, tile.Y + dy)))).ShouldContain(TileKind.Flowers);
            }

            (cover & 7).ShouldBeInRange(0, GardenScenery.DriftColours);
            if (cover != 0 && scenery.CoverAt(new TilePos(tile.X + 1, tile.Y)) is var next and not 0)
            {
                pairs++;
                sameAsNeighbour += (next & 7) == (cover & 7) ? 1 : 0;
            }
        }

        scenery.ToCoverNumbers().Select(cover => cover & 7).Where(colour => colour != 0).Distinct().Count().ShouldBeGreaterThanOrEqualTo(4, "the garden has drifts of several colours");
        ((double)sameAsNeighbour / pairs).ShouldBeGreaterThan(0.7, "neighbouring flowers are mostly of one colour: drifts, not confetti");
        PlaceholderArt.DriftColours.Count.ShouldBe(GardenScenery.DriftColours);
    }

    [Test]
    public void SpriteOf_EveryKindOfScenery_ShouldBeFlatShapesAboutItsFootAndNothingForNone()
    {
        foreach (var kind in Enum.GetValues<SceneryKind>().Where(kind => kind != SceneryKind.None))
        {
            var shapes = PlaceholderArt.SpriteOf(kind);

            shapes.ShouldNotBeEmpty(kind.ToString());
            shapes.ShouldAllBe(shape => shape.Width > 0 && shape.Height > 0 && shape.Y < 0 && Math.Abs(shape.X) <= 48 && shape.Shape != PartShape.Rounded);
        }

        PlaceholderArt.SpriteOf(SceneryKind.None).ShouldBeEmpty();
        var life = PlaceholderArt.SpriteOf(SceneryKind.TreeOfLife);
        var knowledge = PlaceholderArt.SpriteOf(SceneryKind.TreeOfKnowledge);
        life.Select(shape => shape.Colour).Intersect(knowledge.Select(shape => shape.Colour)).ShouldBeEmpty("the two trees must never be confused: they share no colour");
        life.Min(shape => shape.Y - (shape.Height / 2)).ShouldBeLessThan(PlaceholderArt.SpriteOf(SceneryKind.BroadTree).Min(shape => shape.Y - (shape.Height / 2)), "the tree of life is the tallest");
    }

    [Test]
    public void GroundOf_EveryTileKind_ShouldShadeBetweenTwoColoursOfOneKindOfGround()
    {
        foreach (var kind in Enum.GetValues<TileKind>())
        {
            var style = PlaceholderArt.GroundOf(kind);

            style.Colour.ShouldNotBe(style.AlternateColour, kind.ToString());
        }

        PlaceholderArt.GroundOf(TileKind.Flowers).ShouldBe(PlaceholderArt.GroundOf(TileKind.Grass), "flowers lie on grass: their colour is that of their drift");
        PlaceholderArt.GroundOf(TileKind.Crossing).MarkSize.ShouldBeGreaterThan(0);
    }
}
