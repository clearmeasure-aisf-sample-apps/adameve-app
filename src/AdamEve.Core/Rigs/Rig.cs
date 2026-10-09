using AdamEve.Core.World;

namespace AdamEve.Core.Rigs;

/// <summary>What covers a character (design, sections 1 and 5.3). It drives the modesty rule M1.</summary>
public enum Covering
{
    /// <summary>Before Genesis 3:7: companion foliage and hair.</summary>
    None,

    /// <summary>Genesis 3:7 to 3:21: the fig-leaf apron replaces the foliage; hair still covers the woman's chest.</summary>
    Aprons,

    /// <summary>From Genesis 3:21: the coats of skins.</summary>
    Coats,
}

/// <summary>How a rig is seen: its three views. A facing to the west is the mirror of the one to the east.</summary>
public enum RigView
{
    /// <summary>Seen from the front: S, SE, SW.</summary>
    Front,

    /// <summary>Seen from the side: E, W.</summary>
    Side,

    /// <summary>Seen from behind: N, NE, NW.</summary>
    Back,
}

/// <summary>The outline of a part. Placeholder parts are these two shapes in one flat colour.</summary>
public enum PartShape
{
    /// <summary>An ellipse that fills the part's width and height.</summary>
    Ellipse,

    /// <summary>A rectangle of the part's width and height.</summary>
    Rectangle,
}

/// <summary>What a part is, for the modesty rule M1.</summary>
public enum PartRole
{
    /// <summary>The smooth body, without anatomical detail.</summary>
    Body,

    /// <summary>A hair layer, bound to the head.</summary>
    Hair,

    /// <summary>Companion foliage: plants of the garden, bound to the hip and drawn in the layer in front of the character.</summary>
    Occluder,

    /// <summary>The fig-leaf apron (Genesis 3:7).</summary>
    Apron,

    /// <summary>The coat of skins (Genesis 3:21).</summary>
    Coat,

    /// <summary>A detail that covers nothing: the eyes.</summary>
    Detail,
}

/// <summary>A bone of the small skeleton.</summary>
/// <param name="Id">The id.</param>
/// <param name="Parent">The id of the bone it hangs on; null for the root, at the feet.</param>
/// <param name="X">Where it sits on its parent, seen from the front or from behind.</param>
/// <param name="Y">Where it sits on its parent; y grows downward.</param>
/// <param name="SideX">Where it sits on its parent, seen from the side.</param>
public sealed record Bone(string Id, string? Parent, double X, double Y, double SideX);

/// <summary>Where a part sits on its bone in one view.</summary>
/// <param name="X">The centre of the part, in the bone's space.</param>
/// <param name="Y">The centre of the part, in the bone's space.</param>
/// <param name="Depth">The place in the rig's depth order: a greater depth is nearer the viewer.</param>
/// <param name="Rotation">The turn of the part about its centre, in degrees.</param>
public sealed record PartPlacement(double X, double Y, double Depth, double Rotation);

/// <summary>A part of a rig: one flat shape bound to a bone.</summary>
/// <param name="Id">The id.</param>
/// <param name="Bone">The bone it is bound to.</param>
/// <param name="Shape">Its outline.</param>
/// <param name="Width">Its width in logical pixels.</param>
/// <param name="Height">Its height in logical pixels.</param>
/// <param name="Colour">Its colour, 0xRRGGBB. A part is opaque.</param>
/// <param name="Role">What it is.</param>
/// <param name="Coverings">The covering variants it belongs to; empty for all.</param>
/// <param name="Front">Where it sits seen from the front; null when it is not seen.</param>
/// <param name="Side">Where it sits seen from the side; null when it is not seen.</param>
/// <param name="Back">Where it sits seen from behind; null when it is not seen.</param>
/// <param name="TurnX">How far it shifts toward the facing in the four diagonal facings.</param>
public sealed record RigPart(
    string Id,
    string Bone,
    PartShape Shape,
    double Width,
    double Height,
    int Colour,
    PartRole Role,
    IReadOnlyList<Covering> Coverings,
    PartPlacement? Front,
    PartPlacement? Side,
    PartPlacement? Back,
    double TurnX)
{
    /// <summary>Where the part sits in a view; null when it is not seen there.</summary>
    /// <param name="view">The view.</param>
    public PartPlacement? In(RigView view) => view switch
    {
        RigView.Front => Front,
        RigView.Side => Side,
        _ => Back,
    };

    /// <summary>Whether the part belongs to a covering variant.</summary>
    /// <param name="covering">The variant.</param>
    public bool WornWith(Covering covering) => Coverings.Count == 0 || Coverings.Contains(covering);
}

