using AdamEve.Core.Rigs;
using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

/// <summary>
/// The asset check of the modesty rule M1 as decision D18 amended it (design, sections 5.6 and 12) over the rigs
/// that ship: the woman's chest is covered by her hair, and the pelvic zone of both is the body's own plain shape.
/// And the proof that the check fails when a cover is missing, too small or behind the body, when a record is
/// missing or declared where it may not be, and when anything but the smooth body reaches into the pelvic zone.
/// </summary>
[TestFixture]
public class ConcealmentTests
{
    private static readonly Covering[] Coverings = [Covering.None, Covering.Aprons, Covering.Coats];

    // 1x and 2x by the design; the others span what the perspective camera really draws a figure at, from a far
    // figure on a small phone to a near one on a large screen.
    private static readonly double[] Scales = [0.75, 1, 1.5, 2, 2.5, 2.86, 3, 4, 5.6, 8];

    private static readonly double[] DesignScales = [1, 2];

    private static IEnumerable<Rig> ShippedRigs()
    {
        var garden = StubMap.Shipped();
        return [garden.Adam, garden.Woman];
    }

    private static IReadOnlyList<RigAnimation> Animations() => StubMap.Shipped().Animations;

    /// <summary>Every keyframe and the frames between them at 60 Hz.</summary>
    private static IEnumerable<double> FrameTimes(RigAnimation animation)
    {
        var times = Enumerable.Range(0, (int)Math.Ceiling(animation.Duration * 60) + 1).Select(frame => frame / 60.0);
        return times.Concat(animation.Tracks.SelectMany(track => track.Keys.Select(key => key.Time))).Distinct();
    }

    private static RigAnimation Idle() => Animations().Single(animation => animation.Id == "idle");

    private static string? FirstExposed(Rig rig, Facing facing, Covering covering = Covering.None, double scale = 1)
    {
        var pose = new RigPose(rig);
        pose.Sample(Idle(), 0, facing, covering);
        var zone = new ConcealmentChecker().FirstExposedZone(pose, scale);
        return zone < 0 ? null : rig.Zones[zone].Id;
    }

    /// <summary>A part that is not of the rig: a small shape on the hip, where the pelvic zone lies.</summary>
    private static RigPart OnTheHip(string id, PartRole role, int colour, double depth, PartShape shape = PartShape.Ellipse, double x = 0, double y = 1, double size = 3)
    {
        var placement = new PartPlacement(x, y, depth, 0);
        return new RigPart(id, "hip", shape, size, size, colour, role, [], placement, placement, placement, 0);
    }

    [Test]
    public void FirstExposedZone_EveryFrameOfEveryShippedRig_ShouldConcealEveryZone()
    {
        var checker = new ConcealmentChecker();
        var frames = 0;

        foreach (var rig in ShippedRigs())
        {
            var pose = new RigPose(rig);
            foreach (var animation in Animations())
            {
                foreach (var time in FrameTimes(animation))
                {
                    foreach (var facing in Facings.All)
                    {
                        foreach (var covering in Coverings)
                        {
                            pose.Sample(animation, time, facing, covering);
                            foreach (var scale in covering == Covering.None ? Scales : DesignScales)
                            {
                                var exposed = checker.FirstExposedZone(pose, scale);

                                exposed.ShouldBe(-1, $"{rig.Id} {animation.Id} at {time:0.000} s facing {facing} with {covering} at {scale}x");
                                checker.CheckedPixels.ShouldBeGreaterThan(20);
                                frames++;
                            }
                        }
                    }
                }
            }
        }

        frames.ShouldBeGreaterThan(20_000);
    }

