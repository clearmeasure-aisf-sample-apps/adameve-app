using System.Globalization;
using AdamEve.Content;
using AdamEve.Content.Garden;
using AdamEve.Core.Saves;
using AdamEve.Core.World;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace AdamEve.AcceptanceTests;

/// <summary>
/// Slice S2, "Walk the garden", on the three device profiles of the design. A test starts from a saved game written
/// into <c>localStorage</c> before the garden loads: the save format is the scenario format, so the game has no
/// test-only way in. What a test reads is the page: the data- attributes of the game root (player tile, facing, the
/// M1 verdict), the settings and the status line; never pixels.
/// </summary>
[TestFixture]
public class GardenTests : PlaywrightTest
{
    private static readonly (string Name, int X, int Y)[] EightFacings =
    [
        ("N", 0, -1), ("S", 0, 1), ("E", 1, 0), ("W", -1, 0), ("NE", 1, -1), ("SW", -1, 1), ("SE", 1, 1), ("NW", -1, -1),
    ];

    private static GardenContent Garden() => GameContent.LoadEmbedded().Content?.Garden
        ?? throw new InvalidOperationException("The content of the game did not load.");

    private static string Tile(int x, int y) => new TilePos(x, y).ToString();

    private static string SaveAt(int x, int y, PlayerCharacter character = PlayerCharacter.Adam, Facing facing = Facing.S) =>
        SaveCodec.Write(new SaveGame { Character = character, TileX = x, TileY = y, Facing = facing });

    /// <summary>A tile of the glade with a free tile on each of its eight sides, far from the edge of the map.</summary>
    private static TilePos OpenGround()
    {
        var map = Garden().Map;
        var glade = map.Regions.Single(region => region.Id == "central-glade");
        for (var y = glade.Y + 2; y < glade.Y + glade.Height - 2; y++)
        {
            for (var x = glade.X + 2; x < glade.X + glade.Width - 2; x++)
            {
                var around = Enumerable.Range(-1, 3).SelectMany(dy => Enumerable.Range(-1, 3).Select(dx => new TilePos(x + dx, y + dy)));
                if (around.All(tile => map.IsWalkable(tile) && tile != map.Spawn("adam") && tile != map.Spawn("woman")))
                {
                    return new TilePos(x, y);
                }
            }
        }

        throw new InvalidOperationException("The glade has no open ground.");
    }

    private bool HasTouch(string device) => Playwright.Devices[device].HasTouch == true;

    /// <summary>Opens the garden, from a saved game when one is given, and waits for its first frames.</summary>
    private async Task<ILocator> OpenGardenAsync(IPage page, string? save = null, string? settings = null)
    {
        if (save is not null || settings is not null)
        {
            // A page of the site that starts no runtime: the storage of the origin is written before the game loads.
            await page.GotoAsync(Site.BaseAddress + "404.html");
            await page.EvaluateAsync(
                "entries => { for (const [key, value] of entries) { if (value !== null) { localStorage.setItem(key, value); } } }",
                new[] { new[] { SaveCodec.SaveKey, save }, new[] { SaveCodec.SettingsKey, settings } });
        }

        await page.GotoAsync(Site.BaseAddress + "garden");
        var game = page.GetByTestId("game");
        await Expect(game).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
        return game;
    }

    private async Task ExpectStandingOnAsync(ILocator game, string tile)
    {
        await Expect(game).ToHaveAttributeAsync("data-player-tile", tile, new() { Timeout = 15_000 });
        await Expect(game).ToHaveAttributeAsync("data-moving", "false");
    }

    /// <summary>One press of a direction: a tap on the D-pad where the device has touch, a key otherwise.</summary>
    private async Task PressAsync(IPage page, string device, string direction)
    {
        if (HasTouch(device))
        {
            await page.GetByTestId($"dpad-{direction}").TapAsync();
        }
        else
        {
            await page.Keyboard.PressAsync("Arrow" + char.ToUpperInvariant(direction[0]) + direction[1..]);
        }
    }

    /// <summary>A tap (or a click) on the tile that lies a number of tiles from the player, who is in the middle of the play area.</summary>
    private async Task TapTileAsync(IPage page, string device, int tilesRight, int tilesDown)
    {
        var canvas = page.GetByTestId("game-canvas");
        var box = await canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("The canvas has no box.");
        var tile = Math.Max(Math.Min(box.Width, box.Height) / Camera.ShortSideTiles, Math.Max(box.Width, box.Height) / Camera.LongSideTiles);
        var position = new Position { X = (float)((box.Width / 2) + (tilesRight * tile)), Y = (float)((box.Height / 2) + (tilesDown * tile)) };
        position.X.ShouldBeInRange(1, (float)box.Width - 1);
        position.Y.ShouldBeInRange(1, (float)box.Height - 1);
        if (HasTouch(device))
        {
            await canvas.TapAsync(new() { Position = position });
        }
        else
        {
            await canvas.ClickAsync(new() { Position = position });
        }
    }

