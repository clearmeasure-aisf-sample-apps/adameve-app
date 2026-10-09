namespace AdamEve.IntegrationTests;

[TestFixture]
public class HealthPathTests
{
    private static readonly HttpClient Client = new() { BaseAddress = Site.BaseAddress };

    [TestCase("_healthcheck")]
    [TestCase("alive")]
    [TestCase("_version")]
    [TestCase("_build")]
    public async Task Get_AHealthPath_ShouldAnswer200ToEveryOriginAndNeverFromACache(string path)
    {
        using var response = await Client.GetAsync(path);

        ((int)response.StatusCode).ShouldBe(200);
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["*"]);
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
    }

    [Test]
    public async Task Get_Version_ShouldAnswerTheVersionOfTheBuild()
    {
        var expected = Environment.GetEnvironmentVariable("ADAMEVE_VERSION");

        var body = await Client.GetStringAsync("_version");

        using var document = System.Text.Json.JsonDocument.Parse(body);
        var version = document.RootElement.GetProperty("version").GetString();
        version.ShouldNotBeNullOrWhiteSpace();
        if (!string.IsNullOrWhiteSpace(expected))
        {
            version.ShouldBe(expected);
        }
    }
}
