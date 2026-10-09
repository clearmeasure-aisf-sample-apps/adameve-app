using AdamEve.Core.World;

namespace AdamEve.Core.Rigs;

/// <summary>A part of a composed frame: which part, where, and how near the viewer.</summary>
/// <param name="PartIndex">The index of the part in its rig.</param>
/// <param name="Transform">From the part's own space (its centre at the origin) to the rig's space (the feet at the origin).</param>
/// <param name="Depth">The place in the depth order: a greater depth is drawn later.</param>
public readonly record struct PlacedPart(int PartIndex, Affine Transform, double Depth);

/// <summary>
/// One frame of a rig, composed: every part that is seen, placed by the skeleton at a time of an animation, in a
/// facing, under a covering variant. The renderer draws exactly this, and the modesty check judges exactly this.
/// The object is reused from frame to frame and allocates nothing while it samples.
/// </summary>
public sealed class RigPose
{
    private readonly Affine[] bones;
    private readonly int[] boneParents;
    private readonly int[] partBones;
    private readonly int[] zoneBones;
    private readonly PlacedPart[] placed;
    private int count;

    /// <summary>Makes the frame buffer of a rig.</summary>
    /// <param name="rig">The rig.</param>
    public RigPose(Rig rig)
    {
        ArgumentNullException.ThrowIfNull(rig);
        Rig = rig;
        bones = new Affine[rig.Bones.Count];
        boneParents = [.. rig.Bones.Select(bone => bone.Parent is null ? -1 : rig.BoneIndex(bone.Parent))];
        partBones = [.. rig.Parts.Select(part => rig.BoneIndex(part.Bone))];
        zoneBones = [.. rig.Zones.Select(zone => rig.BoneIndex(zone.Bone))];
        placed = new PlacedPart[rig.Parts.Count];
    }

    /// <summary>The rig.</summary>
    public Rig Rig { get; }

    /// <summary>The facing of the frame.</summary>
    public Facing Facing { get; private set; } = Facing.S;

    /// <summary>The covering variant of the frame.</summary>
    public Covering Covering { get; private set; }

    /// <summary>The view of the frame.</summary>
    public RigView View => Rig.ViewOf(Facing);

    /// <summary>The parts that are seen, the farthest first.</summary>
    public ReadOnlySpan<PlacedPart> Parts => placed.AsSpan(0, count);

    /// <summary>Composes the frame.</summary>
    /// <param name="animation">The animation.</param>
    /// <param name="time">The time in the animation, in seconds.</param>
    /// <param name="facing">The facing.</param>
    /// <param name="covering">The covering variant.</param>
    public void Sample(RigAnimation animation, double time, Facing facing, Covering covering)
    {
        ArgumentNullException.ThrowIfNull(animation);
        Facing = facing;
        Covering = covering;
        var view = View;
        var diagonal = facing is Facing.NE or Facing.SE or Facing.SW or Facing.NW;
        var root = Rig.IsMirrored(facing) ? Affine.MirrorX : Affine.Identity;

        for (var index = 0; index < bones.Length; index++)
        {
            var bone = Rig.Bones[index];
            var (moveX, moveY, turn) = animation.Sample(bone.Id, time, view);
            var local = Affine.Translation((view == RigView.Side ? bone.SideX : bone.X) + moveX, bone.Y + moveY).Then(Affine.Rotation(turn));
            bones[index] = (boneParents[index] < 0 ? root : bones[boneParents[index]]).Then(local);
        }

        count = 0;
        for (var index = 0; index < Rig.Parts.Count; index++)
        {
            var part = Rig.Parts[index];
            var placement = part.In(view);
            if (placement is null || !part.WornWith(covering))
            {
                continue;
            }

            var local = Affine.Translation(placement.X + (diagonal ? part.TurnX : 0), placement.Y).Then(Affine.Rotation(placement.Rotation));
            var entry = new PlacedPart(index, bones[partBones[index]].Then(local), placement.Depth);
            var slot = count++;
            while (slot > 0 && placed[slot - 1].Depth > entry.Depth)
            {
                placed[slot] = placed[slot - 1];
                slot--;
            }

            placed[slot] = entry;
        }
    }

    /// <summary>From the space of a zone's bone to the rig's space, in this frame.</summary>
    /// <param name="zoneIndex">The index of the zone in the rig.</param>
    public Affine ZoneTransform(int zoneIndex) => bones[zoneBones[zoneIndex]];
}