/// <summary>
/// A concealment zone (design, section 5.3): a polygon on the surface of a body part, bound to that part's bone.
/// The pelvic zone for both; the chest zone for the woman.
/// </summary>
/// <param name="Id">The id: "pelvis" or "chest".</param>
/// <param name="Bone">The bone it is bound to.</param>
/// <param name="On">The body part whose surface it lies on.</param>
/// <param name="Points">The corners: x, y, x, y, in the bone's space.</param>
/// <param name="FrontDepth">Its depth seen from the front.</param>
/// <param name="SideDepth">Its depth seen from the side.</param>
/// <param name="BackDepth">Its depth seen from behind: below its body part when it is on the far side.</param>
public sealed record ConcealmentZone(string Id, string Bone, string On, IReadOnlyList<double> Points, double FrontDepth, double SideDepth, double BackDepth)
{
    /// <summary>The depth of the zone in a view.</summary>
    /// <param name="view">The view.</param>
    public double DepthIn(RigView view) => view switch
    {
        RigView.Front => FrontDepth,
        RigView.Side => SideDepth,
        _ => BackDepth,
    };
}

/// <summary>
/// A concealment record: how one zone is concealed in one view under one covering variant. A frame without a valid
/// record for each of its zones fails closed (design, section 1).
/// </summary>
/// <param name="Zone">The zone.</param>
/// <param name="Covering">The covering variant.</param>
/// <param name="View">The view.</param>
/// <param name="FacingAway">The zone is turned away from the viewer. Only the back view may declare it.</param>
/// <param name="By">The roles of the parts that cover the zone, when it is not turned away.</param>
public sealed record ConcealmentRecord(string Zone, Covering Covering, RigView View, bool FacingAway, IReadOnlyList<PartRole> By);

/// <summary>
/// A cut-out rig (design, section 5.3): parts on a small skeleton, its concealment zones and how each is concealed.
/// </summary>
public sealed class Rig
{
    private static readonly PartRole[] CoveringRoles = [PartRole.Hair, PartRole.Occluder, PartRole.Apron, PartRole.Coat];

    // The first record of each zone, covering variant and view, when it is one that can conceal.
    private readonly ConcealmentRecord?[] records;

    /// <summary>Makes a rig and checks that it holds together.</summary>
    /// <param name="id">The id: "adam" or "woman".</param>
    /// <param name="bones">The bones, a parent before its children.</param>
    /// <param name="parts">The parts.</param>
    /// <param name="zones">The concealment zones.</param>
    /// <param name="concealment">The concealment records.</param>
    /// <exception cref="ArgumentException">A name is used twice or names nothing, or a shape has no size.</exception>
    public Rig(string id, IReadOnlyList<Bone> bones, IReadOnlyList<RigPart> parts, IReadOnlyList<ConcealmentZone> zones, IReadOnlyList<ConcealmentRecord> concealment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(bones);
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(concealment);
        Id = id;
        Bones = [.. bones];
        Parts = [.. parts];
        Zones = [.. zones];
        Concealment = [.. concealment];

        for (var index = 0; index < Bones.Count; index++)
        {
            var bone = Bones[index];
            if (BoneIndex(bone.Id) != index)
            {
                throw new ArgumentException($"The rig {id} has the bone \"{bone.Id}\" twice.", nameof(bones));
            }

            var parent = bone.Parent is null ? -1 : BoneIndex(bone.Parent);
            if ((bone.Parent is null) != (index == 0) || parent >= index)
            {
                throw new ArgumentException($"The bone \"{bone.Id}\" of the rig {id} does not come after its parent.", nameof(bones));
            }
        }

        for (var index = 0; index < Parts.Count; index++)
        {
            var part = Parts[index];
            if (PartIndex(part.Id) != index || BoneIndex(part.Bone) < 0 || part.Width <= 0 || part.Height <= 0)
            {
                throw new ArgumentException($"The part \"{part.Id}\" of the rig {id} is there twice, has no bone or has no size.", nameof(parts));
            }
        }

        for (var index = 0; index < Zones.Count; index++)
        {
            var zone = Zones[index];
            if (ZoneIndex(zone.Id) != index || BoneIndex(zone.Bone) < 0 || PartIndex(zone.On) < 0 || zone.Points.Count < 6 || zone.Points.Count % 2 != 0)
            {
                throw new ArgumentException($"The zone \"{zone.Id}\" of the rig {id} is there twice, has no bone, no body part or no polygon.", nameof(zones));
            }

            var carrier = Parts[PartIndex(zone.On)];
            if (carrier.Role != PartRole.Body || carrier.Bone != zone.Bone)
            {
                throw new ArgumentException($"The zone \"{zone.Id}\" of the rig {id} does not lie on a body part of its own bone.", nameof(zones));
            }
        }

        records = new ConcealmentRecord?[Zones.Count * 9];
        var seen = new bool[records.Length];
        foreach (var record in Concealment)
        {
            var zone = ZoneIndex(record.Zone);
            if (zone < 0 || !Enum.IsDefined(record.Covering) || !Enum.IsDefined(record.View))
            {
                throw new ArgumentException($"A concealment record of the rig {id} names the zone \"{record.Zone}\", a covering or a view it does not have.", nameof(concealment));
            }

            var slot = (((zone * 3) + (int)record.Covering) * 3) + (int)record.View;
            if (seen[slot])
            {
                continue;
            }

            seen[slot] = true;
            var conceals = record.FacingAway || (record.By.Count > 0 && record.By.All(role => CoveringRoles.Contains(role)));
            records[slot] = conceals ? record : null;
        }
    }

