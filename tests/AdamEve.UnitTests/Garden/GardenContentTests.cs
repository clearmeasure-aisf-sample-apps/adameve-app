using System.Text;
using System.Text.Json.Nodes;
using AdamEve.Content;
using AdamEve.Content.Garden;
using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

[TestFixture]
public class GardenContentTests
{
    private static byte[] RepositoryFile(params string[] path) =>
        File.ReadAllBytes(Path.Combine([CanonicalFile.RepositoryRoot(), "content", .. path]));

    private static IEnumerable<TilePos> Tiles(TileMap map) =>
        Enumerable.Range(0, map.Width * map.Height).Select(index => new TilePos(index % map.Width, index / map.Width));

    [Test]
    public void LoadEmbedded_TheGarden_ShouldBeTheFilesOfTheRepository()
    {
        EmbeddedContent.Map().ShouldBe(RepositoryFile("maps", "garden.tmj"));
        EmbeddedContent.Rig("adam").ShouldBe(RepositoryFile("rigs", "adam.rig.json"));
        EmbeddedContent.Rig("woman").ShouldBe(RepositoryFile("rigs", "woman.rig.json"));
        EmbeddedContent.Animations().ShouldBe(RepositoryFile("rigs", "person.anim.json"));
    }

    [Test]
    public void LoadEmbedded_TheRigs_ShouldBeAdamAndTheWomanAndNoOther()
    {
        var rigs = typeof(EmbeddedContent).Assembly.GetManifestResourceNames().Where(name => name.EndsWith(".rig.json", StringComparison.Ordinal));

        rigs.ShouldBe(["AdamEve.Content.rigs.adam.rig.json", "AdamEve.Content.rigs.woman.rig.json"], ignoreOrder: true);
        Directory.GetFiles(Path.Combine(CanonicalFile.RepositoryRoot(), "content", "rigs"), "*.rig.json").Length.ShouldBe(2);
    }

    [Test]
    public void LoadEmbedded_TheMap_ShouldBe64By48TilesOf32Pixels()
    {
        var map = StubMap.Shipped().Map;

        map.Width.ShouldBe(64);
        map.Height.ShouldBe(48);
        map.TileSize.ShouldBe(32);
    }

    [Test]
    public void LoadEmbedded_TheMap_ShouldHaveTheCentralGladeAndTwoRegionsEachWithItsName()
    {
        var map = StubMap.Shipped().Map;

        map.Regions.Select(region => region.Id).ShouldBe(["central-glade", "spring-of-eden", "pison-meadows"]);
        map.Regions.Select(region => GameText.RegionName(region.Id)).ShouldBe([GameText.CentralGlade, GameText.SpringOfEden, GameText.PisonMeadows]);
        GameText.RegionName(null).ShouldBeEmpty();
    }

    [Test]
    public void LoadEmbedded_TheMap_ShouldBeThicketWhereverNoRegionIsOpen()
    {
        var map = StubMap.Shipped().Map;

        Tiles(map).Where(tile => map.RegionAt(tile) is null).ShouldAllBe(tile => map.KindAt(tile) == TileKind.Thicket);
        Tiles(map).Where(tile => map.RegionAt(tile) is not null).ShouldAllBe(tile => map.KindAt(tile) != TileKind.Thicket);
    }

    [Test]
    public void LoadEmbedded_TheMap_ShouldHaveTheTwoTreesInTheMidstOfTheGladeOnEitherSideOfTheRiver()
    {
        var map = StubMap.Shipped().Map;
        var glade = map.Regions.Single(region => region.Id == "central-glade");

        var life = Tiles(map).Single(tile => map.KindAt(tile) == TileKind.TreeOfLife);
        var knowledge = Tiles(map).Single(tile => map.KindAt(tile) == TileKind.TreeOfKnowledge);
        var fig = Tiles(map).Single(tile => map.KindAt(tile) == TileKind.FigTree);
        var rest = Tiles(map).Single(tile => map.KindAt(tile) == TileKind.RestingPlace);

        new[] { life, knowledge, fig, rest }.ShouldAllBe(tile => glade.Contains(tile));
        life.Y.ShouldBe(knowledge.Y);
        Enumerable.Range(life.X + 1, knowledge.X - life.X - 1).ShouldContain(x => map.KindAt(new TilePos(x, life.Y)) == TileKind.Water);
        Math.Abs(fig.X - knowledge.X).ShouldBeLessThanOrEqualTo(3);
        map.IsWalkable(life).ShouldBeFalse();
        map.IsWalkable(knowledge).ShouldBeFalse();
    }

