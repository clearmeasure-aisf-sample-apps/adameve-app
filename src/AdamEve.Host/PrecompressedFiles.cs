using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace AdamEve.Host;

/// <summary>
/// The compressed copies the publish step writes beside a file (name.br, name.gz): which one answers a request, and
/// what the answer says it is.
/// </summary>
internal static class PrecompressedFiles
{
    // The order of preference: brotli is the smaller one.
    private static readonly (string Coding, string Extension)[] Codings = [("br", ".br"), ("gzip", ".gz")];

    /// <summary>True for the path of a compressed copy. Nobody asks for one by its own name.</summary>
    public static bool IsCopy(string path) =>
        Codings.Any(coding => path.EndsWith(coding.Extension, StringComparison.OrdinalIgnoreCase));

    /// <summary>The content coding of a file by its name: "br", "gzip", or null for a file as it is.</summary>
    public static string? CodingOf(string fileName) =>
        Codings.FirstOrDefault(coding => fileName.EndsWith(coding.Extension, StringComparison.OrdinalIgnoreCase)).Coding;

    /// <summary>
    /// The copy of <paramref name="path"/> to answer with for a request that accepts
    /// <paramref name="acceptEncoding"/>: the first coding the request accepts that has a copy. Null: the file as it
    /// is.
    /// </summary>
    public static string? Choose(string path, IEnumerable<string?> acceptEncoding, Func<string, bool> exists)
    {
        if (!StringWithQualityHeaderValue.TryParseList(acceptEncoding.Where(value => value is not null).ToList()!, out var accepted))
        {
            return null;
        }

        foreach (var (coding, extension) in Codings)
        {
            var accepts = accepted.Any(value => value.Value.Equals(coding, StringComparison.OrdinalIgnoreCase) && value.Quality is null or > 0);
            if (accepts && exists(path + extension))
            {
                return path + extension;
            }
        }

        return null;
    }

    /// <summary>
    /// The content type of a file by its name; a compressed copy has the type of the file it is a copy of.
    /// </summary>
    public sealed class ContentTypes : IContentTypeProvider
    {
        private readonly FileExtensionContentTypeProvider known = new()
        {
            Mappings =
            {
                [".dat"] = "application/octet-stream",
                [".m4a"] = "audio/mp4",
                [".webmanifest"] = "application/manifest+json",
            },
        };

        /// <inheritdoc />
        public bool TryGetContentType(string subpath, out string contentType)
        {
            var name = CodingOf(subpath) is null ? subpath : subpath[..subpath.LastIndexOf('.')];
            return this.known.TryGetContentType(name, out contentType!);
        }
    }
}