    /// <summary>The id.</summary>
    public string Id { get; }

    /// <summary>The bones, a parent before its children.</summary>
    public IReadOnlyList<Bone> Bones { get; }

    /// <summary>The parts.</summary>
    public IReadOnlyList<RigPart> Parts { get; }

    /// <summary>The concealment zones.</summary>
    public IReadOnlyList<ConcealmentZone> Zones { get; }

    /// <summary>The concealment records.</summary>
    public IReadOnlyList<ConcealmentRecord> Concealment { get; }

    /// <summary>The view of a facing.</summary>
    /// <param name="facing">The facing.</param>
    public static RigView ViewOf(Facing facing) => facing switch
    {
        Facing.S or Facing.SE or Facing.SW => RigView.Front,
        Facing.E or Facing.W => RigView.Side,
        _ => RigView.Back,
    };

    /// <summary>Whether a facing is drawn as the mirror of its eastern twin.</summary>
    /// <param name="facing">The facing.</param>
    public static bool IsMirrored(Facing facing) => facing is Facing.SW or Facing.W or Facing.NW;

    /// <summary>The index of a bone, or -1.</summary>
    /// <param name="id">The id of the bone.</param>
    public int BoneIndex(string id) => IndexOf(Bones, id, bone => bone.Id);

    /// <summary>The index of a part, or -1.</summary>
    /// <param name="id">The id of the part.</param>
    public int PartIndex(string id) => IndexOf(Parts, id, part => part.Id);

    /// <summary>The index of a zone, or -1.</summary>
    /// <param name="id">The id of the zone.</param>
    public int ZoneIndex(string id) => IndexOf(Zones, id, zone => zone.Id);

    /// <summary>
    /// The valid concealment record of a zone for a facing under a covering variant, or null. A record that declares
    /// "facing away" is valid only for the three back facings, and a record that names no covering role is not a
    /// record: both leave the frame without one, and it fails closed.
    /// </summary>
    /// <param name="zone">The zone.</param>
    /// <param name="facing">The facing.</param>
    /// <param name="covering">The covering variant.</param>
    public ConcealmentRecord? RecordFor(ConcealmentZone zone, Facing facing, Covering covering)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var zoneIndex = ZoneIndex(zone.Id);
        if (zoneIndex < 0)
        {
            return null;
        }

        var record = records[(((zoneIndex * 3) + (int)covering) * 3) + (int)ViewOf(facing)];
        return record is { FacingAway: true } && !facing.IsTurnedAway() ? null : record;
    }

    private static int IndexOf<T>(IReadOnlyList<T> items, string id, Func<T, string> name)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (string.Equals(name(items[index]), id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