    [Test]
    public void FirstExposedZone_EveryFrameOfTheWomanWithHerChestZoneGrownByOnePixel_ShouldStillBeCoveredByHerHair()
    {
        var woman = StubMap.Shipped().Woman;
        var chestOnly = StubMap.With(woman, zones: woman.Zones.Where(zone => zone.Id == "chest"), concealment: woman.Concealment.Where(record => record.Zone == "chest"));
        var checker = new ConcealmentChecker();
        var pose = new RigPose(chestOnly);

        foreach (var animation in Animations())
        {
            foreach (var time in FrameTimes(animation))
            {
                foreach (var facing in Facings.All.Where(facing => !facing.IsTurnedAway()))
                {
                    foreach (var covering in Coverings)
                    {
                        pose.Sample(animation, time, facing, covering);

                        checker.FirstExposedZone(pose, 1, insetPixels: -1).ShouldBe(-1, $"{animation.Id} at {time:0.000} s facing {facing} with {covering}, 1x");
                        checker.FirstExposedZone(pose, 2, insetPixels: -2).ShouldBe(-1, $"{animation.Id} at {time:0.000} s facing {facing} with {covering}, 2x");
                    }
                }
            }
        }
    }

    [Test]
    public void Zones_TheShippedRigs_ShouldBeThePelvicZoneForBothAndTheChestZoneForTheWoman()
    {
        var garden = StubMap.Shipped();

        garden.Adam.Zones.Select(zone => zone.Id).ShouldBe(["pelvis"]);
        garden.Woman.Zones.Select(zone => zone.Id).ShouldBe(["pelvis", "chest"]);
    }

    [Test]
    public void Concealment_TheShippedRigs_ShouldDeclareFacingAwayForTheBackViewOnly()
    {
        foreach (var rig in ShippedRigs())
        {
            rig.Concealment.Where(record => record.FacingAway).ShouldAllBe(record => record.View == RigView.Back);
            rig.Concealment.Where(record => !record.FacingAway && !record.Plain).ShouldAllBe(record => record.By.Count > 0);
        }
    }

    [Test]
    public void Concealment_TheShippedRigs_ShouldDeclareThePelvicZonePlainBeforeGenesis3_7AndNothingElsePlain()
    {
        foreach (var rig in ShippedRigs())
        {
            rig.Concealment.Where(record => record.Plain).ShouldAllBe(record => record.Zone == "pelvis" && record.Covering == Covering.None && !record.FacingAway && record.By.Count == 0);
            rig.Concealment.Count(record => record.Plain).ShouldBe(3);
        }
    }

    [Test]
    public void Concealment_TheWoman_ShouldHaveHerChestCoveredByHairInEveryFacingThatIsNotTurnedAway()
    {
        var woman = StubMap.Shipped().Woman;
        var chest = woman.Zones.Single(zone => zone.Id == "chest");

        foreach (var facing in Facings.All.Where(facing => !facing.IsTurnedAway()))
        {
            woman.RecordFor(chest, facing, Covering.None).ShouldNotBeNull().By.ShouldBe([PartRole.Hair]);
            woman.RecordFor(chest, facing, Covering.Aprons).ShouldNotBeNull().By.ShouldBe([PartRole.Hair]);
        }
    }

    [Test]
    public void Concealment_TheShippedRigs_ShouldHaveARecordForEveryZoneViewAndCovering()
    {
        foreach (var rig in ShippedRigs())
        {
            foreach (var zone in rig.Zones)
            {
                foreach (var facing in Facings.All)
                {
                    foreach (var covering in Coverings)
                    {
                        rig.RecordFor(zone, facing, covering).ShouldNotBeNull($"{rig.Id} {zone.Id} {facing} {covering}");
                    }
                }
            }
        }
    }

    [Test]
    public void Parts_TheShippedRigs_ShouldHaveNoCompanionFoliageAndBindTheHairToTheHead()
    {
        foreach (var rig in ShippedRigs())
        {
            rig.Parts.ShouldAllBe(part => !part.Id.Contains("foliage", StringComparison.OrdinalIgnoreCase) && !part.Id.Contains("leaf", StringComparison.OrdinalIgnoreCase));
            rig.Parts.Where(part => part.Role == PartRole.Hair).ShouldNotBeEmpty();
            rig.Parts.Where(part => part.Role == PartRole.Hair).ShouldAllBe(part => part.Bone == "head");
            rig.Zones.Single(zone => zone.Id == "pelvis").Bone.ShouldBe("hip");
        }

        Enum.GetNames<PartRole>().ShouldNotContain("Occluder");
    }