    /// <summary>What may not happen in any test of the garden.</summary>
    private async Task ExpectACleanRunAsync(GuardedPage guarded, ILocator game)
    {
        await Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
        await Expect(game).ToHaveAttributeAsync("data-concealment-failures", "0");
        guarded.Errors.ShouldBeEmpty();
        guarded.RequestsOutsideTheOrigin.ShouldBeEmpty();
        (await guarded.Page.EvaluateAsync<string>("() => document.cookie")).ShouldBeEmpty();
        var keys = await guarded.Page.EvaluateAsync<string[]>("() => Object.keys(localStorage)");
        keys.ShouldBeSubsetOf([SaveCodec.SaveKey, SaveCodec.SettingsKey, SaveCodec.CorruptKey]);
    }

    /// <summary>Whether anything of the game reaches beyond the viewport, or the page scrolls.</summary>
    private static Task<string[]> OverflowingAsync(IPage page) => page.EvaluateAsync<string[]>(
        """
        () => {
            const beyond = [];
            const root = document.documentElement;
            if (root.scrollWidth > window.innerWidth || root.scrollHeight > window.innerHeight) {
                beyond.push(`the page: ${root.scrollWidth}x${root.scrollHeight}`);
            }
            for (const element of document.querySelectorAll('#game, #game *')) {
                if (element.closest('.visually-hidden, .game-panel-empty')) { continue; }
                const box = element.getBoundingClientRect();
                if (box.width === 0 || box.height === 0) { continue; }
                if (box.left < -0.5 || box.top < -0.5 || box.right > window.innerWidth + 0.5 || box.bottom > window.innerHeight + 0.5) {
                    beyond.push(`${element.tagName} ${element.className}: ${Math.round(box.left)},${Math.round(box.top)} to ${Math.round(box.right)},${Math.round(box.bottom)}`);
                }
            }
            return beyond;
        }
        """);

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheGardenFromTheTitlePage_ShouldShowThePlayerInTheCentralGladeWithTheVerdictOk(string device, string engine)
    {
        var spawn = Garden().Map.Spawn("adam");
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        await page.GotoAsync(Site.BaseAddress);
        await Expect(page.GetByTestId("status")).ToHaveTextAsync("Coming soon");

        await page.GetByTestId("garden-link").ClickAsync();

        var game = page.GetByTestId("game");
        await Expect(game).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await Expect(game).ToHaveAttributeAsync("data-player-tile", spawn.ToString());
        await Expect(game).ToHaveAttributeAsync("data-facing", "S");
        await Expect(page.GetByTestId("status-line")).ToHaveTextAsync(GameText.CentralGlade);
        await Expect(page).ToHaveTitleAsync("Adam and woman in the garden of Eden");
        var canvas = await page.GetByTestId("game-canvas").EvaluateAsync<int[]>("canvas => [canvas.width, canvas.height, canvas.clientWidth, canvas.clientHeight]");
        canvas.ShouldAllBe(side => side >= 300);
        if (HasTouch(device))
        {
            await Expect(page.GetByTestId("dpad")).ToBeVisibleAsync();
        }
        else
        {
            await Expect(page.GetByTestId("dpad")).ToBeHiddenAsync();
        }

        (await OverflowingAsync(page)).ShouldBeEmpty();
        await ExpectACleanRunAsync(guarded, game);
    }

    [Test]
    public async Task Walk_WithTheArrowKeysAndWithWasd_ShouldMoveOneTileForEachPress()
    {
        var open = OpenGround();
        await using var guarded = await GuardedPage.OpenAsync(Playwright, "Desktop Chrome", "chromium");
        var page = guarded.Page;
        var game = await OpenGardenAsync(page, SaveAt(open.X - 1, open.Y));
        (string Key, int X, int Y, string Facing)[] presses =
        [
            ("ArrowRight", 1, 0, "E"), ("ArrowRight", 1, 0, "E"), ("ArrowLeft", -1, 0, "W"), ("ArrowUp", 0, -1, "N"), ("ArrowDown", 0, 1, "S"),
            ("d", 1, 0, "E"), ("a", -1, 0, "W"), ("d", 1, 0, "E"), ("w", 0, -1, "N"), ("s", 0, 1, "S"),
        ];
        var x = open.X - 1;
        var y = open.Y;

        foreach (var press in presses)
        {
            await page.Keyboard.PressAsync(press.Key);
            x += press.X;
            y += press.Y;

            await ExpectStandingOnAsync(game, Tile(x, y));
            await Expect(game).ToHaveAttributeAsync("data-facing", press.Facing);
        }

        await ExpectACleanRunAsync(guarded, game);
    }

