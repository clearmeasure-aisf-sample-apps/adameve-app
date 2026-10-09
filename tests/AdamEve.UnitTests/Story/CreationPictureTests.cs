using AdamEve.Core.Rigs;
using AdamEve.Core.Story;
using AdamEve.Core.World;
using AdamEve.UnitTests.Garden;

namespace AdamEve.UnitTests.Story;

/// <summary>The picture of the days of creation, and the figures drawn outside the garden under rule M1.</summary>
[TestFixture]
public class CreationPictureTests
{
    /// <summary>What each day adds to the picture: nothing of it may be seen before the player's gesture of that day.</summary>
    private static readonly Dictionary<StoryBeat, SceneLayer[]> MadeOn = new()
    {
        [StoryBeat.B1] = [SceneLayer.Light, SceneLayer.Day],
        [StoryBeat.B2] = [SceneLayer.Firmament],
        [StoryBeat.B3] = [SceneLayer.Land, SceneLayer.Grass, SceneLayer.Trees],
        [StoryBeat.B4] = [SceneLayer.Sun, SceneLayer.Moon, SceneLayer.Stars],
        [StoryBeat.B5] = [SceneLayer.Fish, SceneLayer.Whales, SceneLayer.Birds],
        [StoryBeat.B6] = [SceneLayer.Animals, SceneLayer.Figures],
    };

    private static RigAnimation Idle() => StubMap.Shipped().Animations.Single(animation => animation.Id == "idle");

    [Test]
    public void Layers_BeforeTheGestureOfADay_ShouldShowNothingOfThatDay()
    {
        foreach (var day in CreationStory.Days.Where(day => day.RevealAt >= 0))
        {
            for (var card = day.FirstCard; card < day.RevealAt; card++)
            {
                CreationPicture.Layers(day.Beat, card).ShouldNotContain(layer => MadeOn[day.Beat].Contains(layer), $"{day.Beat}, card {card}");
            }
        }
    }

    [Test]
    public void Layers_OnTheCardTheGestureUncovers_ShouldShowSomethingOfThatDayAndThePresenceOfGod()
    {
        foreach (var day in CreationStory.Days.Where(day => day.RevealAt >= 0))
        {
            var layers = CreationPicture.Layers(day.Beat, day.RevealAt);

            layers.ShouldContain(layer => MadeOn[day.Beat].Contains(layer), day.Beat.ToString());
            layers.ShouldContain(SceneLayer.Presence);
        }
    }

    [Test]
    public void Layers_ByTheEndOfEachDay_ShouldHaveShownEverythingOfThatDayAndNothingOfALaterDay()
    {
        foreach (var day in CreationStory.Days.Where(day => day.RevealAt >= 0))
        {
            var seen = Enumerable.Range(0, day.Cards.Count).SelectMany(card => CreationPicture.Layers(day.Beat, card)).Distinct().ToList();
            var later = MadeOn.Where(made => made.Key > day.Beat).SelectMany(made => made.Value);

            MadeOn[day.Beat].ShouldBeSubsetOf(seen);
            seen.ShouldNotContain(layer => later.Contains(layer));
        }
    }

    [Test]
    public void Layers_ThePresenceOfGod_ShouldBeShownExactlyOnTheCardsOnWhichHeSpeaks()
    {
        foreach (var day in CreationStory.Days)
        {
            for (var card = day.FirstCard; card < day.Cards.Count; card++)
            {
                var speaks = card >= 0 && day.Voice.Contains(day.Cards[card]);

                CreationPicture.Layers(day.Beat, card).Contains(SceneLayer.Presence).ShouldBe(speaks, $"{day.Beat}, card {card}");
            }
        }
    }

    [Test]
    public void Layers_TheTwoFiguresOfLight_ShouldBeShownFrom1_26To1_28Only()
    {
        var shown = CreationStory.Days
            .SelectMany(day => Enumerable.Range(0, day.Cards.Count).Where(card => CreationPicture.Layers(day.Beat, card).Contains(SceneLayer.Figures)).Select(card => day.Cards[card]));

        shown.ShouldBe([new ScriptureRef(1, 26), new ScriptureRef(1, 27), new ScriptureRef(1, 28)]);
    }

