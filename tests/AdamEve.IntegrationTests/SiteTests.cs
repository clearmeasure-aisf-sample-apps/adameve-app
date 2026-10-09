using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AdamEve.IntegrationTests;

/// <summary>
/// What the host sends for the files of the site: the security headers, how long a browser may keep an answer,
/// brotli where the browser accepts it, index.html for a navigation and 404 for a file that is not there.
/// </summary>
[TestFixture]
public partial class SiteTests
{
    private const string Title = "Adam and woman in the garden of Eden";

    // No automatic decompression: the tests read the bytes as they were sent.
    private static readonly HttpClient Client = new(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.None })
    {
        BaseAddress = Site.BaseAddress,
    };

    private sealed record ListedFile(string Path, long Size, string Sha256);

    private static async Task<IReadOnlyList<ListedFile>> ListedFilesAsync()
    {
        using var document = JsonDocument.Parse(await Client.GetStringAsync("_health/files.json"));
        return [.. document.RootElement.GetProperty("files").EnumerateArray().Select(file => new ListedFile(
            file.GetProperty("path").GetString()!,
            file.GetProperty("size").GetInt64(),
            file.GetProperty("sha256").GetString()!))];
    }

    private static async Task<HttpResponseMessage> GetAsync(string path, string? acceptEncoding = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (acceptEncoding is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Encoding", acceptEncoding);
        }

        return await Client.SendAsync(request);
    }

    [Test]
    public async Task Get_Healthcheck_ShouldAnswerHealthy()
    {
        var body = await Client.GetStringAsync("_healthcheck");

        body.Trim().ShouldBe("Healthy");
    }

    [Test]
    public async Task Get_TheFrontPage_ShouldHaveTheTitleAndBeAskedForAgainEachTime()
    {
        using var response = await GetAsync(string.Empty);
        var body = await response.Content.ReadAsStringAsync();

        ((int)response.StatusCode).ShouldBe(200);
        body.ShouldContain($"<title>{Title}</title>");
        response.Headers.CacheControl.ShouldNotBeNull().NoCache.ShouldBeTrue();
    }

    [Test]
    public async Task Get_TheFrontPage_ShouldAllowItsImportMapByHashAndNoOtherInlineScript()
    {
        using var response = await GetAsync(string.Empty);
        var body = await response.Content.ReadAsStringAsync();
        var importMap = ImportMap().Match(body).Groups[1].Value;
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(importMap.Replace("\r\n", "\n", StringComparison.Ordinal))));

        var policy = response.Headers.GetValues("Content-Security-Policy").Single();

        importMap.Trim().ShouldNotBeEmpty();
        policy.ShouldContain($"script-src 'self' 'wasm-unsafe-eval' 'sha256-{hash}';");
        policy.ShouldNotContain("unsafe-inline");
        policy.ShouldStartWith("default-src 'self';");
        policy.ShouldContain("frame-ancestors 'none'");
    }

    [TestCase("")]
    [TestCase("css/app.css")]
    [TestCase("_version")]
    [TestCase("missing.js")]
    public async Task Get_AnyPath_ShouldCarryTheSecurityHeaders(string path)
    {
        using var response = await GetAsync(path);

        response.Headers.GetValues("Content-Security-Policy").Single().ShouldStartWith("default-src 'self';");
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        response.Headers.GetValues("Permissions-Policy").ShouldBe(["camera=(), microphone=(), geolocation=()"]);
    }

    [TestCase("garden")]
    [TestCase("garden/east")]
    public async Task Get_ANavigationPath_ShouldAnswerThePageOfTheGame(string path)
    {
        using var response = await GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        ((int)response.StatusCode).ShouldBe(200);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/html");
        body.ShouldContain($"<title>{Title}</title>");
        response.Headers.CacheControl.ShouldNotBeNull().NoCache.ShouldBeTrue();
    }

    [TestCase("missing.js")]
    [TestCase("_framework/missing.wasm")]
    [TestCase("assets/missing.webp")]
    [TestCase("_missing")]
    [TestCase("index.html.br")]
    [TestCase("staticwebapp.config.json")]
    public async Task Get_AFileThatIsNotThere_ShouldAnswer404AndNotBeKept(string path)
    {
        using var response = await GetAsync(path);

        ((int)response.StatusCode).ShouldBe(404);
        response.Headers.CacheControl.ShouldNotBeNull().NoCache.ShouldBeTrue();
        response.Headers.CacheControl.ShouldNotBeNull().MaxAge.ShouldBeNull();
    }

    [Test]
    public async Task Get_AFileOfTheRuntime_WithBrotliAccepted_ShouldAnswerBrotliToKeepForAYear()
    {
        var file = (await ListedFilesAsync()).First(listed => listed.Path.StartsWith("_framework/dotnet.native.", StringComparison.Ordinal) && listed.Path.EndsWith(".wasm", StringComparison.Ordinal));

        using var response = await GetAsync(file.Path, "gzip, deflate, br");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        ((int)response.StatusCode).ShouldBe(200);
        response.Content.Headers.ContentEncoding.ShouldBe(["br"]);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/wasm");
        response.Headers.Vary.ShouldContain("Accept-Encoding");
        bytes.LongLength.ShouldBeLessThan(file.Size);
        var cacheControl = response.Headers.CacheControl.ShouldNotBeNull();
        cacheControl.Public.ShouldBeTrue();
        cacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(365));
        cacheControl.Extensions.ShouldContain(extension => extension.Name == "immutable");
    }

    [Test]
    public async Task Get_TheFrontPage_WithBrotliAccepted_ShouldAnswerBrotli()
    {
        using var response = await GetAsync(string.Empty, "br");

        ((int)response.StatusCode).ShouldBe(200);
        response.Content.Headers.ContentEncoding.ShouldBe(["br"]);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/html");
    }

    [Test]
    public async Task Get_EveryListedFile_WithoutAcceptEncoding_ShouldAnswerTheFileAsTheBuildMadeIt()
    {
        var files = await ListedFilesAsync();
        var different = new List<string>();

        foreach (var file in files)
        {
            using var response = await GetAsync(file.Path);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if ((int)response.StatusCode != 200 || response.Content.Headers.ContentEncoding.Count != 0 || bytes.LongLength != file.Size || hash != file.Sha256)
            {
                different.Add($"{file.Path} (status {(int)response.StatusCode}, {bytes.LongLength} of {file.Size} bytes)");
            }
        }

        files.Count.ShouldBeGreaterThan(10);
        different.ShouldBeEmpty();
    }

    [Test]
    public async Task Get_AFileWithItsETag_ShouldAnswer304()
    {
        using var first = await GetAsync("css/app.css");
        using var request = new HttpRequestMessage(HttpMethod.Get, "css/app.css");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag.ShouldNotBeNull());

        using var second = await Client.SendAsync(request);

        ((int)second.StatusCode).ShouldBe(304);
    }

    [GeneratedRegex("<script type=\"importmap\">(.*?)</script>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ImportMap();
}