    [Test]
    public async Task Walk_ThreePressesOfArrowRightThenThreeOfD_ShouldMoveThePlayerThreeTilesEachTime()
    {
        // Row 17 of the map is open from the Pison meadows to the river.
        await using var guarded = await GuardedPage.OpenAsync(Playwright, "Desktop Chrome", "chromium");
        var page = guarded.Page;
        var game = await OpenGardenAsync(page, SaveAt(10, 17));

        for (var press = 1; press <= 3; press++)
        {
            await page.Keyboard.PressAsync("ArrowRight");
            await ExpectStandingOnAsync(game, Tile(10 + press, 17));
        }

        for (var press = 1; press <= 3; press++)
        {
            await page.Keyboard.PressAsync("d");
            await ExpectStandingOnAsync(game, Tile(13 + press, 17));
        }

        await ExpectACleanRunAsync(guarded, game);
    }

    [Test]
    public async Task Walk_WithAKeyHeldAndTheMenuOpenedWithEscape_ShouldWalkOnAndThenWait()
    {
        await using var guarded = await GuardedPage.OpenAsync(Playwright, "Desktop Chrome", "chromium");
        var page = guarded.Page;
        var game = await OpenGardenAsync(page, SaveAt(10, 17));

        await page.Keyboard.DownAsync("ArrowRight");
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (int.Parse((await game.GetAttributeAsync("data-player-tile"))!.Split(',')[0], CultureInfo.InvariantCulture) < 14)
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline);
            await page.WaitForTimeoutAsync(50);
        }

        await page.Keyboard.UpAsync("ArrowRight");
        await Expect(game).ToHaveAttributeAsync("data-moving", "false");
        var stopped = await game.GetAttributeAsync("data-player-tile");
        await page.Keyboard.PressAsync("Escape");
        await Expect(page.GetByTestId("settings-close")).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.WaitForTimeoutAsync(500);

        await Expect(game).ToHaveAttributeAsync("data-player-tile", stopped!);
        await page.Keyboard.PressAsync("Escape");
        await Expect(page.GetByTestId("settings-close")).ToHaveCountAsync(0);
        await page.Keyboard.PressAsync("ArrowUp");
        await Expect(game).ToHaveAttributeAsync("data-facing", "N");
        await ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Walk_WithTheDpadAndWithATapAcrossTheStream_ShouldMoveOneTileAndThenWalkAroundTheWater(string device, string engine)
    {
        var map = Garden().Map;
        map.KindAt(new TilePos(31, 20)).ShouldBe(TileKind.Water);
        map.KindAt(new TilePos(32, 20)).ShouldBe(TileKind.Water);
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        var game = await OpenGardenAsync(page, SaveAt(28, 20));

        await page.GetByTestId("dpad-right").TapAsync();
        await ExpectStandingOnAsync(game, Tile(29, 20));
        await page.GetByTestId("dpad-up").TapAsync();
        await ExpectStandingOnAsync(game, Tile(29, 19));
        await page.GetByTestId("dpad-down").TapAsync();
        await ExpectStandingOnAsync(game, Tile(29, 20));
        await page.GetByTestId("dpad-left").TapAsync();
        await ExpectStandingOnAsync(game, Tile(28, 20));
        await page.GetByTestId("dpad-right").TapAsync();
        await page.GetByTestId("dpad-right").TapAsync();
        await ExpectStandingOnAsync(game, Tile(30, 20));

        await TapTileAsync(page, device, 4, 0);

        await ExpectStandingOnAsync(game, Tile(34, 20));
        await ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium", PlayerCharacter.Adam)]
    [TestCase("Desktop Chrome", "chromium", PlayerCharacter.Woman)]
    [TestCase("Pixel 7", "chromium", PlayerCharacter.Adam)]
    [TestCase("Pixel 7", "chromium", PlayerCharacter.Woman)]
    [TestCase("iPhone 13", "webkit", PlayerCharacter.Adam)]
    [TestCase("iPhone 13", "webkit", PlayerCharacter.Woman)]
    public async Task Walk_InAllEightFacings_ShouldHaveTheVerdictOkOnEveryFrame(string device, string engine, PlayerCharacter character)
    {
        var open = OpenGround();
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        var game = await OpenGardenAsync(page, SaveAt(open.X, open.Y, character));
        var x = open.X;
        var y = open.Y;

        foreach (var facing in EightFacings)
        {
            await TapTileAsync(page, device, facing.X, facing.Y);
            x += facing.X;
            y += facing.Y;

            await ExpectStandingOnAsync(game, Tile(x, y));
            await Expect(game).ToHaveAttributeAsync("data-facing", facing.Name);
            await Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
            await KeepScreenshotAsync(page, $"m1-{device}-{character}-{facing.Name}".Replace(' ', '-'));
        }

        Tile(x, y).ShouldBe(open.ToString());
        await ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Rotate_WithTheSettingsOpen_ShouldKeepTheTileTheSettingsAndTheFit(string device, string engine)
    {
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        var portrait = page.ViewportSize ?? throw new InvalidOperationException("The device has no viewport.");
        var game = await OpenGardenAsync(page, SaveAt(10, 17));
        await page.GetByTestId("dpad-right").TapAsync();
        await ExpectStandingOnAsync(game, Tile(11, 17));
        await page.GetByTestId("menu-button").TapAsync();
        await page.GetByTestId("text-size-L").CheckAsync();
        (await OverflowingAsync(page)).ShouldBeEmpty();

        await page.SetViewportSizeAsync(portrait.Height, portrait.Width);

        await Expect(page.GetByTestId("settings-close")).ToBeVisibleAsync();
        await Expect(page.GetByTestId("text-size-L")).ToBeCheckedAsync();
        await Expect(game).ToHaveAttributeAsync("data-player-tile", Tile(11, 17));
        await Expect(game).ToHaveAttributeAsync("data-facing", "E");
        var canvas = await page.GetByTestId("game-canvas").BoundingBoxAsync();
        canvas.ShouldNotBeNull().Width.ShouldBeGreaterThan(canvas.Height);
        (await OverflowingAsync(page)).ShouldBeEmpty();
        await page.GetByTestId("settings-close").TapAsync();
        await page.GetByTestId("dpad-right").TapAsync();
        await ExpectStandingOnAsync(game, Tile(12, 17));

        await page.SetViewportSizeAsync(portrait.Width, portrait.Height);

        await Expect(game).ToHaveAttributeAsync("data-player-tile", Tile(12, 17));
        (await OverflowingAsync(page)).ShouldBeEmpty();
        await ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Reload_AfterAWalk_ShouldResumeOnTheSameTileWithTheSameFacing(string device, string engine)
    {
        var spawn = Garden().Map.Spawn("adam");
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        var game = await OpenGardenAsync(page);
        (await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", SaveCodec.SaveKey)).ShouldBeNull();
        await PressAsync(page, device, "down");
        await ExpectStandingOnAsync(game, Tile(spawn.X, spawn.Y + 1));
        await PressAsync(page, device, "right");
        await ExpectStandingOnAsync(game, Tile(spawn.X + 1, spawn.Y + 1));
        var saved = await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", SaveCodec.SaveKey);

        await page.ReloadAsync();

        game = page.GetByTestId("game");
        await Expect(game).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await Expect(game).ToHaveAttributeAsync("data-player-tile", Tile(spawn.X + 1, spawn.Y + 1));
        await Expect(game).ToHaveAttributeAsync("data-facing", "E");
        saved.ShouldBe(SaveAt(spawn.X + 1, spawn.Y + 1, facing: Facing.E));
        await ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Choose_TextSizeXLAt360By640_ShouldFitKeepEveryButton48PixelsOrMoreAndBeKept(string device, string engine)
    {
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        await page.SetViewportSizeAsync(360, 640);
        var game = await OpenGardenAsync(page);
        await Expect(game).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\btext-m\b"));
        (await OverflowingAsync(page)).ShouldBeEmpty();

        await page.GetByTestId("menu-button").ClickAsync();
        await page.GetByTestId("text-size-S").CheckAsync();
        var smallest = await game.EvaluateAsync<string>("element => getComputedStyle(element).fontSize");
        await page.GetByTestId("text-size-XL").CheckAsync();
        await page.GetByTestId("sound").UncheckAsync();

        await Expect(game).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\btext-xl\b"));
        double.Parse(smallest.Replace("px", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(18);
        (await game.EvaluateAsync<string>("element => getComputedStyle(element).fontSize")).ShouldBe("28px");
        (await OverflowingAsync(page)).ShouldBeEmpty();
        var targets = await page.EvaluateAsync<double[][]>(
            "() => [...document.querySelectorAll('#game button, #game .game-choice, #game .game-panel a')].map(element => element.getBoundingClientRect()).filter(box => box.width > 0).map(box => [box.width, box.height])");
        targets.Length.ShouldBeGreaterThanOrEqualTo(8);
        targets.ShouldAllBe(box => box[0] >= 47.5 && box[1] >= 47.5);
        (await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", SaveCodec.SettingsKey))
            .ShouldBe(SaveCodec.WriteSettings(new GameSettings { TextSize = TextSize.XL, Sound = false }));

        await page.ReloadAsync();

        game = page.GetByTestId("game");
        await Expect(game).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await Expect(game).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\btext-xl\b"));
        await page.GetByTestId("menu-button").ClickAsync();
        await Expect(page.GetByTestId("text-size-XL")).ToBeCheckedAsync();
        await Expect(page.GetByTestId("sound")).Not.ToBeCheckedAsync();
        (await OverflowingAsync(page)).ShouldBeEmpty();
        await ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheGardenWithASavedGameThatCannotBeRead_ShouldSayItKeepItAsideAndStartAgain(string device, string engine)
    {
        var spawn = Garden().Map.Spawn("adam");
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;

        var game = await OpenGardenAsync(page, save: "not a saved game");

        await Expect(page.GetByTestId("save-notice")).ToContainTextAsync(GameText.SaveUnreadable);
        (await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", SaveCodec.CorruptKey)).ShouldBe("not a saved game");
        (await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", SaveCodec.SaveKey)).ShouldBeNull();
        await Expect(game).ToHaveAttributeAsync("data-player-tile", spawn.ToString());
        await page.GetByTestId("save-notice-close").ClickAsync();
        await Expect(page.GetByTestId("save-notice-close")).ToHaveCountAsync(0);
        await PressAsync(page, device, "down");
        await ExpectStandingOnAsync(game, Tile(spawn.X, spawn.Y + 1));
        await ExpectACleanRunAsync(guarded, game);
    }

    [Test]
    public async Task Walk_OnAPixel7WithTheProcessorSlowedFourTimes_ShouldKeepTheMedianFrameAt20MillisecondsOrLess()
    {
        // Row 17 of the map is open from the Pison meadows to the river: five seconds of walking east fit in it.
        await using var guarded = await GuardedPage.OpenAsync(Playwright, "Pixel 7", "chromium");
        var page = guarded.Page;
        var game = await OpenGardenAsync(page, SaveAt(3, 17));
        var session = await page.Context.NewCDPSessionAsync(page);
        await session.SendAsync("Emulation.setCPUThrottlingRate", new Dictionary<string, object> { ["rate"] = 4 });
        await page.GetByTestId("dpad-right").DispatchEventAsync("pointerdown");
        await Expect(game).ToHaveAttributeAsync("data-moving", "true");

        var frames = await page.EvaluateAsync<double[]>(
            """
            () => new Promise(resolve => {
                const times = [];
                let first;
                let last;
                const frame = now => {
                    if (last !== undefined) { times.push(now - last); }
                    first ??= now;
                    last = now;
                    if (now - first < 5000) { requestAnimationFrame(frame); } else { resolve(times); }
                };
                requestAnimationFrame(frame);
            })
            """);
        var tile = await game.GetAttributeAsync("data-player-tile");
        await page.GetByTestId("dpad-right").DispatchEventAsync("pointerup");
        await session.SendAsync("Emulation.setCPUThrottlingRate", new Dictionary<string, object> { ["rate"] = 1 });

        var sorted = frames.Order().ToArray();
        var median = sorted[sorted.Length / 2];
        TestContext.Out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Frame budget, Pixel 7 profile, CPU slowed 4 times: {frames.Length} frames in 5 s, median {median:0.0} ms, 95th percentile {sorted[(int)(sorted.Length * 0.95)]:0.0} ms, longest {sorted[^1]:0.0} ms."));
        median.ShouldBeLessThanOrEqualTo(20);
        frames.Length.ShouldBeGreaterThan(150);
        int.Parse(tile!.Split(',')[0], CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(3 + 15);
        await ExpectACleanRunAsync(guarded, game);
    }

    /// <summary>A screenshot of a key frame, kept with the test results for the review of rule M1. Never compared.</summary>
    private static async Task KeepScreenshotAsync(IPage page, string name)
    {
        var folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "m1-screenshots");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".png");
        await page.ScreenshotAsync(new() { Path = path });
        TestContext.AddTestAttachment(path);
    }
}
