using System.Text.Json;

namespace AdamEve.IntegrationTests;

/// <summary>
/// What the host sends for the trial of the Three.js renderer (docs/spike-threejs.md): its module and the vendored
/// library as scripts of the site itself, under the policy of every other answer, compressed, and never as files a
/// browser may keep for a year (they have no fingerprint in their names).
/// </summary>
[TestFixture]
public class ThreeRendererRouteTests
{
    // No automatic decompression: the tests read the bytes as they were sent.
    private static readonly HttpClient Client = new(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.None })
    {
        BaseAddress = Site.BaseAddress,
    };

    [TestCase("js/render-three.js")]
    [TestCase("lib/three/three.module.min.js")]
    [TestCase("lib/three/three.core.min.js")]
    public async Task Get_AScriptOfTheTrial_WithBrotliAccepted_ShouldAnswerBrotliToAskForAgainEachTimeUnderThePolicyOfTheSite(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "br");

        using var response = await Client.SendAsync(request);

        ((int)response.StatusCode).ShouldBe(200);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/javascript");
        response.Content.Headers.ContentEncoding.ShouldBe(["br"]);
        var cache = response.Headers.CacheControl.ShouldNotBeNull();
        cache.NoCache.ShouldBeTrue();
        cache.ToString().ShouldNotContain("immutable");
        cache.MaxAge.ShouldBeNull();
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        policy.ShouldContain("script-src 'self' 'wasm-unsafe-eval'");
        policy.ShouldContain("style-src 'self';");
        policy.ShouldContain("connect-src 'self';");
        policy.ShouldContain("worker-src 'self';");
        policy.ShouldNotContain("unsafe-eval'; ");
        policy.ShouldNotContain("unsafe-inline");
        policy.ShouldNotContain("http");
    }

    [Test]
    public async Task Get_TheLicenceOfThree_ShouldBeServedBesideIt()
    {
        var licence = await Client.GetStringAsync("lib/three/LICENSE.txt");

        licence.ShouldContain("The MIT License");
        licence.ShouldContain("three.js authors");
    }

    [Test]
    public async Task Get_TheFilesOfTheSite_ShouldListTheFilesOfTheTrialAsLoadedOnDemandAndNoOther()
    {
        using var document = JsonDocument.Parse(await Client.GetStringAsync("_health/files.json"));
        var files = document.RootElement.GetProperty("files").EnumerateArray().Select(file => file.GetProperty("path").GetString()!).ToList();

        var onDemand = document.RootElement.GetProperty("onDemand").EnumerateArray().Select(path => path.GetString()!).ToList();

        onDemand.ShouldBe(files.Where(path => path == "js/render-three.js" || path.StartsWith("lib/three/", StringComparison.Ordinal)), ignoreOrder: true);
        onDemand.ShouldContain("lib/three/three.module.min.js");
        onDemand.ShouldContain("lib/three/three.core.min.js");
        files.Where(path => path.StartsWith("lib/", StringComparison.Ordinal)).ShouldAllBe(path => path.StartsWith("lib/three/", StringComparison.Ordinal));
        files.ShouldAllBe(path => !path.StartsWith("_framework/", StringComparison.Ordinal) || !path.Contains("three", StringComparison.OrdinalIgnoreCase));
    }
}
