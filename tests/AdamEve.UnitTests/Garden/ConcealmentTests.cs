using AdamEve.Core.Rigs;
using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

/// <summary>
/// The asset check of the modesty rule M1 (design, section 5.6) over the rigs that ship, and the proof that the
/// check fails when a cover is missing, too small or declared where it may not be.
/// </summary>
[TestFixture]
public class ConcealmentTests
{
    private static readonly Covering[] Coverings = [Covering.None, Covering.Aprons, Covering.Coats];

    // 1x and 2x by the design; 2.5 and 3 are near what a phone and a desktop really draw at.
    private static readonly double[] Scales = [1, 2, 2.5, 2.86, 3];

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

        frames.ShouldBeGreaterThan(10_000);
    }

    [Test]
    public void FirstExposedZone_EveryFrameOfEveryShippedRigWithEachZoneGrownByTwoPixels_ShouldStillConcealEveryZone()
    {
        var checker = new ConcealmentChecker();

        foreach (var rig in ShippedRigs())
        {
            var pose = new RigPose(rig);
            foreach (var animation in Animations())
            {
                foreach (var time in FrameTimes(animation))
                {
                    foreach (var facing in Facings.All.Where(facing => !facing.IsTurnedAway()))
                    {
                        foreach (var covering in Coverings)
                        {
                            pose.Sample(animation, time, facing, covering);

                            var exposed = checker.FirstExposedZone(pose, 1, insetPixels: -2);

                            exposed.ShouldBe(-1, $"{rig.Id} {animation.Id} at {time:0.000} s facing {facing} with {covering}");
                        }
                    }
                }
            }
        }
    }

    [Test]
    public void DefaultFoliageCoversEveryZone_EveryFrameOfEveryShippedRig_ShouldBeTrue()
    {
        var checker = new ConcealmentChecker();

        foreach (var rig in ShippedRigs())
        {
            var pose = new RigPose(rig);
            foreach (var animation in Animations())
            {
                foreach (var time in FrameTimes(animation))
                {
                    foreach (var facing in Facings.All)
                    {
                        pose.Sample(animation, time, facing, Covering.None);

                        checker.DefaultFoliageCoversEveryZone(pose, 1).ShouldBeTrue($"{rig.Id} {animation.Id} at {time:0.000} s facing {facing}");
                        checker.DefaultFoliageCoversEveryZone(pose, 2).ShouldBeTrue($"{rig.Id} {animation.Id} at {time:0.000} s facing {facing}, 2x");
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
            rig.Concealment.Where(record => !record.FacingAway).ShouldAllBe(record => record.By.Count > 0);
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
    public void Parts_TheShippedRigs_ShouldBindTheCompanionFoliageToTheHipAndTheHairToTheHead()
    {
        foreach (var rig in ShippedRigs())
        {
            rig.Parts.Where(part => part.Role == PartRole.Occluder).ShouldNotBeEmpty();
            rig.Parts.Where(part => part.Role == PartRole.Occluder).ShouldAllBe(part => part.Bone == "hip" && part.Coverings.SequenceEqual(new[] { Covering.None }));
            rig.Parts.Where(part => part.Role == PartRole.Hair).ShouldAllBe(part => part.Bone == "head");
            rig.Zones.Single(zone => zone.Id == "pelvis").Bone.ShouldBe("hip");
        }
    }

    [Test]
    public void Parts_TheShippedRigs_ShouldHaveTheSkinAndHairOfTheDesignAndNothingButFlatShapes()
    {
        int[] skin = [0xE2B994, 0xC99A73, 0xF1D3B3];

        foreach (var rig in ShippedRigs())
        {
            rig.Parts.Where(part => part.Role == PartRole.Body).ShouldAllBe(part => skin.Contains(part.Colour));
            rig.Parts.Where(part => part.Role == PartRole.Hair).ShouldAllBe(part => part.Colour == 0x2B1D16);
            rig.Parts.Select(part => part.Role).Distinct().ShouldBeSubsetOf([PartRole.Body, PartRole.Hair, PartRole.Occluder, PartRole.Apron, PartRole.Coat, PartRole.Detail]);
            rig.Parts.Where(part => part.Role == PartRole.Detail).Select(part => part.Id).ShouldBe(["eyeL", "eyeR"]);
        }
    }

    [TestCase(Facing.E)]
    [TestCase(Facing.SE)]
    [TestCase(Facing.S)]
    [TestCase(Facing.SW)]
    [TestCase(Facing.W)]
    public void FirstExposedZone_ARigWithoutItsCompanionFoliageFacingTheViewerOrSideways_ShouldReportThePelvicZone(Facing facing)
    {
        var adam = StubMap.Shipped().Adam;
        var bare = StubMap.With(adam, parts: adam.Parts.Where(part => part.Role != PartRole.Occluder));

        var exposed = FirstExposed(bare, facing);

        exposed.ShouldBe("pelvis");
    }

    [TestCase(Facing.N)]
    [TestCase(Facing.NE)]
    [TestCase(Facing.NW)]
    public void FirstExposedZone_ARigWithoutItsCompanionFoliageTurnedAway_ShouldBeConcealedByItsOwnBack(Facing facing)
    {
        var adam = StubMap.Shipped().Adam;
        var bare = StubMap.With(adam, parts: adam.Parts.Where(part => part.Role != PartRole.Occluder));

        var exposed = FirstExposed(bare, facing);

        exposed.ShouldBeNull();
    }

    [Test]
    public void FirstExposedZone_CompanionFoliageTooSmallForTheZone_ShouldReportThePelvicZone()
    {
        var adam = StubMap.Shipped().Adam;
        var sparse = StubMap.With(adam, parts: adam.Parts
            .Where(part => part.Role != PartRole.Occluder || part.Id == "foliage-bush")
            .Select(part => part.Id == "foliage-bush" ? part with { Width = 12 } : part));

        var exposed = FirstExposed(sparse, Facing.S);

        exposed.ShouldBe("pelvis");
    }

    [Test]
    public void FirstExposedZone_CompanionFoliageBehindTheBody_ShouldReportThePelvicZone()
    {
        var adam = StubMap.Shipped().Adam;
        var behind = StubMap.With(adam, parts: adam.Parts.Select(part => part.Role == PartRole.Occluder
            ? part with { Front = part.Front! with { Depth = 0.5 } }
            : part));

        var exposed = FirstExposed(behind, Facing.S);

        exposed.ShouldBe("pelvis");
    }

    [Test]
    public void FirstExposedZone_AnOpeningOfOnePixelInTheCover_ShouldReportTheZone()
    {
        var woman = StubMap.Shipped().Woman;
        var parted = StubMap.With(woman, parts: woman.Parts.Select(part => part.Id switch
        {
            "hair-front-left" => part with { Width = 9, Front = part.Front! with { X = -5.5 } },
            "hair-front-right" => part with { Width = 9, Front = part.Front! with { X = 5.5 } },
            _ => part,
        }));

        var exposed = FirstExposed(parted, Facing.S, scale: 2);

        exposed.ShouldBe("chest");
    }

    [TestCase(Facing.S)]
    [TestCase(Facing.SE)]
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
    public void FirstExposedZone_AFrameWithoutAConcealmentRecord_ShouldReportTheZone()
    {
        var woman = StubMap.Shipped().Woman;
        var unrecorded = StubMap.With(woman, concealment: woman.Concealment.Where(record => record.Zone != "chest" || record.View != RigView.Side));

        FirstExposed(unrecorded, Facing.E).ShouldBe("chest");
        FirstExposed(unrecorded, Facing.W).ShouldBe("chest");
        FirstExposed(unrecorded, Facing.S).ShouldBeNull();
    }

    [TestCase(RigView.Front, Facing.S)]
    [TestCase(RigView.Front, Facing.SE)]
    [TestCase(RigView.Front, Facing.SW)]
    [TestCase(RigView.Side, Facing.E)]
    [TestCase(RigView.Side, Facing.W)]
    public void FirstExposedZone_FacingAwayDeclaredForAFacingThatIsNotABackFacing_ShouldReportTheZone(RigView view, Facing facing)
    {
        var adam = StubMap.Shipped().Adam;
        var wrong = StubMap.With(adam, concealment: adam.Concealment.Select(record => record.View == view && record.Covering == Covering.None
            ? record with { FacingAway = true, By = [] }
            : record));

        var exposed = FirstExposed(wrong, facing);

        exposed.ShouldBe("pelvis");
        wrong.RecordFor(wrong.Zones[0], facing, Covering.None).ShouldBeNull();
    }

    [Test]
    public void FirstExposedZone_FacingAwayDeclaredForAZoneOnTheNearSideOfItsBodyPart_ShouldReportTheZone()
    {
        var adam = StubMap.Shipped().Adam;
        var nearSide = StubMap.With(adam, zones: adam.Zones.Select(zone => zone with { BackDepth = zone.FrontDepth }));

        var exposed = FirstExposed(nearSide, Facing.N);

        exposed.ShouldBe("pelvis");
    }

    [Test]
    public void FirstExposedZone_ARecordThatNamesTheBodyAsCover_ShouldReportTheZone()
    {
        var adam = StubMap.Shipped().Adam;
        var wrong = StubMap.With(adam, concealment: adam.Concealment.Select(record => record.FacingAway ? record : record with { By = [PartRole.Body] }));

        var exposed = FirstExposed(wrong, Facing.S);

        exposed.ShouldBe("pelvis");
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
    public void Sample_TheApronVariant_ShouldLeaveTheCompanionFoliageOut()
    {
        var adam = StubMap.Shipped().Adam;
        var pose = new RigPose(adam);

        pose.Sample(Idle(), 0, Facing.S, Covering.Aprons);

        pose.Parts.ToArray().Select(placed => adam.Parts[placed.PartIndex].Role).ShouldNotContain(PartRole.Occluder);
        pose.Parts.ToArray().Select(placed => adam.Parts[placed.PartIndex].Role).ShouldContain(PartRole.Apron);
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

        Should.Throw<ArgumentException>(() => StubMap.With(adam, zones: adam.Zones.Select(zone => zone with { On = "foliage-bush" })));
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
