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
    public void Compose_AdamAndTheWomanStandingAsTheGameDrawsThem_ShouldHaveTheVerdictOkAndEveryPartOfTheRig(double scale, int? light)
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
    public void Compose_AFigure_ShouldDrawNoFoliageAndTheWomansHairOverHerChest()
    {
        var garden = StubMap.Shipped();
        int[] greens = [0x3E8E4E, 0x2F7A44, 0x57A65B, 0x4E9A4A];

        var adam = StillFigure.Compose(garden.Adam, Idle(), Facing.S, Covering.None, 2);
        var woman = StillFigure.Compose(garden.Woman, Idle(), Facing.S, Covering.None, 2);

        adam.Shapes.Concat(woman.Shapes).ShouldAllBe(shape => !greens.Contains(shape.Colour));
        woman.Shapes.Count(shape => shape.Colour == 0x2B1D16).ShouldBeGreaterThan(adam.Shapes.Count(shape => shape.Colour == 0x2B1D16) + 5);
        woman.Shapes[^1].Colour.ShouldBe(0x2B1D16, "the hair over her chest is painted last");
    }

    [Test]
    public void Compose_ARigThatDoesNotPass_ShouldFailClosedWithNothingDrawnAndNameWhatFailed()
    {
        var garden = StubMap.Shipped();
        var shorn = StubMap.With(garden.Woman, parts: garden.Woman.Parts.Where(part => !part.Id.StartsWith("hair-front", StringComparison.Ordinal)));
        var marked = StubMap.With(garden.Adam, parts: garden.Adam.Parts.Append(new RigPart("navel", "hip", PartShape.Ellipse, 2, 2, 0x2B1D16, PartRole.Detail, [], new PartPlacement(0, 0, 30, 0), null, null, 0)));

        var woman = StillFigure.Compose(shorn, Idle(), Facing.S, Covering.None, 2);
        var adam = StillFigure.Compose(marked, Idle(), Facing.S, Covering.None, 2);

        woman.Concealment.ShouldBe("fail:woman:chest");
        woman.Shapes.ShouldBeEmpty();
        adam.Concealment.ShouldBe("fail:adam:structure");
        adam.Shapes.ShouldBeEmpty();
        StillFigure.VerdictOf([StillFigure.Compose(garden.Adam, Idle(), Facing.S, Covering.None, 2), woman]).ShouldBe("fail:woman:chest");
        StillFigure.VerdictOf([]).ShouldBe("ok");
    }
}
