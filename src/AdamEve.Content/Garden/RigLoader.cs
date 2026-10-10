using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdamEve.Core.Rigs;

namespace AdamEve.Content.Garden;

/// <summary>
/// Reads the rigs (<c>content/rigs/*.rig.json</c>: parts, bones, concealment zones, hair bindings, covering
/// variants) and their animations (<c>content/rigs/*.anim.json</c>: keyframes).
/// </summary>
public static class RigLoader
{
    /// <summary>Reads one rig.</summary>
    /// <param name="utf8Json">The bytes of the file.</param>
    /// <exception cref="ContentFormatException">The file is not a rig that holds together.</exception>
    public static Rig Parse(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            var file = JsonSerializer.Deserialize(utf8Json, GardenJsonContext.Default.RigFile) ?? throw new ContentFormatException("The rig is empty.");
            return new Rig(
                Required(file.Id, "id"),
                [.. Required(file.Bones, "bones").Select(bone => new Bone(Required(bone.Id, "bone id"), bone.Parent, bone.X, bone.Y, bone.SideX ?? bone.X))],
                [.. Required(file.Parts, "parts").Select(Part)],
                [.. Required(file.Zones, "zones").Select(Zone)],
                [.. Required(file.Concealment, "concealment").Select(Record)]);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new ContentFormatException($"The rig cannot be read: {exception.Message}", exception);
        }
    }

    /// <summary>Reads the animations of a file.</summary>
    /// <param name="utf8Json">The bytes of the file.</param>
    /// <exception cref="ContentFormatException">The file is not a list of animations.</exception>
    public static IReadOnlyList<RigAnimation> ParseAnimations(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            var file = JsonSerializer.Deserialize(utf8Json, GardenJsonContext.Default.AnimationFile) ?? throw new ContentFormatException("The animations are empty.");
            return [.. Required(file.Animations, "animations").Select(animation =>
            {
                if (animation.Duration <= 0)
                {
                    throw new ContentFormatException($"The animation \"{animation.Id}\" has no length.");
                }

                return new RigAnimation(
                    Required(animation.Id, "animation id"),
                    animation.Duration,
                    [.. Required(animation.Tracks, "tracks").Select(track => new AnimationTrack(
                        Required(track.Bone, "track bone"),
                        track.FrontRotation ?? 1,
                        [.. Required(track.Keys, "keys").Select(key => new Keyframe(key.Time, key.X, key.Y, key.Rotation))]))]);
            })];
        }
        catch (JsonException exception)
        {
            throw new ContentFormatException($"The animations cannot be read: {exception.Message}", exception);
        }
    }

    /// <summary>Reads a colour written as "#RRGGBB".</summary>
    /// <param name="text">The text.</param>
    /// <exception cref="ContentFormatException">The text is not such a colour.</exception>
    public static int Colour(string? text)
    {
        if (text is { Length: 7 } && text[0] == '#' && int.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var colour))
        {
            return colour;
        }

        throw new ContentFormatException($"\"{text}\" is not a colour as #RRGGBB.");
    }

    private static RigPart Part(PartFile part)
    {
        var id = Required(part.Id, "part id");
        var hidden = part.Hide ?? [];
        PartPlacement? In(string view, PlacementFile? own)
        {
            if (hidden.Contains(view, StringComparer.Ordinal))
            {
                return null;
            }

            var placement = own ?? part.At ?? throw new ContentFormatException($"The part \"{id}\" has no place seen from the {view}.");
            return new PartPlacement(placement.X, placement.Y, placement.Depth, placement.Rotation);
        }

        return new RigPart(
            id,
            Required(part.Bone, "part bone"),
            Name<PartShape>(part.Shape),
            part.W,
            part.H,
            Colour(part.Colour),
            Name<PartRole>(part.Role),
            [.. (part.Coverings ?? []).Select(Name<Covering>)],
            In("front", part.Front),
            In("side", part.Side),
            In("back", part.Back),
            part.TurnX,
            part.Round);
    }

    private static ConcealmentZone Zone(ZoneFile zone)
    {
        var depth = Required(zone.Depth, "zone depth");
        return new ConcealmentZone(Required(zone.Id, "zone id"), Required(zone.Bone, "zone bone"), Required(zone.On, "zone body part"), Required(zone.Points, "zone points"), depth.Front, depth.Side, depth.Back);
    }

    private static ConcealmentRecord Record(RecordFile record) => new(
        Required(record.Zone, "record zone"),
        Name<Covering>(record.Covering),
        Name<RigView>(record.View),
        record.FacingAway,
        [.. (record.By ?? []).Select(Name<PartRole>)],
        record.Plain);

    private static T Name<T>(string? text)
        where T : struct, Enum
    {
        // A name of the list, as written there but for its case; a number is not a name.
        if (text is not null && !char.IsAsciiDigit(text.FirstOrDefault()) && text.All(char.IsAsciiLetter) && Enum.TryParse<T>(text, ignoreCase: true, out var value))
        {
            return value;
        }

        throw new ContentFormatException($"\"{text}\" is not one of: {string.Join(", ", Enum.GetNames<T>())}.");
    }

    private static T Required<T>(T? value, string what)
        where T : class => value ?? throw new ContentFormatException($"The file has no {what}.");
}

internal sealed class RigFile
{
    public string? Id { get; set; }

    public List<BoneFile>? Bones { get; set; }

    public List<PartFile>? Parts { get; set; }

    public List<ZoneFile>? Zones { get; set; }

    public List<RecordFile>? Concealment { get; set; }
}

internal sealed class BoneFile
{
    public string? Id { get; set; }

    public string? Parent { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public double? SideX { get; set; }
}

internal sealed class PlacementFile
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Depth { get; set; }

    public double Rotation { get; set; }
}

internal sealed class PartFile
{
    public string? Id { get; set; }

    public string? Bone { get; set; }

    public string? Shape { get; set; }

    public double W { get; set; }

    public double H { get; set; }

    public string? Colour { get; set; }

    public string? Role { get; set; }

    public List<string>? Coverings { get; set; }

    public PlacementFile? At { get; set; }

    public PlacementFile? Front { get; set; }

    public PlacementFile? Side { get; set; }

    public PlacementFile? Back { get; set; }

    public List<string>? Hide { get; set; }

    public double TurnX { get; set; }

    public double Round { get; set; }
}

internal sealed class ZoneDepthFile
{
    public double Front { get; set; }

    public double Side { get; set; }

    public double Back { get; set; }
}

internal sealed class ZoneFile
{
    public string? Id { get; set; }

    public string? Bone { get; set; }

    public string? On { get; set; }

    public List<double>? Points { get; set; }

    public ZoneDepthFile? Depth { get; set; }
}

internal sealed class RecordFile
{
    public string? Zone { get; set; }

    public string? Covering { get; set; }

    public string? View { get; set; }

    public bool FacingAway { get; set; }

    public bool Plain { get; set; }

    public List<string>? By { get; set; }
}

internal sealed class AnimationFile
{
    public List<AnimationEntryFile>? Animations { get; set; }
}

internal sealed class AnimationEntryFile
{
    public string? Id { get; set; }

    public double Duration { get; set; }

    public List<TrackFile>? Tracks { get; set; }
}

internal sealed class TrackFile
{
    public string? Bone { get; set; }

    public double? FrontRotation { get; set; }

    public List<KeyFile>? Keys { get; set; }
}

internal sealed class KeyFile
{
    public double Time { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Rotation { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RigFile))]
[JsonSerializable(typeof(AnimationFile))]
internal sealed partial class GardenJsonContext : JsonSerializerContext;
