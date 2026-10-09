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

    private static byte[] Read(string name)
    {
        using var stream = typeof(EmbeddedContent).Assembly.GetManifestResourceStream(name)
            ?? throw new ContentFormatException($"The embedded content {name} is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
