using System.Reflection;

namespace AdamEve.Host;

/// <summary>
/// What the process says about the build it is: its version, and the facts the build wrote beside it.
/// </summary>
internal static class BuildInfo
{
    /// <summary>The file of the content root that holds the build facts (scripts/Write-BuildFacts.ps1).</summary>
    public const string FactsFile = "build-facts.json";

    /// <summary>The file of the content root that lists every file of the site with its size and SHA-256.</summary>
    public const string FilesFile = "files.json";

    /// <summary>The version the build compiled into the process (MAJOR.MINOR.run_number).</summary>
    public static string Version { get; } = VersionOf(
        typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>The version in an informational version, which may carry the commit after a plus sign.</summary>
    public static string VersionOf(string? informationalVersion)
    {
        var version = (informationalVersion ?? string.Empty).Split('+')[0].Trim();
        return version.Length > 0 ? version : "0.0.0";
    }
}
