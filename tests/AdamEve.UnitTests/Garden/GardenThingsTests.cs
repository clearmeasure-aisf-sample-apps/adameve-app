using AdamEve.Core.Game;
using AdamEve.Core.Story;
using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

/// <summary>The things of the garden that are not scenery and not a person, and the places of the man's path.</summary>
[TestFixture]
public class GardenThingsTests
{
    [Test]
    public void Write_Things_ShouldListThemAfterTheEntriesTheFarthestFirst()
    {
        var things = new GardenThings();
        var list = new double[RenderList.Length];
        things.Place(new GardenThing("near", 3, 5, 9));
        things.Place(new GardenThing("far", 7, 6, 2) { LooksWest = true });

        things.Write(list, 32, 8, 0);

        list[RenderList.ThingCount].ShouldBe(2);
        list[RenderList.Things + RenderList.ThingArt].ShouldBe(7);
        list[RenderList.Things + RenderList.ThingX].ShouldBe(6.5 * 32);
        list[RenderList.Things + RenderList.ThingY].ShouldBe((2.5 * 32) + 8);
        list[RenderList.Things + RenderList.ThingLooks].ShouldBe(-1);
        list[RenderList.Things + RenderList.EntryLength + RenderList.ThingArt].ShouldBe(3);
        list[RenderList.Things + RenderList.EntryLength + RenderList.ThingLooks].ShouldBe(1);
        list[RenderList.Count].ShouldBe(0, "the entries of the list are not touched");
        RenderList.Things.ShouldBe(RenderList.HeaderLength + (RenderList.Capacity * RenderList.EntryLength));
        RenderList.Length.ShouldBe(RenderList.Things + (RenderList.ThingCapacity * RenderList.EntryLength));
        RenderList.ThingCount.ShouldBeLessThan(RenderList.HeaderLength);
    }

    [Test]
    public void Write_MoreThingsThanThereIsRoomFor_ShouldListOnlyAsManyAsFit()
    {
        var things = new GardenThings();
        var list = new double[RenderList.Length];
        for (var index = 0; index < RenderList.ThingCapacity + 5; index++)
        {
            things.Place(new GardenThing($"stub-{index}", 0, index, index));
        }

        things.Write(list, 32, 8, 0);

        list[RenderList.ThingCount].ShouldBe(RenderList.ThingCapacity);
    }

    [Test]
    public void Advance_AThingWithAnotherPlaceToGoTo_ShouldWalkThereTurnedTheWayItGoesAndThenStand()
    {
        var things = new GardenThings();
        var thing = new GardenThing("stub", 0, 10, 4) { TargetX = 6, TargetY = 4 };
        var list = new double[RenderList.Length];
        things.Place(thing);

        things.Advance(0.2);
        things.Write(list, 32, 8, 0.1);
        var lifted = list[RenderList.Things + RenderList.ThingLift];
        var between = thing.X;
        things.Advance(5);
        things.Write(list, 32, 8, 0.1);

        between.ShouldBe(9, 0.001);
        lifted.ShouldBeGreaterThan(0);
        thing.LooksWest.ShouldBeTrue();
        thing.X.ShouldBe(6);
        thing.Moving.ShouldBeFalse();
        list[RenderList.Things + RenderList.ThingLift].ShouldBe(0);
    }

    [Test]
    public void Place_AThingWithAKeyAlreadyThere_ShouldReplaceItAndRemoveShouldTakeItAway()
    {
        var things = new GardenThings();
        things.Place(new GardenThing("stub", 1, 0, 0));
        things.Place(new GardenThing("stub", 2, 0, 0));

        things.Items.Count.ShouldBe(1);
        things.Find("stub")!.Art.ShouldBe(2);
        things.Remove("stub");
        things.Find("stub").ShouldBeNull();
    }

    [Test]
    public void StoryPlaces_TheShippedGarden_ShouldPutEveryThingOnOpenGroundOfTheGladeAndTwoTilesForEachOfThe24Animals()
    {
        var map = StubMap.Shipped().Map;
        var glade = map.Regions.Single(region => region.Id == "central-glade");

        var places = new StoryPlaces(map);

        places.AnimalPlaces.Count.ShouldBe(24);
        places.AnimalPlaces.ShouldBeUnique();
        var tiles = places.AnimalPlaces.SelectMany(tile => new[] { tile, new TilePos(tile.X + 1, tile.Y) }).Append(places.Sapling).Append(places.Branch).ToList();
        tiles.ShouldBeUnique();
        tiles.ShouldAllBe(tile => glade.Contains(tile) && map.IsWalkable(tile) && map.KindAt(tile) != TileKind.Crossing);
        tiles.ShouldNotContain(map.Spawn("adam"));
        map.KindAt(places.Sapling).ShouldBe(TileKind.Grass);
    }

    [Test]
    public void Reached_ThePlaces_ShouldBeTheBankOfTheRiverAndTheTilesBesideTheSaplingAndTheBranch()
    {
        var map = StubMap.Shipped().Map;
        var places = new StoryPlaces(map);
        var bank = Enumerable.Range(0, map.Width * map.Height).Select(index => new TilePos(index % map.Width, index / map.Width))
            .First(tile => map.IsWalkable(tile) && map.KindAt(new TilePos(tile.X + 1, tile.Y)) == TileKind.Water);

        places.Reached(StoryArea.River, bank).ShouldBeTrue();
        places.Reached(StoryArea.River, map.Spawn("adam")).ShouldBeFalse();
        places.Reached(StoryArea.Sapling, new TilePos(places.Sapling.X + 1, places.Sapling.Y - 1)).ShouldBeTrue();
        places.Reached(StoryArea.Sapling, new TilePos(places.Sapling.X + 2, places.Sapling.Y)).ShouldBeFalse();
        places.Reached(StoryArea.Branch, places.Branch).ShouldBeTrue();
        places.Reached(StoryArea.Branch, places.Sapling).ShouldBeFalse();
        places.Reached(StoryArea.Sapling, map.Spawn("adam")).ShouldBeFalse();
        places.Reached(StoryArea.Branch, map.Spawn("adam")).ShouldBeFalse();
    }
}
