using AdamEve.Content;
using AdamEve.Content.Scripture;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace AdamEve.AcceptanceTests;

/// <summary>
/// The reader on the three device profiles of the design. What a test expects of Scripture is the canonical text as
/// parsed, never text typed here.
/// </summary>
[TestFixture]
public class ReaderTests : PlaywrightTest
{
    private static GameContent Content() => GameContent.LoadEmbedded().Content
        ?? throw new InvalidOperationException("The content of the game did not load.");

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheReaderFromTheTitlePage_ShouldShow80VersesAsParsedEachWithItsReference(string device, string engine)
    {
        var verses = Content().Scripture.Verses;
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;

        await page.GotoAsync(Site.BaseAddress);
        await page.GetByTestId("reader-link").ClickAsync();

        await Expect(page.GetByTestId("reader-title")).ToHaveTextAsync(GameText.ReaderTitle);
        await Expect(page.GetByTestId("scripture-card")).ToHaveCountAsync(80);
        var texts = await page.GetByTestId("scripture-text").AllTextContentsAsync();
        var references = await page.GetByTestId("scripture-reference").AllTextContentsAsync();
        texts.ShouldBe(verses.Select(verse => verse.Text));
        references.ShouldBe(verses.Select(verse => GameText.Citation(verse.Ref)));
        guarded.Errors.ShouldBeEmpty();
        guarded.RequestsOutsideTheOrigin.ShouldBeEmpty();
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheReaderAsADeepLink_ShouldShowChapter3Verse20WithItsCurlyApostrophe(string device, string engine)
    {
        var verse = Content().Scripture.Find(new VerseRef(3, 20));
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;

        await page.GotoAsync(Site.BaseAddress + "reader");

        var card = page.Locator("[data-testid='scripture-card'][data-ref='3:20']");
        await Expect(card).ToHaveCountAsync(1);
        var text = await card.GetByTestId("scripture-text").TextContentAsync();
        text.ShouldBe(verse.Text);
        text.ShouldNotBeNull().ShouldContain('’');
        text.ShouldNotContain('\'');
        await Expect(card.GetByTestId("scripture-reference")).ToHaveTextAsync("Genesis 3:20");
        await Expect(card).ToBeVisibleAsync();
        guarded.Errors.ShouldBeEmpty();
        guarded.RequestsOutsideTheOrigin.ShouldBeEmpty();
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Choose_AGlossaryWord_ShouldShowItsDefinitionAsNarrationOutsideTheCard(string device, string engine)
    {
        var content = Content();
        var verse = content.Scripture.Verses.First(candidate => content.Glossary.Segment(candidate.Text).Any(segment => segment.Entry is not null));
        var entry = content.Glossary.Segment(verse.Text).First(segment => segment.Entry is not null).Entry!;
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        await page.GotoAsync(Site.BaseAddress + "reader");
        var card = page.Locator($"[data-testid='scripture-card'][data-ref='{verse.Ref}']");
        var word = card.GetByTestId("glossary-word").First;
        var definition = page.GetByTestId("glossary-definition");
        await Expect(word).ToHaveTextAsync(entry.Term);
        await Expect(definition).ToBeEmptyAsync();

        if (Playwright.Devices[device].HasTouch == true)
        {
            await word.TapAsync();
        }
        else
        {
            await word.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
        }

        await Expect(page.GetByTestId("glossary-term")).ToHaveTextAsync(entry.Term);
        await Expect(page.GetByTestId("glossary-meaning")).ToHaveTextAsync(entry.Definition);
        await Expect(definition).ToBeInViewportAsync();
        (await page.Locator("[data-testid='scripture-card'] [data-testid='glossary-definition']").CountAsync()).ShouldBe(0);
        (await definition.Locator("[data-testid='scripture-reference']").CountAsync()).ShouldBe(0);
        (await card.GetByTestId("scripture-text").TextContentAsync()).ShouldBe(verse.Text);
        var scriptureFace = await card.EvaluateAsync<string>("element => getComputedStyle(element).fontFamily");
        var narrationFace = await definition.EvaluateAsync<string>("element => getComputedStyle(element).fontFamily");
        var scripturePaper = await card.EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor");
        var narrationPaper = await definition.EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor");
        scriptureFace.ShouldEndWith("serif");
        scriptureFace.ShouldNotEndWith("sans-serif");
        narrationFace.ShouldEndWith("sans-serif");
        narrationPaper.ShouldNotBe(scripturePaper);

        await page.GetByTestId("glossary-close").ClickAsync();

        await Expect(definition).ToBeEmptyAsync();
        guarded.Errors.ShouldBeEmpty();
        guarded.RequestsOutsideTheOrigin.ShouldBeEmpty();
    }
}
