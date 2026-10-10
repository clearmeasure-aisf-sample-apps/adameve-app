namespace AdamEve.Core.Rigs;

/// <summary>
/// The structural half of the modesty rule M1 as decision D18 amended it (design, sections 5.6 and 12): the figures
/// are never anatomical, by construction. A rig may hold only what is listed here: plain shapes, parts of a known
/// kind and name in a colour of that kind, the two zones on the body parts that carry them, and the pelvic zone
/// declared plain before Genesis 3:7. Anything else is a violation: a new shape, kind, name or colour has to be
/// added to these lists by a person who has looked at it, and a test fails until then.
/// </summary>
public static class RigStructure
{
    /// <summary>The colour of the skin (design, decision D7).</summary>
    public const int Skin = 0xE2B994;

    /// <summary>The colour of a limb on the far side of the body: the same skin, in shade.</summary>
    public const int SkinInShade = 0xC99A73;

    /// <summary>The colour of the hair, and of the eyes (design, decision D7).</summary>
    public const int Hair = 0x2B1D16;

    /// <summary>The colour of the fig-leaf apron (Genesis 3:7).</summary>
    public const int ApronGreen = 0x4E9A4A;

    /// <summary>The colour of the coat of skins (Genesis 3:21).</summary>
    public const int CoatBrown = 0x8A6A4A;

    /// <summary>The id of the pelvic zone, which both rigs have.</summary>
    public const string Pelvis = "pelvis";

    /// <summary>The id of the chest zone, which the rig of the woman has.</summary>
    public const string Chest = "chest";

    /// <summary>The prefix of the id of every hair part.</summary>
    public const string HairPrefix = "hair-";

    /// <summary>The bone the hair hangs on.</summary>
    public const string Head = "head";

    /// <summary>
    /// The bone on which hair that moves hangs: a bone of the head itself, so that the hair is still bound to the
    /// head and goes where it goes.
    /// </summary>
    public const string HairBone = "hair";

    /// <summary>The outlines a part may have: plain shapes only.</summary>
    public static IReadOnlyList<PartShape> AllowedShapes { get; } = [PartShape.Ellipse, PartShape.Rectangle, PartShape.Rounded];

    /// <summary>The kinds of part a rig may have.</summary>
    public static IReadOnlyList<PartRole> AllowedRoles { get; } = [PartRole.Body, PartRole.Hair, PartRole.Apron, PartRole.Coat, PartRole.Detail];

    /// <summary>
    /// The parts a rig may have beside its hair, by id, each with its kind. The body is these seven blocks and
    /// nothing else; the only detail is the eyes.
    /// </summary>
    public static IReadOnlyDictionary<string, PartRole> AllowedParts { get; } = new Dictionary<string, PartRole>(StringComparer.Ordinal)
    {
        ["head"] = PartRole.Body,
        ["torso"] = PartRole.Body,
        ["hips"] = PartRole.Body,
        ["armL"] = PartRole.Body,
        ["armR"] = PartRole.Body,
        ["legL"] = PartRole.Body,
        ["legR"] = PartRole.Body,
        ["eyeL"] = PartRole.Detail,
        ["eyeR"] = PartRole.Detail,
        ["apron"] = PartRole.Apron,
        ["coat"] = PartRole.Coat,
    };

