namespace AdamEve.IntegrationTests;

[TestFixture]
public class ReaderRouteTests
{
    private static readonly HttpClient Client = new() { BaseAddress = Site.BaseAddress };

    [Test]
    public async Task Get_TheReaderAsADeepLink_ShouldAnswerThePageOfTheSite()
    {
        var index = await Client.GetStringAsync(string.Empty);

        using var response = await Client.GetAsync("reader");
        var body = await response.Content.ReadAsStringAsync();

        ((int)response.StatusCode).ShouldBe(200);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/html");
        body.ShouldBe(index);
    }
}
