using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AdamEve.Host;

/// <summary>
/// Healthy when the process has the site it is to serve: index.html and the runtime under _framework. A process
/// that runs without them answers every page with a 404, and says so here.
/// </summary>
internal sealed class SiteHealthCheck(IWebHostEnvironment environment) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var files = environment.WebRootFileProvider;
        var healthy = files.GetFileInfo("index.html").Exists && files.GetDirectoryContents("_framework").Any();
        return Task.FromResult(healthy
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The web root has no index.html or no _framework folder."));
    }
}