    /// <summary>The body part each zone lies on.</summary>
    public static IReadOnlyDictionary<string, string> ZoneCarriers { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Pelvis] = "hips",
        [Chest] = "torso",
    };

    /// <summary>The colours a part of a kind may have.</summary>
    /// <param name="role">The kind.</param>
    public static IReadOnlyList<int> AllowedColours(PartRole role) => role switch
    {
        PartRole.Body => [Skin, SkinInShade],
        PartRole.Hair or PartRole.Detail => [Hair],
        PartRole.Apron => [ApronGreen],
        PartRole.Coat => [CoatBrown],
        _ => [],
    };

    /// <summary>What a rig holds that the amended rule does not allow; empty for a rig that may be drawn.</summary>
    /// <param name="rig">The rig.</param>
    public static IReadOnlyList<string> Violations(Rig rig)
    {
        ArgumentNullException.ThrowIfNull(rig);
        var found = new List<string>();
        foreach (var part in rig.Parts)
        {
            if (!AllowedShapes.Contains(part.Shape))
            {
                found.Add($"The part \"{part.Id}\" of the rig {rig.Id} has a shape that is not on the list of plain shapes.");
            }

            if (!AllowedRoles.Contains(part.Role))
            {
                found.Add($"The part \"{part.Id}\" of the rig {rig.Id} is of a kind that is not on the list of kinds.");
                continue;
            }

            var named = AllowedParts.TryGetValue(part.Id, out var role)
                ? role == part.Role
                : part.Role == PartRole.Hair && part.Id.StartsWith(HairPrefix, StringComparison.Ordinal);
            if (!named)
            {
                found.Add($"The part \"{part.Id}\" of the rig {rig.Id} is not on the list of parts, or not of the kind the list gives it.");
            }

            if (!AllowedColours(part.Role).Contains(part.Colour))
            {
                found.Add($"The part \"{part.Id}\" of the rig {rig.Id} has a colour that is not a colour of its kind.");
            }

            // Hair is bound to the head: to its bone, or to the bone of the head on which moving hair hangs. An eye
            // is on the head itself.
            var onTheHead = part.Bone == Head
                || (part.Role == PartRole.Hair && part.Bone == HairBone && rig.Bones.Any(bone => bone.Id == HairBone && bone.Parent == Head));
            if (part.Role is PartRole.Hair or PartRole.Detail && !onTheHead)
            {
                found.Add($"The part \"{part.Id}\" of the rig {rig.Id} is hair or an eye and is not bound to the head.");
            }

            var rounded = part.Shape == PartShape.Rounded
                ? part.Round > 0 && part.Round <= Math.Min(part.Width, part.Height) / 2
                : part.Round == 0;
            if (!rounded)
            {
                found.Add($"The part \"{part.Id}\" of the rig {rig.Id} has corners rounded by a radius its shape does not have.");
            }

            var worn = part.Role switch
            {
                PartRole.Apron => part.Coverings.SequenceEqual([Covering.Aprons]),
                PartRole.Coat => part.Coverings.SequenceEqual([Covering.Coats]),
                _ => part.Coverings.Count == 0,
            };
            if (!worn)
            {
                found.Add($"The part \"{part.Id}\" of the rig {rig.Id} belongs to covering variants its kind does not have.");
            }
        }

        if (rig.ZoneIndex(Pelvis) < 0)
        {
            found.Add($"The rig {rig.Id} has no pelvic zone.");
        }

        foreach (var zone in rig.Zones)
        {
            if (!ZoneCarriers.TryGetValue(zone.Id, out var carrier) || carrier != zone.On)
            {
                found.Add($"The zone \"{zone.Id}\" of the rig {rig.Id} is not on the list of zones, or not on its body part.");
            }

            if (!IsConvex(zone.Points))
            {
                found.Add($"The zone \"{zone.Id}\" of the rig {rig.Id} is not a convex polygon.");
            }

            foreach (var covering in Enum.GetValues<Covering>())
            {
                foreach (var view in Enum.GetValues<RigView>())
                {
                    var record = rig.Concealment.FirstOrDefault(each => each.Zone == zone.Id && each.Covering == covering && each.View == view);
                    if (record is null)
                    {
                        found.Add($"The zone \"{zone.Id}\" of the rig {rig.Id} has no concealment record for {covering}, {view}.");
                        continue;
                    }

                    // Before Genesis 3:7 the pelvic region is the body's own smooth shape in every view, and no
                    // other zone, view or variant may say so of itself.
                    var mustBePlain = zone.Id == Pelvis && covering == Covering.None;
                    if (record.Plain != mustBePlain)
                    {
                        found.Add($"The record of the zone \"{zone.Id}\" of the rig {rig.Id} for {covering}, {view} is plain where it may not be, or not plain where it must be.");
                    }

                    var mayCover = (zone.Id, covering) switch
                    {
                        (Chest, Covering.Coats) => record.By.All(role => role is PartRole.Hair or PartRole.Coat),
                        (Chest, _) => record.By.All(role => role == PartRole.Hair),
                        (_, Covering.Aprons) => record.By.All(role => role == PartRole.Apron),
                        (_, Covering.Coats) => record.By.All(role => role == PartRole.Coat),
                        _ => record.By.Count == 0,
                    };
                    if (!mayCover)
                    {
                        found.Add($"The record of the zone \"{zone.Id}\" of the rig {rig.Id} for {covering}, {view} names a cover that zone and variant do not have.");
                    }
                }
            }
        }

        return found;
    }

    private static bool IsConvex(IReadOnlyList<double> points)
    {
        var count = points.Count / 2;
        var sign = 0;
        for (var index = 0; index < count; index++)
        {
            var next = (index + 1) % count;
            var after = (index + 2) % count;
            var cross = ((points[next * 2] - points[index * 2]) * (points[(after * 2) + 1] - points[(next * 2) + 1]))
                - ((points[(next * 2) + 1] - points[(index * 2) + 1]) * (points[after * 2] - points[next * 2]));
            if (cross == 0)
            {
                continue;
            }

            if (sign != 0 && Math.Sign(cross) != sign)
            {
                return false;
            }

            sign = Math.Sign(cross);
        }

        return sign != 0;
    }
}
