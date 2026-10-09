using System.Text.Json;

namespace AdamEve.IntegrationTests;

/// <summary>
/// What the host sends for the garden: the page as a deep link, the two modules of the game as scripts of the site
/// itself, and no sound, font or image file that somebody would have had to generate.
/// </summary>
[TestFixture]
public class GardenRouteTests
{
    private static readonly HttpClient Client = new() { BaseAddress = Site.BaseAddress };

    [Test]
    public async Task Get_TheGardenAsADeepLink_ShouldAnswerThePageOfTheSite()
    {
        var index = await Client.GetStringAsync(string.Empty);

        using var response = await Client.GetAsync("garden");
        var body = await response.Content.ReadAsStringAsync();

        ((int)response.StatusCode).ShouldBe(200);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/html");
        body.ShouldBe(index);
    }

    [TestCase("js/render.js")]
    [TestCase("js/audio.js")]
    public async Task Get_AModuleOfTheGame_ShouldBeAScriptOfTheSiteThatNamesNoOtherOrigin(string path)
    {
        using var response = await Client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        ((int)response.StatusCode).ShouldBe(200);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/javascript");
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("script-src 'self' 'wasm-unsafe-eval'");
        response.Headers.CacheControl.ShouldNotBeNull().NoCache.ShouldBeTrue();
        foreach (var forbidden in new[] { "http:", "https:", "//", "fetch(", "XMLHttpRequest", "WebSocket", "sendBeacon", "eval(", "new Function", "document.cookie", "innerHTML", ".style" })
        {
            body.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)).ShouldAllBe(line => !line.Contains(forbidden, StringComparison.Ordinal), forbidden);
        }
    }

    [Test]
    public async Task Get_TheFilesOfTheSite_ShouldHoldNoSoundNoFontAndNoImageButTheIcon()
    {
        using var document = JsonDocument.Parse(await Client.GetStringAsync("_health/files.json"));
        var paths = document.RootElement.GetProperty("files").EnumerateArray().Select(file => file.GetProperty("path").GetString()!).ToList();
        string[] generated = [".m4a", ".mp3", ".ogg", ".wav", ".aac", ".flac", ".webm", ".mp4", ".webp", ".jpg", ".jpeg", ".gif", ".svg", ".avif", ".bmp", ".woff", ".woff2", ".ttf", ".otf"];

        paths.ShouldContain("js/render.js");
        paths.ShouldContain("js/audio.js");
        paths.Where(path => generated.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)).ShouldBeEmpty();
        paths.Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ShouldBe(["icon-192.png"]);
    }
}
