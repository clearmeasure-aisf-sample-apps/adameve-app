namespace AdamEve.IntegrationTests;

[TestFixture]
public class CreationRouteTests
{
    private static readonly HttpClient Client = new() { BaseAddress = Site.BaseAddress };

    [Test]
    public async Task Get_TheDaysOfCreationAsADeepLink_ShouldAnswerThePageOfTheSite()
    {
        var index = await Client.GetStringAsync(string.Empty);

        using var response = await Client.GetAsync("creation");
        var body = await response.Content.ReadAsStringAsync();

        ((int)response.StatusCode).ShouldBe(200);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/html");
        body.ShouldBe(index);
    }

    [Test]
    public async Task Get_TheStyleSheet_ShouldTurnTheMotionOfThePictureOffForReducedMotionAndNameNoOtherOrigin()
    {
        var css = await Client.GetStringAsync("css/app.css");

        css.ShouldContain("@media (prefers-reduced-motion: reduce)");
        css.ShouldNotContain("url(");
        css.ShouldNotContain("@import");
        css.ShouldNotContain("@font-face");
    }
}