    [Test]
    public void Parts_TheWoman_ShouldHaveLongHairThatReachesFromHerHeadToHerWaistAndNoFarther()
    {
        var woman = StubMap.Shipped().Woman;
        var pose = new RigPose(woman);

        foreach (var facing in Facings.All)
        {
            pose.Sample(Idle(), 0, facing, Covering.None);
            var hair = pose.Parts.ToArray().Where(placed => woman.Parts[placed.PartIndex].Role == PartRole.Hair).ToList();
            var top = hair.Min(placed => placed.Transform.F - (woman.Parts[placed.PartIndex].Height / 2));
            var bottom = hair.Max(placed => placed.Transform.F + (woman.Parts[placed.PartIndex].Height / 2));

            // The feet are at 0 and y grows downward: the head is 49 high, the waist 22, the pelvic zone begins at 20.
            top.ShouldBeLessThan(-45, $"facing {facing}");
            bottom.ShouldBeInRange(-23, -20.5, $"facing {facing}");
            (bottom - top).ShouldBeGreaterThan(24, $"facing {facing}: the hair is more than half as long as she is tall");
            hair.Count.ShouldBeGreaterThanOrEqualTo(3, $"facing {facing}");
        }
    }

    [Test]
    public void Parts_TheShippedRigs_ShouldHaveTheSkinAndHairOfTheDesignAndNothingButFlatShapes()
    {
        int[] skin = [0xE2B994, 0xC99A73];

        foreach (var rig in ShippedRigs())
        {
            rig.Parts.Where(part => part.Role == PartRole.Body).ShouldAllBe(part => skin.Contains(part.Colour));
            rig.Parts.Where(part => part.Role == PartRole.Hair).ShouldAllBe(part => part.Colour == 0x2B1D16);
            rig.Parts.Select(part => part.Role).Distinct().ShouldBeSubsetOf([PartRole.Body, PartRole.Hair, PartRole.Apron, PartRole.Coat, PartRole.Detail]);
            rig.Parts.Where(part => part.Role == PartRole.Detail).Select(part => part.Id).ShouldBe(["eyeL", "eyeR"]);
        }
    }

    [Test]
    public void Parts_TheShippedRigs_ShouldBeTheSameBodyForAdamAndTheWoman()
    {
        var garden = StubMap.Shipped();

        // The two bodies are the same seven blocks: the woman's has no modelling of its own. Only the hair differs.
        static IEnumerable<object> Body(Rig rig) => rig.Parts.Where(part => part.Role == PartRole.Body)
            .Select(part => (object)(part.Id, part.Bone, part.Shape, part.Width, part.Height, part.Colour, part.Front, part.Side, part.Back, part.TurnX));

        Body(garden.Woman).ShouldBe(Body(garden.Adam));
        garden.Woman.Bones.ShouldBe(garden.Adam.Bones);
    }

    [Test]
    public void ReachesInto_EveryPartOfEveryFrameOfEveryShippedRigBeforeGenesis3_7_ShouldLeaveThePelvicZoneToTheSmoothBody()
    {
        var checker = new ConcealmentChecker();
        var judged = 0;

        foreach (var rig in ShippedRigs())
        {
            var pose = new RigPose(rig);
            var pelvis = rig.ZoneIndex("pelvis");
            foreach (var animation in Animations())
            {
                foreach (var time in FrameTimes(animation))
                {
                    foreach (var facing in Facings.All)
                    {
                        pose.Sample(animation, time, facing, Covering.None);
                        var hips = pose.Parts.ToArray().Single(placed => rig.Parts[placed.PartIndex].Id == "hips");
                        foreach (var placed in pose.Parts)
                        {
                            var part = rig.Parts[placed.PartIndex];
                            if (!checker.ReachesInto(pose, pelvis, placed))
                            {
                                continue;
                            }

                            // In the zone: the hips, and beneath them the tops of the legs. Nothing else, ever.
                            part.Role.ShouldBe(PartRole.Body, $"{rig.Id} {animation.Id} at {time:0.000} s facing {facing}: {part.Id}");
                            part.Id.ShouldBeOneOf("hips", "legL", "legR");
                            (part.Id == "hips" || placed.Depth < hips.Depth).ShouldBeTrue($"{rig.Id} {animation.Id} at {time:0.000} s facing {facing}: {part.Id} lies over the hips");
                            judged++;
                        }
                    }
                }
            }
        }

        judged.ShouldBeGreaterThan(1000);
    }

