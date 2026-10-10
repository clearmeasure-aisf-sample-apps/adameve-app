namespace AdamEve.Content;

/// <summary>The content files this assembly embeds from <c>content/</c>, as the bytes the build read.</summary>
public static class EmbeddedContent
{
    private const string ScriptureResource = "AdamEve.Content.kjv-genesis-1-3.txt";
    private const string GlossaryResource = "AdamEve.Content.glossary.json";

    /// <summary>The bytes of the canonical text, <c>content/kjv-genesis-1-3.txt</c>.</summary>
    /// <exception cref="ContentFormatException">The assembly does not hold the file.</exception>
    public static byte[] Scripture() => Read(ScriptureResource);

    /// <summary>The bytes of the glossary, <c>content/glossary.json</c>.</summary>
    /// <exception cref="ContentFormatException">The assembly does not hold the file.</exception>
    public static byte[] Glossary() => Read(GlossaryResource);

    /// <summary>The bytes of the animals and their kind-names, <c>content/animals.json</c>.</summary>
    /// <exception cref="ContentFormatException">The assembly does not hold the file.</exception>
    public static byte[] Animals() => Read("AdamEve.Content.animals.json");

    /// <summary>The bytes of the map of the garden, <c>content/maps/garden.tmj</c>.</summary>
    /// <exception cref="ContentFormatException">The assembly does not hold the file.</exception>
    public static byte[] Map() => Read("AdamEve.Content.maps.garden.tmj");

    /// <summary>The bytes of a rig, <c>content/rigs/&lt;id&gt;.rig.json</c>.</summary>
    /// <param name="id">The id of the rig: "adam" or "woman".</param>
    /// <exception cref="ContentFormatException">The assembly does not hold the file.</exception>
    public static byte[] Rig(string id) => Read($"AdamEve.Content.rigs.{id}.rig.json");

    /// <summary>The bytes of the animations, <c>content/rigs/person.anim.json</c>.</summary>
    /// <exception cref="ContentFormatException">The assembly does not hold the file.</exception>
    public static byte[] Animations() => Read("AdamEve.Content.rigs.person.anim.json");

    private static byte[] Read(string name)
    {
        using var stream = typeof(EmbeddedContent).Assembly.GetManifestResourceStream(name)
            ?? throw new ContentFormatException($"The embedded content {name} is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