    [Test]
    public void LoadEmbedded_TheMap_ShouldLetAWalkReachEveryOpenTileOfTheThreeRegions()
    {
        var map = StubMap.Shipped().Map;
        var reached = new HashSet<TilePos> { map.Spawn("adam") };
        var next = new Queue<TilePos>(reached);

        while (next.TryDequeue(out var tile))
        {
            foreach (var facing in Facings.All)
            {
                var neighbour = new TilePos(tile.X + facing.DeltaX(), tile.Y + facing.DeltaY());
                if (Pathfinder.CanStep(map, tile, facing.DeltaX(), facing.DeltaY(), null) && reached.Add(neighbour))
                {
                    next.Enqueue(neighbour);
                }
            }
        }

        Tiles(map).Where(map.IsWalkable).ShouldAllBe(tile => reached.Contains(tile));
        map.Regions.ShouldAllBe(region => reached.Any(region.Contains));
    }

    [Test]
    public void LoadEmbedded_TheStartingTiles_ShouldBeTwoDifferentOpenTilesOfTheGlade()
    {
        var map = StubMap.Shipped().Map;

        map.SpawnIds.ShouldBe(["adam", "woman"], ignoreOrder: true);
        map.Spawn("adam").ShouldNotBe(map.Spawn("woman"));
        map.RegionAt(map.Spawn("adam"))!.Id.ShouldBe("central-glade");
        map.RegionAt(map.Spawn("woman"))!.Id.ShouldBe("central-glade");
    }

    [Test]
    public void LoadEmbedded_TheAnimations_ShouldBeIdleAndWalk()
    {
        var animations = StubMap.Shipped().Animations;

        animations.Select(animation => animation.Id).ShouldBe(["idle", "walk"]);
        animations.ShouldAllBe(animation => animation.Duration > 0 && animation.Tracks.Count > 0);
    }

    [Test]
    public void Load_AWomanWithoutTheChestZone_ShouldBeRefused()
    {
        var woman = JsonNode.Parse(EmbeddedContent.Rig("woman"))!;
        var zones = woman["zones"]!.AsArray();
        zones.RemoveAt(1);
        var records = woman["concealment"]!.AsArray();
        foreach (var record in records.Where(record => record!["zone"]!.GetValue<string>() == "chest").ToList())
        {
            records.Remove(record);
        }

        var bytes = Encoding.UTF8.GetBytes(woman.ToJsonString());

        Should.Throw<ContentFormatException>(() => GardenContent.Load(EmbeddedContent.Map(), EmbeddedContent.Rig("adam"), bytes, EmbeddedContent.Animations()))
            .Message.ShouldContain("concealment zone");
    }

    [TestCase("\"#E2B994\"", "\"skin\"", Description = "a colour that is not #RRGGBB")]
    [TestCase("\"role\": \"body\"", "\"role\": \"garment\"", Description = "a role that is not of the list")]
    [TestCase("\"role\": \"body\"", "\"role\": \"3\"", Description = "a number where a name belongs")]
    [TestCase("\"bone\": \"hip\"", "\"bone\": \"tail\"", Description = "a bone the rig does not have")]
    [TestCase("\"view\": \"side\"", "\"view\": \"above\"", Description = "a view that is not of the list")]
    [TestCase("\"id\": \"adam\",", "", Description = "no id")]
    public void Parse_ARigThatDoesNotHoldTogether_ShouldBeRefused(string from, string to)
    {
        var text = Encoding.UTF8.GetString(EmbeddedContent.Rig("adam"));
        text.ShouldContain(from);
        var bytes = Encoding.UTF8.GetBytes(text.Replace(from, to, StringComparison.Ordinal));

        Should.Throw<ContentFormatException>(() => RigLoader.Parse(bytes));
    }

