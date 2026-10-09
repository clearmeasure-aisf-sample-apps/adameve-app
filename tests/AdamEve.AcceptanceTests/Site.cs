namespace AdamEve.AcceptanceTests;

/// <summary>
/// The published site, served by the published host (src/AdamEve.Host) as a process. The build starts it and names
/// its address in ADAMEVE_BASE_URL (build.ps1, Start-Site).
/// </summary>
internal static class Site
{
    public static string BaseAddress
    {
        get
        {
            var address = Environment.GetEnvironmentVariable("ADAMEVE_BASE_URL");
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new InvalidOperationException(
                    "ADAMEVE_BASE_URL is not set. Run the private build (pwsh ./PrivateBuild.ps1): it publishes the site and starts its host.");
            }

            return address.TrimEnd('/') + "/";
        }
    }
}
