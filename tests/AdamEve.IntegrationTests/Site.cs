namespace AdamEve.IntegrationTests;

/// <summary>
/// The published site, served by the Static Web Apps CLI emulator. The build starts the emulator and names its
/// address in ADAMEVE_BASE_URL (build.ps1, Start-SiteEmulator).
/// </summary>
internal static class Site
{
    public static Uri BaseAddress
    {
        get
        {
            var address = Environment.GetEnvironmentVariable("ADAMEVE_BASE_URL");
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new InvalidOperationException(
                    "ADAMEVE_BASE_URL is not set. Run the private build (pwsh ./PrivateBuild.ps1): it publishes the site and serves it with the emulator.");
            }

            return new Uri(address.TrimEnd('/') + "/");
        }
    }
}
