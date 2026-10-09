using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace AdamEve.AcceptanceTests;

/// <summary>
/// The title page on the three device profiles of the design: a desktop with a keyboard, a phone with Chromium and
/// touch, a phone with WebKit and touch. All headless. Since slice S4 it is the title screen of the design (beat
/// B0), with the character select; it no longer says "Coming soon".
/// </summary>
[TestFixture]
public class TitlePageTests : PlaywrightTest
{
    private const string Title = "Adam and woman in the garden of Eden";

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheTitlePage_ShouldShowTheTitleWithoutABrowserError(string device, string engine)
    {
        var errors = new List<string>();
        await using var browser = await Playwright[engine].LaunchAsync();
        await using var context = await browser.NewContextAsync(Playwright.Devices[device]);
        var page = await context.NewPageAsync();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Add(message.Text);
            }
        };
        page.PageError += (_, error) => errors.Add(error);

        await page.GotoAsync(Site.BaseAddress);

        try
        {
            await Expect(page.GetByTestId("title")).ToHaveTextAsync(Title);
            await Expect(page.GetByTestId("status")).ToHaveCountAsync(0);
            await Expect(page.GetByTestId("character-select")).ToBeVisibleAsync();
            await Expect(page).ToHaveTitleAsync(Title);
        }
        catch (PlaywrightException exception) when (errors.Count > 0)
        {
            Assert.Fail($"{exception.Message}{Environment.NewLine}The browser reported: {string.Join(Environment.NewLine, errors)}");
        }

        errors.ShouldBeEmpty();
    }
}