    [TestCase(PartRole.Detail, 0x2B1D16, Description = "a detail: a new kind in the zone")]
    [TestCase(PartRole.Hair, 0x2B1D16, Description = "hair")]
    [TestCase(PartRole.Apron, 0x4E9A4A, Description = "an apron before Genesis 3:7")]
    [TestCase(PartRole.Coat, 0x8A6A4A, Description = "a coat before Genesis 3:7")]
    [TestCase(PartRole.Body, 0xC99A73, Description = "a body part in another colour, over the hips")]
    [TestCase(PartRole.Body, 0xE2B995, Description = "a body part one shade off, over the hips")]
    public void FirstExposedZone_AnyOtherPartInThePelvicZone_ShouldReportThePelvicZone(PartRole role, int colour)
    {
        foreach (var rig in ShippedRigs())
        {
            foreach (var shape in Enum.GetValues<PartShape>())
            {
                var marked = StubMap.With(rig, parts: rig.Parts.Append(OnTheHip("extra", role, colour, depth: 30, shape)));

                foreach (var facing in Facings.All)
                {
                    FirstExposed(marked, facing).ShouldBe("pelvis", $"{rig.Id} facing {facing}, {shape}");
                }
            }
        }
    }

    [TestCase(PartRole.Detail)]
    [TestCase(PartRole.Hair)]
    public void FirstExposedZone_APartOfAnotherKindInThePelvicZoneEvenBehindTheBody_ShouldReportThePelvicZone(PartRole role)
    {
        var adam = StubMap.Shipped().Adam;
        var hidden = StubMap.With(adam, parts: adam.Parts.Append(OnTheHip("extra", role, 0x2B1D16, depth: 0.5)));

        FirstExposed(hidden, Facing.S).ShouldBe("pelvis");
        FirstExposed(hidden, Facing.N).ShouldBe("pelvis");
    }

    [Test]
    public void FirstExposedZone_APartThatOnlyTouchesTheCornerOfThePelvicZoneFromOutside_ShouldReportThePelvicZoneOnlyWhenItReachesIn()
    {
        var adam = StubMap.Shipped().Adam;
        var outside = StubMap.With(adam, parts: adam.Parts.Append(OnTheHip("extra", PartRole.Detail, 0x2B1D16, depth: 30, PartShape.Rectangle, x: 9, y: 1, size: 3)));
        var inside = StubMap.With(adam, parts: adam.Parts.Append(OnTheHip("extra", PartRole.Detail, 0x2B1D16, depth: 30, PartShape.Rectangle, x: 8.4, y: 1, size: 3)));
        var roundOutside = StubMap.With(adam, parts: adam.Parts.Append(OnTheHip("extra", PartRole.Detail, 0x2B1D16, depth: 30, PartShape.Ellipse, x: 8.2, y: 7.2, size: 3)));
        var roundInside = StubMap.With(adam, parts: adam.Parts.Append(OnTheHip("extra", PartRole.Detail, 0x2B1D16, depth: 30, PartShape.Ellipse, x: 7.9, y: 6.9, size: 3)));

        FirstExposed(outside, Facing.S).ShouldBeNull();
        FirstExposed(inside, Facing.S).ShouldBe("pelvis");
        FirstExposed(roundOutside, Facing.S).ShouldBeNull();
        FirstExposed(roundInside, Facing.S).ShouldBe("pelvis");
    }