    [TestCase("\"role\": \"body\"", "\"role\": \"occluder\"", Description = "the companion foliage of before decision D18: no longer a kind of part")]
    public void Parse_ARigWithAKindOfPartThatIsNoLongerOne_ShouldBeRefused(string from, string to)
    {
        var text = Encoding.UTF8.GetString(EmbeddedContent.Rig("woman"));
        var bytes = Encoding.UTF8.GetBytes(text.Replace(from, to, StringComparison.Ordinal));

        Should.Throw<ContentFormatException>(() => RigLoader.Parse(bytes)).Message.ShouldContain("occluder");
    }

    [TestCase("\"id\": \"eyeL\"", "\"id\": \"navel\"", "navel", Description = "a part that is not on the list")]
    [TestCase("\"id\": \"torso\", \"bone\": \"torso\", \"shape\": \"rounded\", \"w\": 15, \"h\": 16, \"round\": 4.5, \"colour\": \"#E2B994\"", "\"id\": \"torso\", \"bone\": \"torso\", \"shape\": \"rounded\", \"w\": 15, \"h\": 16, \"round\": 4.5, \"colour\": \"#D9A07A\"", "torso", Description = "a third tone of skin")]
    [TestCase("\"id\": \"torso\", \"bone\": \"torso\", \"shape\": \"rounded\", \"w\": 15, \"h\": 16, \"round\": 4.5,", "\"id\": \"torso\", \"bone\": \"torso\", \"shape\": \"rounded\", \"w\": 15, \"h\": 16, \"round\": 9,", "torso", Description = "corners rounded beyond half the shorter side")]
    [TestCase("\"view\": \"front\", \"plain\": true", "\"view\": \"front\", \"by\": [\"hair\"]", "pelvis", Description = "the pelvic zone not declared plain")]
    public void Load_ARigThatHoldsWhatTheAmendedModestyRuleDoesNotAllow_ShouldBeRefused(string from, string to, string named)
    {
        foreach (var name in new[] { "adam", "woman" })
        {
            var text = Encoding.UTF8.GetString(EmbeddedContent.Rig(name));
            text.ShouldContain(from);
            var changed = Encoding.UTF8.GetBytes(text.Replace(from, to, StringComparison.Ordinal));

            Should.Throw<ContentFormatException>(() => GardenContent.Load(
                EmbeddedContent.Map(),
                name == "adam" ? changed : EmbeddedContent.Rig("adam"),
                name == "woman" ? changed : EmbeddedContent.Rig("woman"),
                EmbeddedContent.Animations())).Message.ShouldContain($"\"{named}\"");
        }
    }

    [TestCase("\"type\": \"water\"", "\"type\": \"lava\"", Description = "an unknown tile type")]
    [TestCase("\"name\": \"ground\"", "\"name\": \"floor\"", Description = "no ground layer")]
    [TestCase("\"tileheight\": 32", "\"tileheight\": 16", Description = "tiles that are not square")]
    [TestCase("\"width\": 64,", "\"width\": 63,", Description = "a width the tiles do not fill")]
    public void Parse_AMapThatIsNotAMapOfTheGarden_ShouldBeRefused(string from, string to)
    {
        var text = Encoding.UTF8.GetString(EmbeddedContent.Map());
        text.ShouldContain(from);
        var index = text.IndexOf(from, StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(text[..index] + to + text[(index + from.Length)..]);

        Should.Throw<ContentFormatException>(() => MapLoader.Parse(bytes));
    }

    [Test]
    public void Load_TheShippedContent_ShouldHoldTheGarden()
    {
        var result = GameContent.LoadEmbedded();

        result.IsLoaded.ShouldBeTrue(result.Failure);
        result.Content!.Garden.Map.Width.ShouldBe(64);
        result.Content.Garden.Woman.Id.ShouldBe("woman");
    }
}