    [Test]
    public void Layers_TheSeventhDay_ShouldShowTheSamePictureOnEveryCardAndNoFigure()
    {
        var day = CreationStory.DayOf(StoryBeat.B7).ShouldNotBeNull();

        var pictures = Enumerable.Range(0, day.Cards.Count).Select(card => CreationPicture.Layers(StoryBeat.B7, card)).ToList();

        pictures.ShouldAllBe(picture => picture.SequenceEqual(pictures[0]));
        pictures[0].ShouldNotContain(SceneLayer.Figures);
        pictures[0].ShouldNotContain(SceneLayer.Presence);
        day.RevealAt.ShouldBe(-1);
    }

    [Test]
    public void ShapesOf_EveryLayerButTheFigures_ShouldBeFlatShapesInsideThePicture()
    {
        foreach (var layer in Enum.GetValues<SceneLayer>().Where(layer => layer != SceneLayer.Figures))
        {
            var shapes = CreationPicture.ShapesOf(layer);

            shapes.ShouldNotBeEmpty(layer.ToString());
            shapes.ShouldAllBe(shape => shape.X >= 0 && shape.X <= CreationPicture.Width && shape.Y >= 0 && shape.Y <= CreationPicture.Height && shape.Width > 0 && shape.Height > 0);
        }

        CreationPicture.ShapesOf(SceneLayer.Figures).ShouldBeEmpty();
    }

    [Test]
    public void ShapesOf_ThePresenceOfGod_ShouldBeLightAboveTheEarthAndNoFigure()
    {
        var presence = CreationPicture.ShapesOf(SceneLayer.Presence);

        presence.ShouldAllBe(shape => shape.Shape == PartShape.Ellipse && shape.X == presence[0].X && shape.Y == presence[0].Y);
        presence.ShouldAllBe(shape => shape.Y + (shape.Height / 2) < 100);
        Enum.GetNames<SceneLayer>().ShouldNotContain(name => name.Contains("God", StringComparison.OrdinalIgnoreCase));
    }

    [TestCase(1, null)]
    [TestCase(2, null)]
    [TestCase(3, null)]
    [TestCase(1, CreationPicture.FigureLight)]
    [TestCase(2, CreationPicture.FigureLight)]
    public void Compose_AdamAndTheWomanStandingAsTheGameDrawsThem_ShouldHaveTheVerdictOkAndNoDefaultFoliage(double scale, int? light)
    {
        var garden = StubMap.Shipped();

        foreach (var rig in new[] { garden.Adam, garden.Woman })
        {
            var figure = StillFigure.Compose(rig, Idle(), Facing.S, Covering.None, scale, light);

            figure.Concealment.ShouldBe("ok");
            figure.Shapes.Count.ShouldBe(rig.Parts.Count(part => part.In(RigView.Front) is not null && part.WornWith(Covering.None)));
            figure.Shapes.Select(shape => shape.Colour).Distinct().Count().ShouldBe(light is null ? rig.Parts.Where(part => part.In(RigView.Front) is not null && part.WornWith(Covering.None)).Select(part => part.Colour).Distinct().Count() : 1);
        }
    }

    [Test]
    public void Compose_AFigure_ShouldDrawTheCompanionFoliageAfterTheBody()
    {
        var woman = StubMap.Shipped().Woman;
        var occluders = woman.Parts.Count(part => part.Role == PartRole.Occluder && part.WornWith(Covering.None));

        var figure = StillFigure.Compose(woman, Idle(), Facing.S, Covering.None, 2);

        occluders.ShouldBeGreaterThan(0);
        figure.Shapes.TakeLast(occluders).Select(shape => shape.Colour).ShouldBe(woman.Parts.Where(part => part.Role == PartRole.Occluder).Select(part => part.Colour), ignoreOrder: true);
    }

    [Test]
    public void Compose_ARigWithoutItsCompanionFoliage_ShouldFailClosedWithTheDefaultFoliageInFrontAndNameTheZone()
    {
        var adam = StubMap.Shipped().Adam;
        var bare = StubMap.With(adam, parts: adam.Parts.Where(part => part.Role != PartRole.Occluder));

        var figure = StillFigure.Compose(bare, Idle(), Facing.S, Covering.None, 2);

        figure.Concealment.ShouldBe("fail:adam:pelvis");
        figure.Shapes.TakeLast(DefaultFoliage.Shapes.Count).Select(shape => shape.Colour).ShouldBe(DefaultFoliage.Shapes.Select(shape => shape.Colour));
        StillFigure.VerdictOf([StillFigure.Compose(adam, Idle(), Facing.S, Covering.None, 2), figure]).ShouldBe("fail:adam:pelvis");
        StillFigure.VerdictOf([]).ShouldBe("ok");
    }
}
