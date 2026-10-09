using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AdamEve.Host;

/// <summary>
/// The content security policy of the site: everything from the site itself, nothing from anywhere else, and no
/// inline script but one.
/// </summary>
/// <remarks>
/// .NET 10 writes an import map into the published index.html: the fingerprinted name of every script of the
/// runtime. It is an inline script, which <c>script-src 'self'</c> refuses, and without it the runtime does not
/// start. The policy therefore names that one import map by its SHA-256, taken from the index.html the process
/// serves. No other inline script runs.
/// </remarks>
internal static partial class ContentSecurityPolicy
{
    private const string ScriptSource = "script-src 'self' 'wasm-unsafe-eval'";

    /// <summary>The policy for a site whose index.html is <paramref name="indexHtml"/>.</summary>
    public static string For(string? indexHtml)
    {
        var hash = ImportMapHash(indexHtml);
        var scripts = hash is null ? ScriptSource : $"{ScriptSource} 'sha256-{hash}'";
        return $"default-src 'self'; {scripts}; style-src 'self'; img-src 'self' data: blob:; connect-src 'self'; "
            + "font-src 'self'; media-src 'self'; worker-src 'self'; frame-ancestors 'none'";
    }

    /// <summary>
    /// The SHA-256 of the import map of <paramref name="indexHtml"/> in base 64, as a browser computes it (line ends
    /// as LF); null when the page has no import map or an empty one.
    /// </summary>
    public static string? ImportMapHash(string? indexHtml)
    {
        if (string.IsNullOrEmpty(indexHtml))
        {
            return null;
        }

        var match = ImportMap().Match(indexHtml);
        if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[1].Value))
        {
            return null;
        }

        var text = match.Groups[1].Value.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    [GeneratedRegex("<script type=\"importmap\">(.*?)</script>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ImportMap();
}