    [Test]
    public void FirstExposedZone_TheWomansHairGrownDownIntoThePelvicZone_ShouldReportThePelvicZone()
    {
        var woman = StubMap.Shipped().Woman;
        var longer = StubMap.With(woman, parts: woman.Parts.Select(part => part.Id == "hair-fall"
            ? part with { Height = part.Height + 6, Front = part.Front! with { Y = part.Front.Y + 3 }, Side = part.Side! with { Y = part.Side.Y + 3 }, Back = part.Back! with { Y = part.Back.Y + 3 } }
            : part));

        foreach (var facing in Facings.All)
        {
            FirstExposed(longer, facing).ShouldBe("pelvis", $"facing {facing}");
        }
    }

    [Test]
    public void FirstExposedZone_ARigWithoutItsHipsOrWithHipsTooSmallForTheZone_ShouldReportThePelvicZone()
    {
        var adam = StubMap.Shipped().Adam;
        var narrow = StubMap.With(adam, parts: adam.Parts.Select(part => part.Id == "hips" ? part with { Width = 10 } : part));
        var hidden = StubMap.With(adam, parts: adam.Parts.Select(part => part.Id == "hips" ? part with { Front = null } : part));

        FirstExposed(narrow, Facing.S).ShouldBe("pelvis");
        FirstExposed(hidden, Facing.S).ShouldBe("pelvis");
        FirstExposed(hidden, Facing.E).ShouldBeNull();
    }

    [Test]
    public void FirstExposedZone_AnOpeningOfOnePixelInTheHair_ShouldReportTheChestZone()
    {
        var woman = StubMap.Shipped().Woman;
        var parted = StubMap.With(woman, parts: woman.Parts.Select(part => part.Id switch
        {
            "hair-front-left" => part with { Width = 10, Front = part.Front! with { X = -5.25 } },
            "hair-front-right" => part with { Width = 10, Front = part.Front! with { X = 5.25 } },
            _ => part,
        }));

        var exposed = FirstExposed(parted, Facing.S, scale: 2);

        exposed.ShouldBe("chest");
        FirstExposed(woman, Facing.S, scale: 2).ShouldBeNull();
    }

    [Test]
    public void FirstExposedZone_TheWomansHairBehindHerBody_ShouldReportTheChestZone()
    {
        var woman = StubMap.Shipped().Woman;
        var behind = StubMap.With(woman, parts: woman.Parts.Select(part => part.Role == PartRole.Hair && part.Front is not null
            ? part with { Front = part.Front with { Depth = 0.5 } }
            : part));

        var exposed = FirstExposed(behind, Facing.S);

        exposed.ShouldBe("chest");
    }

    [TestCase(Facing.S)]
    [TestCase(Facing.SE)]
    [TestCase(Facing.SW)]
    [TestCase(Facing.E)]
    [TestCase(Facing.W)]
    public void FirstExposedZone_TheWomanWithoutHerFrontHair_ShouldReportTheChestZone(Facing facing)
    {
        var woman = StubMap.Shipped().Woman;
        var shorn = StubMap.With(woman, parts: woman.Parts.Where(part => !part.Id.StartsWith("hair-front", StringComparison.Ordinal)));

        var exposed = FirstExposed(shorn, facing);

        exposed.ShouldBe("chest");
    }

    [Test]
    public void FirstExposedZone_TheWomansFrontHairTooShortForTheZone_ShouldReportTheChestZone()
    {
        var woman = StubMap.Shipped().Woman;
        var bobbed = StubMap.With(woman, parts: woman.Parts
            .Where(part => !part.Id.StartsWith("hair-tip", StringComparison.Ordinal))
            .Select(part => part.Id.StartsWith("hair-front", StringComparison.Ordinal) ? part with { Height = 6, Front = part.Front! with { Y = 8 } } : part));

        var exposed = FirstExposed(bobbed, Facing.S);

        exposed.ShouldBe("chest");
    }

