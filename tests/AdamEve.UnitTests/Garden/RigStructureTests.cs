using AdamEve.Core.Rigs;
using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

/// <summary>
/// The structural half of the modesty rule M1 as decision D18 amended it (design, sections 5.6 and 12): a rig holds
/// only plain shapes, parts of a listed kind and name in a colour of that kind. A test here fails when a new
/// shape, kind, name or colour appears: a person has to look at it and add it to the lists of
/// <see cref="RigStructure"/>.
/// </summary>
[TestFixture]
public class RigStructureTests
{
    private static IEnumerable<Rig> ShippedRigs()
    {
        var garden = StubMap.Shipped();
        return [garden.Adam, garden.Woman];
    }

    private static RigPart Extra(string id, PartRole role, int colour, string bone = "hip", PartShape shape = PartShape.Ellipse)
    {
        var placement = new PartPlacement(0, 0, 30, 0);
        return new RigPart(id, bone, shape, 3, 3, colour, role, [], placement, placement, placement, 0);
    }

    [Test]
    public void Violations_TheShippedRigs_ShouldBeNone()
    {
        foreach (var rig in ShippedRigs())
        {
            RigStructure.Violations(rig).ShouldBeEmpty();
        }
    }

    [Test]
    public void AllowedShapes_TheList_ShouldBeEveryShapeThereIsSoThatANewShapeFailsHere()
    {
        Enum.GetValues<PartShape>().ShouldBe(RigStructure.AllowedShapes, ignoreOrder: true);
        Enum.GetNames<PartShape>().ShouldBe(["Ellipse", "Rectangle"]);
    }

    [Test]
    public void AllowedRoles_TheList_ShouldBeEveryKindThereIsSoThatANewKindFailsHere()
    {
        Enum.GetValues<PartRole>().ShouldBe(RigStructure.AllowedRoles, ignoreOrder: true);
        Enum.GetNames<PartRole>().ShouldBe(["Body", "Hair", "Apron", "Coat", "Detail"]);
    }

    [Test]
    public void AllowedParts_TheList_ShouldBeTheSevenBlocksOfTheBodyTheEyesTheApronAndTheCoat()
    {
        RigStructure.AllowedParts.Where(part => part.Value == PartRole.Body).Select(part => part.Key)
            .ShouldBe(["head", "torso", "hips", "armL", "armR", "legL", "legR"], ignoreOrder: true);
        RigStructure.AllowedParts.Where(part => part.Value == PartRole.Detail).Select(part => part.Key).ShouldBe(["eyeL", "eyeR"], ignoreOrder: true);
        RigStructure.AllowedParts.Where(part => part.Value is PartRole.Apron or PartRole.Coat).Select(part => part.Key).ShouldBe(["apron", "coat"], ignoreOrder: true);
        RigStructure.AllowedParts.Count.ShouldBe(11);
        RigStructure.ZoneCarriers.ShouldBe(new Dictionary<string, string> { ["pelvis"] = "hips", ["chest"] = "torso" }, ignoreOrder: true);
    }

    [Test]
    public void Parts_TheShippedRigs_ShouldBeExactlyTheListedPartsAndHair()
    {
        foreach (var rig in ShippedRigs())
        {
            var listed = rig.Parts.Where(part => part.Role != PartRole.Hair).Select(part => part.Id);
            var hair = rig.Parts.Where(part => part.Role == PartRole.Hair).Select(part => part.Id);

            listed.ShouldBe(RigStructure.AllowedParts.Keys, ignoreOrder: true);
            hair.ShouldAllBe(id => id.StartsWith("hair-", StringComparison.Ordinal));
        }
    }

