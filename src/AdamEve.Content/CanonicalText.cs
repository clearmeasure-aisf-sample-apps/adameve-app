namespace AdamEve.Content;

/// <summary>
/// The one source of quoted Scripture: Genesis 1 to 3 in the King James Version, the file the repository holds byte
/// for byte as <c>content/kjv-genesis-1-3.txt</c>.
/// </summary>
public static class CanonicalText
{
    /// <summary>The path of the file, from the root of the repository.</summary>
    public const string RepositoryPath = "content/kjv-genesis-1-3.txt";

    /// <summary>The SHA-256 of the file, in lower-case hexadecimal. A change of one byte is a change of this value.</summary>
    public const string Sha256 = "b2eae7f0b5db545ca31502ef5a2f1bcb6853eee763a80a48553c13b876600e6f";
}
