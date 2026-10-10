using AdamEve.Core.Saves;
using AdamEve.Core.Story;
using AdamEve.Core.World;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace AdamEve.AcceptanceTests;

/// <summary>
/// The gallery: pictures of what the game draws, kept with the test results for a person to look at
/// (<c>gallery/&lt;device&gt;-&lt;subject&gt;.png</c>). Nothing here is compared with a kept picture: the art is
/// made by code and is judged by eye. What the tests require is only that each subject can be reached and drawn
/// without an error, with the modesty verdict "ok" (rule M1).
/// </summary>
[TestFixture]
public class GalleryTests : PlaywrightTest
{
    private const string Gallery = "gallery";

    // Where the player stands for each subject of the garden: a walkable tile from which the subject is in view.
    private static readonly (string Subject, int X, int Y)[] Places =
    [
        ("glade", 26, 22),
        ("spring", 29, 8),
        ("river-crossing", 30, 12),
        ("on-the-crossing", 31, 10),
        ("tree-of-life", 28, 27),
        ("tree-of-knowledge", 35, 27),
        ("horizon", 10, 21),
    ];

    private static async Task KeepAsync(IPage page, string device, string subject, Clip? clip = null)
    {
        var path = GardenView.KeptPath(Gallery, $"{device}-{subject}.png");
        await page.ScreenshotAsync(new() { Path = path, Clip = clip, Caret = ScreenshotCaret.Initial, Animations = ScreenshotAnimations.Allow });
        TestContext.AddTestAttachment(path, subject);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    public async Task Stand_AtEachPlaceOfTheGarden_ShouldDrawItWithTheVerdictOkAndKeepAPictureOfEach(string device, string engine)
    {
        var map = GardenView.Garden().Map;
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        ILocator? game = null;

        foreach (var (subject, x, y) in Places)
        {
            map.IsWalkable(new TilePos(x, y)).ShouldBeTrue(subject);
            game = await GardenView.OpenAsync(page, GardenView.SaveAt(x, y, PlayerCharacter.Woman));
            await GardenView.ExpectThreeDrawsAsync(game);
            // The clock of the garden runs a little, so the wind, the water and the air are somewhere in their motion.
            await page.WaitForTimeoutAsync(400);

            await KeepAsync(page, device, subject);

            await Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
        }

        await GardenView.SetAsideRefusedStyleSheetsAsync(guarded, Places.Length);
        await GardenView.ExpectACleanRunAsync(guarded, game!);
    }

    [TestCase("Desktop Chrome", "chromium", PlayerCharacter.Adam)]
    [TestCase("Desktop Chrome", "chromium", PlayerCharacter.Woman)]
    [TestCase("Pixel 7", "chromium", PlayerCharacter.Adam)]
    [TestCase("Pixel 7", "chromium", PlayerCharacter.Woman)]
    public async Task Walk_EachWayInTheOpen_ShouldKeepAPictureOfTheFigureStandingAndInMidStep(string device, string engine, PlayerCharacter character)
    {
        var open = GardenView.OpenGround(3);
        var name = character.ToString().ToLowerInvariant();
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(open.X, open.Y, character));
        await GardenView.ExpectThreeDrawsAsync(game);
        var canvas = await page.GetByTestId("game-canvas").BoundingBoxAsync() ?? throw new InvalidOperationException("The canvas has no box.");
        var shots = 1;

        async Task<Clip> AroundTheFigureAsync()
        {
            // The figure where the renderer itself says its feet are, in this very frame.
            var probe = await GardenView.ProbeAsync(page);
            var feet = probe.Anchors[character == PlayerCharacter.Adam ? 0 : 1];
            var at = (await GardenView.ProbeAsync(page, (feet.X, 0, feet.Y))).Screen[0];
            var scale = (await GardenView.CameraAsync(page, open)).Scale;
            return new Clip { X = (float)Math.Max(0, canvas.X + at.X - (34 * scale)), Y = (float)Math.Max(0, canvas.Y + at.Y - (62 * scale)), Width = (float)(68 * scale), Height = (float)(72 * scale) };
        }

        await KeepAsync(page, device, $"{name}-standing", await AroundTheFigureAsync());
        foreach (var (key, facing) in new[] { ("ArrowRight", "east"), ("ArrowDown", "south"), ("ArrowLeft", "west"), ("ArrowUp", "north") })
        {
            await page.Keyboard.DownAsync(key);
            await Expect(game).ToHaveAttributeAsync("data-moving", "true");
            await page.WaitForTimeoutAsync(260);
            await KeepAsync(page, device, $"{name}-walking-{facing}", await AroundTheFigureAsync());
            await page.Keyboard.UpAsync(key);
            await Expect(game).ToHaveAttributeAsync("data-moving", "false", new() { Timeout = 15_000 });
            shots++;
        }

        await GardenView.SetAsideRefusedStyleSheetsAsync(guarded, shots);
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    public async Task Open_TheTitle_ShouldKeepAPictureOfItWithTheCharacterSelect(string device, string engine)
    {
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        await page.GotoAsync(Site.BaseAddress);
        await Expect(page.GetByTestId("character-select")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(page.GetByTestId("title-screen")).ToHaveAttributeAsync("data-concealment", "ok");

        await KeepAsync(page, device, "title");

        await GardenView.SetAsideRefusedStyleSheetsAsync(guarded, 1);
        guarded.Errors.ShouldBeEmpty();
        guarded.RequestsOutsideTheOrigin.ShouldBeEmpty();
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    public async Task Play_TheDaysOfCreation_ShouldKeepAPictureOfEachDayAsItsLastCardShowsIt(string device, string engine)
    {
        // One visit through the seven days, with "reduce motion" set: each picture is whole as soon as it is shown.
        var woman = GardenView.Garden().Map.Spawn("woman");
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine, reducedMotion: true);
        var page = guarded.Page;
        var kept = new List<string>();
        await page.GotoAsync(Site.BaseAddress + "404.html");
        await page.EvaluateAsync("entry => localStorage.setItem(entry[0], entry[1])", new[] { SaveCodec.SaveKey, SaveCodec.Write(new SaveGame { Character = PlayerCharacter.Woman, TileX = woman.X, TileY = woman.Y, Chapter = StoryChapter.Creation, Beat = StoryBeat.B1 }) });
        await page.GotoAsync(Site.BaseAddress + "creation");
        var creation = page.GetByTestId("creation");
        await Expect(creation).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        var lastCards = CreationStory.Days.ToDictionary(day => day.Cards[^1].ToString(), day => CreationStory.Days.ToList().IndexOf(day) + 1);

        for (var press = 0; press < 80 && kept.Count < lastCards.Count + 2; press++)
        {
            var card = await creation.GetAttributeAsync("data-card") ?? string.Empty;
            var subject = lastCards.TryGetValue(card, out var number) ? $"creation-day-{number}"
                : card == "1:3" ? "creation-day-1-light"
                : card == "1:27" ? "creation-day-6-figures"
                : null;
            if (subject is not null && !kept.Contains(subject))
            {
                await Expect(creation).ToHaveAttributeAsync("data-concealment", "ok");
                if (card == "1:27")
                {
                    await Expect(page.Locator("[data-figure][data-concealment='ok']")).ToHaveCountAsync(2);
                }

                await KeepAsync(page, device, subject);
                kept.Add(subject);
            }

            if (kept.Count < lastCards.Count + 2)
            {
                await page.GetByTestId("primary").ClickAsync();
                await page.WaitForFunctionAsync("before => { const root = document.getElementById('creation'); return !root || `${root.dataset.card ?? ''}|${root.dataset.awaitsReveal}` !== before; }", $"{card}|{await creation.GetAttributeAsync("data-awaits-reveal")}", new() { Timeout = 15_000 });
            }
        }

        kept.Count.ShouldBe(9, "the seven days, the light of the first and the two far figures of the sixth");
        await GardenView.SetAsideRefusedStyleSheetsAsync(guarded, 9);
        guarded.Errors.ShouldBeEmpty();
        guarded.RequestsOutsideTheOrigin.ShouldBeEmpty();
    }
}
