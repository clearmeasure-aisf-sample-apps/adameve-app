using AdamEve.Content;
using AdamEve.Content.Scripture;
using AdamEve.Core.Saves;
using AdamEve.Core.Story;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace AdamEve.AcceptanceTests;

/// <summary>
/// Slice S4: the title with the character select, and the days of creation (beats B0 to B7), on the three device
/// profiles of the design: with the keyboard on the desktop, with touch on the phones. What a test expects of
/// Scripture is the canonical text as parsed and its references, never text typed here. What a test reads is the
/// page: the cards, the controls and the data- attributes of the root (chapter, beat, card, the M1 verdict).
/// </summary>
[TestFixture]
public class CreationTests : PlaywrightTest
{
    private static readonly string[] SixDays = ["B1", "B2", "B3", "B4", "B5", "B6"];

    private static GameContent Content() => GameContent.LoadEmbedded().Content
        ?? throw new InvalidOperationException("The content of the game did not load.");

    /// <summary>Genesis 1:1 to 2:3 as the parser reads the canonical file.</summary>
    private static List<Verse> Opening() => [.. Content().Scripture.Verses.TakeWhile(verse => verse.Ref != new VerseRef(2, 4))];

    private bool HasTouch(string device) => Playwright.Devices[device].HasTouch == true;

    /// <summary>Writes into the storage of the origin before the game loads, on a page of the site that starts no runtime.</summary>
    private static async Task StoreAsync(IPage page, string? save, string? settings = null)
    {
        await page.GotoAsync(Site.BaseAddress + "404.html");
        await page.EvaluateAsync(
            "entries => { for (const [key, value] of entries) { if (value !== null) { localStorage.setItem(key, value); } } }",
            new[] { new[] { SaveCodec.SaveKey, save }, new[] { SaveCodec.SettingsKey, settings } });
    }

    private static string SpawnOf(PlayerCharacter character) => Content().Garden.Map.Spawn(character == PlayerCharacter.Adam ? "adam" : "woman").ToString();

    /// <summary>Opens the title and chooses a character: a tap on a phone, the keyboard on the desktop.</summary>
    private async Task<ILocator> ChooseAsync(IPage page, string device, PlayerCharacter character)
    {
        await page.GotoAsync(Site.BaseAddress);
        var choice = page.GetByTestId($"choose-{character}");
        await Expect(choice).ToBeVisibleAsync(new() { Timeout = 30_000 });
        if (HasTouch(device))
        {
            await choice.TapAsync();
        }
        else
        {
            await choice.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
        }

        var creation = page.GetByTestId("creation");
        await Expect(creation).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        return creation;
    }

    /// <summary>What the player sees of the story, as one text: the beat, the card and whether the gesture is awaited.</summary>
    private static Task<string?> StateAsync(IPage page) => page.EvaluateAsync<string?>(
        "() => { const root = document.getElementById('creation'); return root ? `${root.dataset.beat}|${root.dataset.card ?? ''}|${root.dataset.awaitsReveal}` : null; }");

    /// <summary>
    /// The one thing the player can do: the gesture of the day when the story waits for it (a tap on the picture,
    /// or Space), the next page otherwise (a tap on the card, or Enter). Returns when the page shows the answer.
    /// </summary>
    private async Task<string?> ActAsync(IPage page, string device, string state)
    {
        var awaited = state.EndsWith("|true", StringComparison.Ordinal);
        if (!HasTouch(device))
        {
            await page.Keyboard.PressAsync(awaited ? "Space" : "Enter");
        }
        else if (awaited)
        {
            await page.GetByTestId("scene").TapAsync();
        }
        else
        {
            // The corner of the card, where no glossary word lies.
            await page.GetByTestId("scripture-card").TapAsync(new() { Position = new Position { X = 5, Y = 5 } });
        }

        await page.WaitForFunctionAsync(
            "before => { const root = document.getElementById('creation'); return !root || `${root.dataset.beat}|${root.dataset.card ?? ''}|${root.dataset.awaitsReveal}` !== before; }",
            state,
            new() { Timeout = 15_000 });
        return await StateAsync(page);
    }

