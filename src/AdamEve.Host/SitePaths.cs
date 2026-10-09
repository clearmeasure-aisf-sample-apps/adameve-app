namespace AdamEve.Host;

/// <summary>
/// What a path of the site is, and how long a browser may keep its answer.
/// </summary>
internal static class SitePaths
{
    /// <summary>The page every navigation gets: the game is one page and routes in the browser.</summary>
    public const string Index = "/index.html";

    /// <summary>Health: 200 and "Healthy" when the process has the site it is to serve.</summary>
    public const string Health = "/_healthcheck";

    /// <summary>Liveness: 200 whenever the process answers.</summary>
    public const string Alive = "/alive";

    /// <summary>The version of the build the process is.</summary>
    public const string Version = "/_version";

    /// <summary>The build facts.</summary>
    public const string Build = "/_build";

    /// <summary>Every file of the site with its size and SHA-256, for verify.ps1.</summary>
    public const string Files = "/_health/files.json";

    private const string Immutable = "public, max-age=31536000, immutable";

    // Every file under these folders has a fingerprint in its name (the build fails otherwise: step StaticFiles).
    private static readonly string[] FingerprintedFolders = ["/_framework/", "/assets/"];

    /// <summary>
    /// True for the paths a dashboard in another origin reads: they allow every origin and are never kept.
    /// </summary>
    public static bool IsOpenToEveryOrigin(string path) =>
        path is Health or Alive or Version or Build;

    /// <summary>
    /// True for a path a person navigates to: no file name at its end and not one of the site's own paths. Such a
    /// path gets index.html; every other path that names nothing gets a 404.
    /// </summary>
    public static bool IsNavigation(string path)
    {
        if (path.StartsWith("/_", StringComparison.Ordinal) || path == Alive || IsFingerprinted(path))
        {
            return false;
        }

        var lastSegment = path[(path.LastIndexOf('/') + 1)..];
        return !lastSegment.Contains('.', StringComparison.Ordinal);
    }

    /// <summary>
    /// The Cache-Control of an answer with <paramref name="statusCode"/> to <paramref name="path"/>, the path as
    /// asked. A fingerprinted file is kept for a year; the health paths are never kept; everything else (index.html
    /// first) is asked for again each time, and answered with 304 when it has not changed.
    /// </summary>
    public static string CacheControl(string path, int statusCode)
    {
        if (IsOpenToEveryOrigin(path) || path == Files)
        {
            return "no-store";
        }

        var found = statusCode is StatusCodes.Status200OK or StatusCodes.Status206PartialContent or StatusCodes.Status304NotModified;
        return found && IsFingerprinted(path) ? Immutable : "no-cache";
    }

    private static bool IsFingerprinted(string path) =>
        FingerprintedFolders.Any(folder => path.StartsWith(folder, StringComparison.Ordinal));
}