    [Test]
    public void FirstExposedZone_AFrameWithoutAConcealmentRecord_ShouldReportTheZone()
    {
        var woman = StubMap.Shipped().Woman;
        var unrecorded = StubMap.With(woman, concealment: woman.Concealment.Where(record => record.Zone != "chest" || record.View != RigView.Side));
        var adam = StubMap.Shipped().Adam;
        var unrecordedPelvis = StubMap.With(adam, concealment: adam.Concealment.Where(record => record.View != RigView.Front));

        FirstExposed(unrecorded, Facing.E).ShouldBe("chest");
        FirstExposed(unrecorded, Facing.W).ShouldBe("chest");
        FirstExposed(unrecorded, Facing.S).ShouldBeNull();
        FirstExposed(unrecordedPelvis, Facing.S).ShouldBe("pelvis");
        FirstExposed(unrecordedPelvis, Facing.SE).ShouldBe("pelvis");
        FirstExposed(unrecordedPelvis, Facing.E).ShouldBeNull();
        FirstExposed(StubMap.With(adam, concealment: []), Facing.N).ShouldBe("pelvis");
    }

    [TestCase(RigView.Front, Facing.S)]
    [TestCase(RigView.Front, Facing.SE)]
    [TestCase(RigView.Front, Facing.SW)]
    [TestCase(RigView.Side, Facing.E)]
    [TestCase(RigView.Side, Facing.W)]
    public void FirstExposedZone_FacingAwayDeclaredForAFacingThatIsNotABackFacing_ShouldReportTheZone(RigView view, Facing facing)
    {
        var woman = StubMap.Shipped().Woman;
        var wrong = StubMap.With(woman, concealment: woman.Concealment.Select(record => record.Zone == "chest" && record.View == view && record.Covering == Covering.None
            ? record with { FacingAway = true, By = [] }
            : record));

        var exposed = FirstExposed(wrong, facing);

        exposed.ShouldBe("chest");
        wrong.RecordFor(wrong.Zones[1], facing, Covering.None).ShouldBeNull();
    }

    [Test]
    public void FirstExposedZone_FacingAwayDeclaredForAZoneOnTheNearSideOfItsBodyPart_ShouldReportTheZone()
    {
        var woman = StubMap.Shipped().Woman;
        var nearSide = StubMap.With(woman, zones: woman.Zones.Select(zone => zone with { BackDepth = zone.FrontDepth }));

        var exposed = FirstExposed(nearSide, Facing.N);

        exposed.ShouldBe("chest");
    }

    [Test]
    public void FirstExposedZone_ARecordThatNamesTheBodyOrADetailAsCover_ShouldReportTheZone()
    {
        var woman = StubMap.Shipped().Woman;
        var body = StubMap.With(woman, concealment: woman.Concealment.Select(record => record.By.Count > 0 ? record with { By = [PartRole.Body] } : record));
        var detail = StubMap.With(woman, concealment: woman.Concealment.Select(record => record.By.Count > 0 ? record with { By = [PartRole.Hair, PartRole.Detail] } : record));

        FirstExposed(body, Facing.S).ShouldBe("chest");
        FirstExposed(detail, Facing.S).ShouldBe("chest");
    }

    [Test]
    public void FirstExposedZone_TheApronVariantWithoutItsApron_ShouldReportThePelvicZone()
    {
        var adam = StubMap.Shipped().Adam;
        var bare = StubMap.With(adam, parts: adam.Parts.Where(part => part.Role != PartRole.Apron));

        FirstExposed(bare, Facing.S, Covering.Aprons).ShouldBe("pelvis");
        FirstExposed(adam, Facing.S, Covering.Aprons).ShouldBeNull();
    }