    /// <summary>What may not happen in any test of the days of creation.</summary>
    private static async Task ExpectACleanRunAsync(GuardedPage guarded)
    {
        guarded.Errors.ShouldBeEmpty();
        guarded.RequestsOutsideTheOrigin.ShouldBeEmpty();
        (await guarded.Page.EvaluateAsync<string>("() => document.cookie")).ShouldBeEmpty();
        var keys = await guarded.Page.EvaluateAsync<string[]>("() => Object.keys(localStorage)");
        keys.ShouldBeSubsetOf([SaveCodec.SaveKey, SaveCodec.SettingsKey, SaveCodec.CorruptKey]);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheTitle_ShouldShowAdamAndTheWomanConcealedWithTheSettingsAndTheReader(string device, string engine)
    {
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;

        await page.GotoAsync(Site.BaseAddress);

        var title = page.GetByTestId("title-screen");
        await Expect(page.GetByTestId("title")).ToHaveTextAsync("Adam and woman in the garden of Eden");
        await Expect(title).ToHaveAttributeAsync("data-beat", "B0");
        await Expect(title).ToHaveAttributeAsync("data-concealment", "ok");
        await Expect(page.GetByTestId("figure-Adam")).ToHaveAttributeAsync("data-concealment", "ok");
        await Expect(page.GetByTestId("figure-Woman")).ToHaveAttributeAsync("data-concealment", "ok");
        await Expect(page.GetByTestId("choose-Adam")).ToHaveTextAsync(GameText.CharacterAdam);
        await Expect(page.GetByTestId("choose-Woman")).ToContainTextAsync(GameText.CharacterWoman);
        await Expect(page.GetByTestId("woman-note")).ToHaveTextAsync("named Eve in " + GameText.Citation(new VerseRef(3, 20)));
        await Expect(page.GetByTestId("reader-link")).ToHaveTextAsync(GameText.ReaderLink);
        await Expect(page.GetByTestId("continue")).ToHaveCountAsync(0);
        await Expect(page.GetByTestId("text-size-M")).ToBeCheckedAsync();
        await Expect(page.GetByTestId("sound")).ToBeCheckedAsync();
        (await page.Locator("#title svg.figure > *").CountAsync()).ShouldBeGreaterThan(20);
        (await page.Locator("#title img, #title canvas, #title [style]").CountAsync()).ShouldBe(0);
        var targets = await page.EvaluateAsync<double[][]>(
            "() => [...document.querySelectorAll('#title button, #title .game-choice, #title a')].map(element => element.getBoundingClientRect()).map(box => [box.width, box.height])");
        targets.Length.ShouldBe(8);
        targets.ShouldAllBe(box => box[0] >= 47.5 && box[1] >= 47.5);
        (await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth")).ShouldBeTrue();
        if (engine == "chromium")
        {
            await KeepScreenshotAsync(page, $"m1-{device}-title".Replace(' ', '-'));
        }

        await ExpectACleanRunAsync(guarded);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Choose_TextSizeXLAndNoSoundAtTheTitle_ShouldBeKeptAndSizeTheCardsOfTheDaysOfCreation(string device, string engine)
    {
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        await page.GotoAsync(Site.BaseAddress);

        await page.GetByTestId("text-size-XL").CheckAsync();
        await page.GetByTestId("sound").UncheckAsync();

        await Expect(page.GetByTestId("title-screen")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\btext-xl\b"));
        (await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", SaveCodec.SettingsKey))
            .ShouldBe(SaveCodec.WriteSettings(new GameSettings { TextSize = TextSize.XL, Sound = false }));
        (await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= window.innerWidth")).ShouldBeTrue();
        var creation = await ChooseAsync(page, device, PlayerCharacter.Adam);
        await Expect(creation).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\btext-xl\b"));
        (await page.GetByTestId("scripture-text").EvaluateAsync<string>("element => getComputedStyle(element).fontSize")).ShouldBe("28px");
        await ExpectACleanRunAsync(guarded);
    }

    [TestCase("Desktop Chrome", "chromium", PlayerCharacter.Adam)]
    [TestCase("Desktop Chrome", "chromium", PlayerCharacter.Woman)]
    [TestCase("Pixel 7", "chromium", PlayerCharacter.Adam)]
    [TestCase("Pixel 7", "chromium", PlayerCharacter.Woman)]
    [TestCase("iPhone 13", "webkit", PlayerCharacter.Adam)]
    [TestCase("iPhone 13", "webkit", PlayerCharacter.Woman)]
    public async Task Play_ANewGameThroughTheSevenDays_ShouldShowTheCardsFrom1_1To2_3InOrderWithSixRevealsAndNoneOnTheSeventhDay(string device, string engine, PlayerCharacter character)
    {
        var opening = Opening();
        var spoken = CreationStory.Days.SelectMany(day => day.Voice).Select(verse => verse.ToString()).ToList();
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        var creation = await ChooseAsync(page, device, character);
        var cards = new List<string>();
        var texts = new List<string?>();
        var voiced = new List<string>();
        var reveals = new List<string>();
        var beats = new List<string>();
        var figures = new List<string>();
        var state = await StateAsync(page);

        while (state is not null)
        {
            var parts = state.Split('|');
            var awaited = parts[2] == "true";
            if (beats.Count == 0 || beats[^1] != parts[0])
            {
                beats.Add(parts[0]);
            }

            await Expect(creation).ToHaveAttributeAsync("data-chapter", "Creation");
            await Expect(creation).ToHaveAttributeAsync("data-concealment", "ok");
            await Expect(page.GetByTestId("primary")).ToHaveTextAsync(awaited ? GameText.Reveal : GameText.TurnThePage);
            await Expect(page.GetByTestId("skip")).ToHaveCountAsync(0);
            await Expect(page.GetByTestId("scripture-card")).ToHaveCountAsync(parts[1].Length == 0 ? 0 : 1);
            if (parts[1].Length > 0 && (cards.Count == 0 || cards[^1] != parts[1]))
            {
                cards.Add(parts[1]);
                texts.Add(await page.GetByTestId("scripture-text").TextContentAsync());
                await Expect(page.GetByTestId("scripture-reference")).ToHaveTextAsync($"{GameText.Book} {parts[1]}");
                if (await page.GetByTestId("scripture-speaker").CountAsync() > 0)
                {
                    await Expect(page.GetByTestId("scripture-speaker")).ToHaveTextAsync(GameText.SpeakerGod);
                    await Expect(page.Locator("[data-layer='Presence']")).ToHaveCountAsync(1);
                    voiced.Add(parts[1]);
                }
                else
                {
                    await Expect(page.Locator("[data-layer='Presence']")).ToHaveCountAsync(0);
                }

                if (await page.Locator("[data-layer='Figures']").CountAsync() > 0)
                {
                    figures.Add(parts[1]);
                    await Expect(page.Locator("[data-figure][data-concealment='ok']")).ToHaveCountAsync(2);
                    if (engine == "chromium" && parts[1] == "1:27")
                    {
                        await KeepScreenshotAsync(page, $"m1-{device}-{character}-day-6".Replace(' ', '-'));
                    }
                }
            }

            // Nothing the page says outside the Scripture card says that anyone creates or makes.
            var said = await page.EvaluateAsync<string>(
                "() => { const copy = document.getElementById('creation').cloneNode(true); copy.querySelectorAll('.scripture-card').forEach(card => card.remove()); return copy.textContent; }");
            said.ShouldNotContain("creat", Case.Insensitive);
            said.ShouldNotContain("make", Case.Insensitive);
            said.ShouldNotContain("made", Case.Insensitive);

            if (awaited)
            {
                reveals.Add(parts[0]);
            }

            state = await ActAsync(page, device, state);
        }

        cards.ShouldBe(opening.Select(verse => verse.Ref.ToString()));
        texts.ShouldBe(opening.Select(verse => verse.Text));
        cards.Count.ShouldBe(34);
        voiced.ShouldBe(spoken);
        reveals.ShouldBe(SixDays);
        beats.ShouldBe([.. SixDays, "B7"]);
        figures.ShouldBe(["1:26", "1:27", "1:28"]);
        var game = page.GetByTestId("game");
        await Expect(game).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
        await Expect(game).ToHaveAttributeAsync("data-player-tile", SpawnOf(character));
        var saved = SaveCodec.Read(await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", SaveCodec.SaveKey), Content().Garden.Map).Save.ShouldNotBeNull();
        saved.Character.ShouldBe(character);
        saved.Chapter.ShouldBe(StoryChapter.Formation);
        saved.CreationWatched.ShouldBeTrue();
        await ExpectACleanRunAsync(guarded);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Reveal_TheLightOfTheFirstDay_ShouldShowNoLightBeforeTheGestureAndThenTheCardOnWhichGodSpeaks(string device, string engine)
    {
        var verse = Content().Scripture.Find(new VerseRef(1, 3));
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        var creation = await ChooseAsync(page, device, PlayerCharacter.Adam);
        var state = await ActAsync(page, device, (await StateAsync(page))!);
        await Expect(creation).ToHaveAttributeAsync("data-awaits-reveal", "true");
        await Expect(creation).ToHaveAttributeAsync("data-card", "1:2");
        await Expect(page.Locator("[data-layer='Darkness']")).ToHaveCountAsync(1);
        await Expect(page.Locator("[data-layer='Light']")).ToHaveCountAsync(0);
        await Expect(page.GetByTestId("primary")).ToHaveAttributeAsync("data-action", "reveal");
        if (HasTouch(device))
        {
            // A tap on the card does not stand for the gesture: the page stays.
            await page.GetByTestId("scripture-card").TapAsync(new() { Position = new Position { X = 5, Y = 5 } });
            await page.WaitForTimeoutAsync(300);
            await Expect(creation).ToHaveAttributeAsync("data-card", "1:2");
        }

        await ActAsync(page, device, state!);

        await Expect(creation).ToHaveAttributeAsync("data-card", "1:3");
        await Expect(page.Locator("[data-layer='Light']")).ToHaveCountAsync(1);
        await Expect(page.Locator("[data-layer='Presence']")).ToHaveCountAsync(1);
        await Expect(page.GetByTestId("scripture-speaker")).ToHaveTextAsync(GameText.SpeakerGod);
        (await page.GetByTestId("scripture-text").TextContentAsync()).ShouldBe(verse.Text);
        await Expect(page.GetByTestId("scripture-reference")).ToHaveTextAsync(GameText.Citation(verse.Ref));
        (await page.Locator("#creation img, #creation canvas, #creation [style], #creation text").CountAsync()).ShouldBe(0);
        var cardFace = await page.GetByTestId("scripture-card").EvaluateAsync<string>("element => getComputedStyle(element).fontFamily");
        var controlFace = await page.GetByTestId("primary").EvaluateAsync<string>("element => getComputedStyle(element).fontFamily");
        cardFace.ShouldEndWith("serif");
        cardFace.ShouldNotEndWith("sans-serif");
        controlFace.ShouldEndWith("sans-serif");
        await ExpectACleanRunAsync(guarded);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Choose_ACharacterAfterAFirstCompletion_ShouldOfferToSkipTheDaysOfCreationAndGoToTheGarden(string device, string engine)
    {
        var adam = Content().Garden.Map.Spawn("adam");
        var watched = SaveCodec.Write(new SaveGame { Character = PlayerCharacter.Adam, TileX = adam.X, TileY = adam.Y, CreationWatched = true });
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        await StoreAsync(page, watched);
        var creation = await ChooseAsync(page, device, PlayerCharacter.Woman);
        await Expect(creation).ToHaveAttributeAsync("data-beat", "B1");
        await Expect(creation).ToHaveAttributeAsync("data-card", "1:1");
        await Expect(page.GetByTestId("skip")).ToHaveTextAsync(GameText.Skip);

        if (HasTouch(device))
        {
            await page.GetByTestId("skip").TapAsync();
        }
        else
        {
            await page.Keyboard.PressAsync("1");
        }

        var game = page.GetByTestId("game");
        await Expect(game).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await Expect(game).ToHaveAttributeAsync("data-player-tile", SpawnOf(PlayerCharacter.Woman));
        await Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
        var saved = SaveCodec.Read(await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", SaveCodec.SaveKey), Content().Garden.Map).Save.ShouldNotBeNull();
        saved.Character.ShouldBe(PlayerCharacter.Woman);
        saved.Chapter.ShouldBe(StoryChapter.Formation);
        saved.CreationWatched.ShouldBeTrue();
        await ExpectACleanRunAsync(guarded);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Reload_InTheSecondDay_ShouldGoOnFromTheBeginningOfThatDayAndFromTheTitleWithContinue(string device, string engine)
    {
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        var creation = await ChooseAsync(page, device, PlayerCharacter.Woman);
        var state = await StateAsync(page);
        while (state is not null && !state.StartsWith("B2|1:6|", StringComparison.Ordinal))
        {
            state = await ActAsync(page, device, state);
        }

        await page.ReloadAsync();

        creation = page.GetByTestId("creation");
        await Expect(creation).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await Expect(creation).ToHaveAttributeAsync("data-beat", "B2");
        await Expect(creation).ToHaveAttributeAsync("data-awaits-reveal", "true");
        await Expect(page.GetByTestId("scripture-card")).ToHaveCountAsync(0);
        await Expect(page.Locator("[data-layer='Firmament']")).ToHaveCountAsync(0);
        await page.GotoAsync(Site.BaseAddress);
        await page.GetByTestId("continue").ClickAsync();
        creation = page.GetByTestId("creation");
        await Expect(creation).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await Expect(creation).ToHaveAttributeAsync("data-beat", "B2");
        await ActAsync(page, device, (await StateAsync(page))!);
        await Expect(creation).ToHaveAttributeAsync("data-card", "1:6");
        await Expect(page.Locator("[data-layer='Firmament']")).ToHaveCountAsync(1);
        var saved = SaveCodec.Read(await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", SaveCodec.SaveKey), Content().Garden.Map).Save.ShouldNotBeNull();
        saved.ShouldBe(new SaveGame { Character = PlayerCharacter.Woman, TileX = saved.TileX, TileY = saved.TileY, Chapter = StoryChapter.Creation, Beat = StoryBeat.B2 });
        await ExpectACleanRunAsync(guarded);
    }

    [TestCase("Desktop Chrome", "chromium", false)]
    [TestCase("Desktop Chrome", "chromium", true)]
    [TestCase("Pixel 7", "chromium", false)]
    [TestCase("Pixel 7", "chromium", true)]
    [TestCase("iPhone 13", "webkit", false)]
    [TestCase("iPhone 13", "webkit", true)]
    public async Task Reveal_TheLightWithAndWithoutReducedMotion_ShouldMoveThePictureOnlyWithoutIt(string device, string engine, bool reducedMotion)
    {
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine, reducedMotion);
        var page = guarded.Page;
        await ChooseAsync(page, device, PlayerCharacter.Adam);
        var state = await StateAsync(page);
        while (state is not null && !state.StartsWith("B1|1:3|", StringComparison.Ordinal))
        {
            state = await ActAsync(page, device, state);
        }

        var light = await page.Locator("[data-layer='Light']").EvaluateAsync<string>("element => getComputedStyle(element).animationName");
        var presence = await page.Locator("[data-layer='Presence']").EvaluateAsync<string>("element => getComputedStyle(element).animationName");
        var darkness = await page.Locator("[data-layer='Darkness']").EvaluateAsync<string>("element => getComputedStyle(element).animationName");
        var prefers = await page.EvaluateAsync<bool>("() => matchMedia('(prefers-reduced-motion: reduce)').matches");

        prefers.ShouldBe(reducedMotion);
        if (reducedMotion)
        {
            light.ShouldBe("none");
            presence.ShouldBe("none");
            darkness.ShouldBe("none");
        }
        else
        {
            light.ShouldBe("scene-spread");
            presence.ShouldContain("presence-drift");
            darkness.ShouldBe("scene-appear");
        }

        // With or without motion the same card and the same picture are there.
        await Expect(page.GetByTestId("scripture-card")).ToHaveAttributeAsync("data-ref", "1:3");
        await Expect(page.Locator("[data-layer='Light'] ellipse")).ToHaveCountAsync(1);
        await ExpectACleanRunAsync(guarded);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheSixthDayAt360By640WithTextSizeXL_ShouldFitWithEveryControl48PixelsOrMoreAndBeStillOnTheSeventh(string device, string engine)
    {
        var woman = Content().Garden.Map.Spawn("woman");
        var sixth = SaveCodec.Write(new SaveGame { Character = PlayerCharacter.Woman, TileX = woman.X, TileY = woman.Y, Chapter = StoryChapter.Creation, Beat = StoryBeat.B6 });
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        await page.SetViewportSizeAsync(360, 640);
        await StoreAsync(page, sixth, SaveCodec.WriteSettings(new GameSettings { TextSize = TextSize.XL }));
        await page.GotoAsync(Site.BaseAddress + "creation");
        var creation = page.GetByTestId("creation");
        await Expect(creation).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        var state = await StateAsync(page);

        while (state is not null && !state.StartsWith("B7|2:1|", StringComparison.Ordinal))
        {
            var beyond = await page.EvaluateAsync<string[]>(
                """
                () => {
                    const beyond = [];
                    const root = document.documentElement;
                    if (root.scrollWidth > window.innerWidth || root.scrollHeight > window.innerHeight) { beyond.push('the page scrolls'); }
                    for (const element of document.querySelectorAll('#creation, .creation-scene, .creation-panel, .creation-card, .creation-controls, #creation button.game-button')) {
                        const box = element.getBoundingClientRect();
                        if (box.left < -0.5 || box.top < -0.5 || box.right > window.innerWidth + 0.5 || box.bottom > window.innerHeight + 0.5) { beyond.push(element.className); }
                    }
                    for (const button of document.querySelectorAll('#creation button.game-button')) {
                        const box = button.getBoundingClientRect();
                        if (box.width < 47.5 || box.height < 47.5) { beyond.push(`small: ${button.textContent}`); }
                    }
                    const scene = document.querySelector('.creation-scene').getBoundingClientRect();
                    if (scene.height < 120) { beyond.push(`the picture is ${scene.height} high`); }
                    return beyond;
                }
                """);
            beyond.ShouldBeEmpty(state);
            state = await ActAsync(page, device, state);
        }

        await Expect(creation).ToHaveAttributeAsync("data-beat", "B7");
        await Expect(creation).ToHaveAttributeAsync("data-awaits-reveal", "false");
        await Expect(page.GetByTestId("primary")).ToHaveTextAsync(GameText.TurnThePage);
        var moving = await page.Locator(".scene-layer").EvaluateAllAsync<string[]>("layers => layers.map(layer => getComputedStyle(layer).animationName).filter(name => name !== 'none')");
        moving.ShouldBeEmpty();
        await ExpectACleanRunAsync(guarded);
    }

    /// <summary>A screenshot of a frame that shows a character, kept with the test results for the review of rule M1. Never compared.</summary>
    private static async Task KeepScreenshotAsync(IPage page, string name)
    {
        var folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "m1-screenshots");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".png");
        await page.ScreenshotAsync(new() { Path = path, Caret = ScreenshotCaret.Initial, Animations = ScreenshotAnimations.Allow });
        TestContext.AddTestAttachment(path);
    }
}