    [Test]
    public void Parts_TheShippedRigs_ShouldHaveNoNameThatCouldStandForAnatomy()
    {
        // The names a part may never have. The lists of RigStructure already refuse them; this says so in words.
        string[] never = ["breast", "nipple", "chest", "bust", "pelvi", "groin", "genital", "crotch", "buttock", "bottom", "navel", "anatom"];

        foreach (var rig in ShippedRigs())
        {
            foreach (var part in rig.Parts)
            {
                never.ShouldAllBe(word => !part.Id.Contains(word, StringComparison.OrdinalIgnoreCase), part.Id);
            }
        }

        RigStructure.AllowedParts.Keys.ShouldAllBe(id => never.All(word => !id.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    [TestCase("navel", PartRole.Detail, 0x2B1D16, "hip", Description = "a detail that is not an eye")]
    [TestCase("eyeM", PartRole.Detail, 0x2B1D16, "head", Description = "a third eye")]
    [TestCase("chest-left", PartRole.Body, 0xE2B994, "torso", Description = "an eighth block of the body")]
    [TestCase("hips2", PartRole.Body, 0xE2B994, "hip", Description = "a second part on the hip")]
    [TestCase("shadow", PartRole.Hair, 0x2B1D16, "head", Description = "hair without the name of hair")]
    [TestCase("hair-low", PartRole.Hair, 0x2B1D16, "hip", Description = "hair that is not bound to the head")]
    [TestCase("hair-red", PartRole.Hair, 0xC0392B, "head", Description = "hair in another colour")]
    [TestCase("hair-skin", PartRole.Hair, 0xE2B994, "head", Description = "hair in the colour of the skin")]
    [TestCase("apron2", PartRole.Apron, 0x4E9A4A, "hip", Description = "a second apron")]
    public void Violations_ARigWithAPartThatIsNotOnTheLists_ShouldNameIt(string id, PartRole role, int colour, string bone)
    {
        foreach (var rig in ShippedRigs())
        {
            var changed = StubMap.With(rig, parts: rig.Parts.Append(Extra(id, role, colour, bone)));

            var violations = RigStructure.Violations(changed);

            violations.ShouldNotBeEmpty();
            violations.ShouldAllBe(violation => violation.Contains($"\"{id}\"", StringComparison.Ordinal));
        }
    }

    [TestCase(0xD9A07A, Description = "a third tone of skin: shading that models the body")]
    [TestCase(0xE8A0A0, Description = "pink")]
    [TestCase(0x2B1D16, Description = "the colour of the hair")]
    [TestCase(0x000000, Description = "black")]
    public void Violations_ABodyPartInAColourThatIsNotTheSkin_ShouldNameIt(int colour)
    {
        var adam = StubMap.Shipped().Adam;
        var changed = StubMap.With(adam, parts: adam.Parts.Select(part => part.Id == "torso" ? part with { Colour = colour } : part));

        RigStructure.Violations(changed).ShouldHaveSingleItem().ShouldContain("\"torso\"");
    }

    [Test]
    public void Violations_ABodyPartGivenAnotherKind_ShouldNameIt()
    {
        var adam = StubMap.Shipped().Adam;
        var changed = StubMap.With(adam, parts: adam.Parts.Select(part => part.Id == "armL" ? part with { Role = PartRole.Detail, Colour = 0x2B1D16 } : part));

        RigStructure.Violations(changed).ShouldContain(violation => violation.Contains("\"armL\"", StringComparison.Ordinal));
    }

    [Test]
    public void Violations_AnApronOrACoatWornBeforeItsVerse_ShouldNameIt()
    {
        var adam = StubMap.Shipped().Adam;
        var early = StubMap.With(adam, parts: adam.Parts.Select(part => part.Id == "apron" ? part with { Coverings = [] } : part));
        var swapped = StubMap.With(adam, parts: adam.Parts.Select(part => part.Id == "coat" ? part with { Coverings = [Covering.None, Covering.Coats] } : part));
        var bodyVariant = StubMap.With(adam, parts: adam.Parts.Select(part => part.Id == "hips" ? part with { Coverings = [Covering.Coats] } : part));

        RigStructure.Violations(early).ShouldHaveSingleItem().ShouldContain("\"apron\"");
        RigStructure.Violations(swapped).ShouldHaveSingleItem().ShouldContain("\"coat\"");
        RigStructure.Violations(bodyVariant).ShouldHaveSingleItem().ShouldContain("\"hips\"");
    }

    [Test]
    public void Violations_ThePelvicZoneNotDeclaredPlainBeforeGenesis3_7_ShouldNameIt()
    {
        foreach (var rig in ShippedRigs())
        {
            var covered = StubMap.With(rig, concealment: rig.Concealment.Select(record => record.Plain && record.View == RigView.Front ? record with { Plain = false, By = [PartRole.Hair] } : record));
            var away = StubMap.With(rig, concealment: rig.Concealment.Select(record => record.Plain && record.View == RigView.Back ? record with { Plain = false, FacingAway = true } : record));

            RigStructure.Violations(covered).ShouldNotBeEmpty();
            RigStructure.Violations(covered).ShouldAllBe(violation => violation.Contains("\"pelvis\"", StringComparison.Ordinal) && violation.Contains("None, Front", StringComparison.Ordinal));
            RigStructure.Violations(away).ShouldHaveSingleItem().ShouldContain("None, Back");
        }
    }

    [Test]
    public void Violations_TheChestDeclaredPlainOrCoveredByAnythingButHair_ShouldNameIt()
    {
        var woman = StubMap.Shipped().Woman;
        var plain = StubMap.With(woman, concealment: woman.Concealment.Select(record => record.Zone == "chest" && record.Covering == Covering.None && record.View == RigView.Front ? record with { Plain = true, By = [] } : record));
        var apron = StubMap.With(woman, concealment: woman.Concealment.Select(record => record.Zone == "chest" && record.Covering == Covering.None && record.View == RigView.Side ? record with { By = [PartRole.Apron] } : record));

        RigStructure.Violations(plain).ShouldHaveSingleItem().ShouldContain("\"chest\"");
        RigStructure.Violations(apron).ShouldHaveSingleItem().ShouldContain("\"chest\"");
    }

    [Test]
    public void Violations_AZoneThatIsNotListedNotOnItsBodyPartNotConvexOrWithoutARecord_ShouldNameIt()
    {
        var woman = StubMap.Shipped().Woman;
        var moved = StubMap.With(woman, zones: woman.Zones.Select(zone => zone.Id == "chest" ? zone with { On = "hips", Bone = "hip" } : zone));
        var dented = StubMap.With(woman, zones: woman.Zones.Select(zone => zone.Id == "pelvis" ? zone with { Points = [-7, -4, 7, -4, 0, 0, 7, 6, -7, 6] } : zone));
        var unrecorded = StubMap.With(woman, concealment: woman.Concealment.Where(record => record.Zone != "chest" || record.Covering != Covering.Coats || record.View != RigView.Side));
        var noPelvis = StubMap.With(woman, zones: woman.Zones.Where(zone => zone.Id == "chest"), concealment: woman.Concealment.Where(record => record.Zone == "chest"));

        RigStructure.Violations(moved).ShouldHaveSingleItem().ShouldContain("\"chest\"");
        RigStructure.Violations(dented).ShouldHaveSingleItem().ShouldContain("convex");
        RigStructure.Violations(unrecorded).ShouldHaveSingleItem().ShouldContain("Coats, Side");
        RigStructure.Violations(noPelvis).ShouldHaveSingleItem().ShouldContain("no pelvic zone");
    }

    [Test]
    public void AllowedColours_EachKind_ShouldBeTheColoursOfTheDesign()
    {
        RigStructure.AllowedColours(PartRole.Body).ShouldBe([0xE2B994, 0xC99A73]);
        RigStructure.AllowedColours(PartRole.Hair).ShouldBe([0x2B1D16]);
        RigStructure.AllowedColours(PartRole.Detail).ShouldBe([0x2B1D16]);
        RigStructure.AllowedColours(PartRole.Apron).ShouldBe([0x4E9A4A]);
        RigStructure.AllowedColours(PartRole.Coat).ShouldBe([0x8A6A4A]);
    }

    [Test]
    public void Parts_TheShippedRigsInEveryFrame_ShouldKeepEveryDetailOnTheHeadAboveTheChest()
    {
        var garden = StubMap.Shipped();

        foreach (var rig in ShippedRigs())
        {
            var pose = new RigPose(rig);
            foreach (var animation in garden.Animations)
            {
                foreach (var facing in Facings.All)
                {
                    for (var frame = 0; frame <= animation.Duration * 60; frame++)
                    {
                        pose.Sample(animation, frame / 60.0, facing, Covering.None);
                        foreach (var placed in pose.Parts)
                        {
                            if (rig.Parts[placed.PartIndex].Role == PartRole.Detail)
                            {
                                // The feet are at 0 and y grows downward: the chin is 32 high.
                                placed.Transform.F.ShouldBeLessThan(-34, $"{rig.Id} {animation.Id} frame {frame} facing {facing}");
                            }
                        }
                    }
                }
            }
        }
    }
}