    [Test]
    public void Sample_TheVariantsOfGenesis3_ShouldWearTheApronFrom3_7AndTheCoatFrom3_21AndNeitherBefore()
    {
        foreach (var rig in ShippedRigs())
        {
            var pose = new RigPose(rig);
            PartRole[] Roles(Covering covering)
            {
                pose.Sample(Idle(), 0, Facing.S, covering);
                return [.. pose.Parts.ToArray().Select(placed => rig.Parts[placed.PartIndex].Role).Distinct()];
            }

            Roles(Covering.None).ShouldNotContain(PartRole.Apron);
            Roles(Covering.None).ShouldNotContain(PartRole.Coat);
            Roles(Covering.Aprons).ShouldContain(PartRole.Apron);
            Roles(Covering.Aprons).ShouldNotContain(PartRole.Coat);
            Roles(Covering.Coats).ShouldContain(PartRole.Coat);
            rig.Parts.Single(part => part.Id == "apron").ShouldBe(rig.Parts.Single(part => part.Id == "apron") with { Width = 21, Height = 15, Colour = 0x4E9A4A, Bone = "hip" });
            rig.Parts.Single(part => part.Id == "coat").ShouldBe(rig.Parts.Single(part => part.Id == "coat") with { Width = 21, Height = 27, Colour = 0x8A6A4A, Bone = "torso" });
        }
    }

    [Test]
    public void Sample_AFacingToTheWest_ShouldBeTheMirrorOfItsEasternTwin()
    {
        var woman = StubMap.Shipped().Woman;
        var east = new RigPose(woman);
        var west = new RigPose(woman);
        var walk = Animations().Single(animation => animation.Id == "walk");

        east.Sample(walk, 0.1, Facing.SE, Covering.None);
        west.Sample(walk, 0.1, Facing.SW, Covering.None);

        var eastParts = east.Parts.ToArray();
        var westParts = west.Parts.ToArray();
        westParts.Select(placed => placed.PartIndex).ShouldBe(eastParts.Select(placed => placed.PartIndex));
        for (var index = 0; index < eastParts.Length; index++)
        {
            westParts[index].Transform.E.ShouldBe(-eastParts[index].Transform.E, 1e-9);
            westParts[index].Transform.F.ShouldBe(eastParts[index].Transform.F, 1e-9);
        }
    }

    [Test]
    public void Sample_ATimeBetweenTwoKeyframes_ShouldLieBetweenThem()
    {
        var animation = new RigAnimation("stub", 1, [new AnimationTrack("torso", 0.5, [new Keyframe(0, 0, 0, 0), new Keyframe(1, 0, -4, 40)])]);

        var side = animation.Sample("torso", 0.25, RigView.Side);
        var front = animation.Sample("torso", 1.25, RigView.Front);
        var still = animation.Sample("head", 0.25, RigView.Side);

        side.ShouldBe((0, -1, 10));
        front.ShouldBe((0, -1, 5));
        still.ShouldBe((0, 0, 0));
    }

    [Test]
    public void Sample_TheBackView_ShouldDrawTheBodyOverItsZones()
    {
        foreach (var rig in ShippedRigs())
        {
            var pose = new RigPose(rig);
            pose.Sample(Idle(), 0, Facing.N, Covering.None);

            foreach (var zone in rig.Zones)
            {
                var body = pose.Parts.ToArray().Single(placed => rig.Parts[placed.PartIndex].Id == zone.On);

                zone.DepthIn(RigView.Back).ShouldBeLessThan(body.Depth);
                zone.DepthIn(RigView.Front).ShouldBeGreaterThan(rig.Parts[body.PartIndex].Front!.Depth);
            }
        }
    }

    [Test]
    public void Rig_AZoneThatLiesOnNoBodyPart_ShouldBeRefused()
    {
        var adam = StubMap.Shipped().Adam;

        Should.Throw<ArgumentException>(() => StubMap.With(adam, zones: adam.Zones.Select(zone => zone with { On = "hair-top" })));
        Should.Throw<ArgumentException>(() => StubMap.With(adam, zones: adam.Zones.Select(zone => zone with { On = "nothing" })));
        Should.Throw<ArgumentException>(() => StubMap.With(adam, concealment: [new ConcealmentRecord("chest", Covering.None, RigView.Front, false, [PartRole.Hair])]));
    }

    [Test]
    public void IsTurnedAway_TheEightFacings_ShouldBeTrueForTheThreeBackFacingsOnly()
    {
        var turnedAway = Facings.All.Where(facing => facing.IsTurnedAway());

        turnedAway.ShouldBe([Facing.N, Facing.NE, Facing.NW], ignoreOrder: true);
        Facings.All.Count.ShouldBe(8);
    }
}
