using AdamEve.Core.Rigs;
using AdamEve.Core.World;

namespace AdamEve.Content.Garden;

/// <summary>What slice S2 walks: the map of the garden, the rigs of Adam and the woman, and their animations.</summary>
public sealed class GardenContent
{
    private GardenContent(TileMap map, Rig adam, Rig woman, IReadOnlyList<RigAnimation> animations)
    {
        Map = map;
        Adam = adam;
        Woman = woman;
        Animations = animations;
    }

    /// <summary>The map.</summary>
    public TileMap Map { get; }

    /// <summary>The rig of Adam.</summary>
    public Rig Adam { get; }

    /// <summary>The rig of the woman.</summary>
    public Rig Woman { get; }

    /// <summary>The animations, the same for both rigs.</summary>
    public IReadOnlyList<RigAnimation> Animations { get; }

    /// <summary>
    /// Loads and checks the garden: the map has a starting tile for each character; both rigs have the pelvic zone
    /// and the woman's the chest zone as well (design, section 5.3); both rigs hold only what the amended modesty
    /// rule allows (<see cref="RigStructure"/>); the animations are "idle" and "walk", and move only bones both
    /// rigs have.
    /// </summary>
    /// <param name="map">The bytes of <c>content/maps/garden.tmj</c>.</param>
    /// <param name="adam">The bytes of <c>content/rigs/adam.rig.json</c>.</param>
    /// <param name="woman">The bytes of <c>content/rigs/woman.rig.json</c>.</param>
    /// <param name="animations">The bytes of <c>content/rigs/person.anim.json</c>.</param>
    /// <exception cref="ContentFormatException">A check failed.</exception>
    public static GardenContent Load(ReadOnlySpan<byte> map, ReadOnlySpan<byte> adam, ReadOnlySpan<byte> woman, ReadOnlySpan<byte> animations)
    {
        var garden = new GardenContent(MapLoader.Parse(map), RigLoader.Parse(adam), RigLoader.Parse(woman), RigLoader.ParseAnimations(animations));
        if (garden.Adam.Id != "adam" || garden.Woman.Id != "woman")
        {
            throw new ContentFormatException("The rigs are not those of Adam and the woman.");
        }

        foreach (var id in new[] { "adam", "woman" })
        {
            if (!garden.Map.SpawnIds.Contains(id))
            {
                throw new ContentFormatException($"The map has no starting tile \"{id}\".");
            }
        }

        if (garden.Adam.ZoneIndex("pelvis") < 0 || garden.Woman.ZoneIndex("pelvis") < 0 || garden.Woman.ZoneIndex("chest") < 0)
        {
            throw new ContentFormatException("A rig lacks a concealment zone: the pelvic zone for both, the chest zone for the woman.");
        }

        // The figures are never anatomical (design, decision D18): a rig that holds a shape, a kind, a name or a
        // colour that is not on the lists of RigStructure does not load.
        foreach (var rig in new[] { garden.Adam, garden.Woman })
        {
            if (RigStructure.Violations(rig) is { Count: > 0 } violations)
            {
                throw new ContentFormatException(violations[0]);
            }
        }

        foreach (var name in new[] { "idle", "walk" })
        {
            var animation = garden.Animations.FirstOrDefault(candidate => candidate.Id == name)
                ?? throw new ContentFormatException($"There is no animation \"{name}\".");
            foreach (var track in animation.Tracks)
            {
                if (garden.Adam.BoneIndex(track.Bone) < 0 || garden.Woman.BoneIndex(track.Bone) < 0)
                {
                    throw new ContentFormatException($"The animation \"{name}\" moves the bone \"{track.Bone}\", which a rig does not have.");
                }
            }
        }

        return garden;
    }

    /// <summary>Loads and checks the garden this assembly embeds.</summary>
    /// <exception cref="ContentFormatException">A check failed.</exception>
    public static GardenContent LoadEmbedded() =>
        Load(EmbeddedContent.Map(), EmbeddedContent.Rig("adam"), EmbeddedContent.Rig("woman"), EmbeddedContent.Animations());
}
