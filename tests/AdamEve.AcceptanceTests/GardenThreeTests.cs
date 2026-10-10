using System.Globalization;
using System.Text.Json;
using AdamEve.Content;
using AdamEve.Content.Garden;
using AdamEve.Core.Game;
using AdamEve.Core.Rigs;
using AdamEve.Core.Saves;
using AdamEve.Core.World;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace AdamEve.AcceptanceTests;

/// <summary>
/// The trial of the Three.js renderer (docs/spike-threejs.md), on the three device profiles of the design: the
/// garden of slice S2 asked for with <c>?renderer=three</c>. The game is the same, so what the tests of the canvas
/// read on the game root means the same here; what is new is read from the pixels the renderer drew and from the
/// requests the page made.
/// </summary>
[TestFixture]
public class GardenThreeTests : PlaywrightTest
{
    private const string Three = "garden?renderer=three";

    // A headless Chromium has WebGL only with one of these, by the machine it runs on. The first that gives a
    // WebGL 2 context is used for every test here, and the test output names it and the renderer behind it.
    private static readonly string[][] ChromiumArguments =
    [
        [],
        ["--enable-unsafe-swiftshader"],
        ["--use-angle=swiftshader", "--enable-unsafe-swiftshader"],
        ["--use-angle=gl-egl", "--enable-gpu", "--ignore-gpu-blocklist"],
    ];

    private static readonly (string Name, int X, int Y)[] EightFacings =
    [
        ("N", 0, -1), ("S", 0, 1), ("E", 1, 0), ("W", -1, 0), ("NE", 1, -1), ("SW", -1, 1), ("SE", 1, 1), ("NW", -1, -1),
    ];

    private static readonly int[] Skin = [0xE2B994, 0xC99A73];

    private static readonly SemaphoreSlim Probing = new(1, 1);
    private static string[]? chromiumArguments;
    private static string webGlOfChromium = "unknown";

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

    /// <summary>The arguments a browser of this engine needs here to have WebGL.</summary>
    private async Task<string[]> ArgumentsAsync(string engine)
    {
        if (engine != "chromium")
        {
            return [];
        }

        await Probing.WaitAsync();
        try
        {
            if (chromiumArguments is null)
            {
                foreach (var candidate in ChromiumArguments)
                {
                    await using var browser = await Playwright.Chromium.LaunchAsync(new() { Args = candidate });
                    var page = await browser.NewPageAsync();
                    var renderer = await page.EvaluateAsync<string?>(
                        """
                        () => {
                            const context = document.createElement('canvas').getContext('webgl2');
                            if (!context) { return null; }
                            const names = context.getExtension('WEBGL_debug_renderer_info');
                            return names ? context.getParameter(names.UNMASKED_RENDERER_WEBGL) : 'not named';
                        }
                        """);
                    if (renderer is not null)
                    {
                        chromiumArguments = candidate;
                        webGlOfChromium = renderer;
                        break;
                    }
                }
            }
        }
        finally
        {
            Probing.Release();
        }

        chromiumArguments.ShouldNotBeNull("No headless Chromium here has WebGL 2 with any of the arguments tried: the Three.js renderer cannot be tested on this machine.");
        return chromiumArguments;
    }

    private async Task<GuardedPage> OpenPageAsync(string device, string engine, bool reducedMotion = false) =>
        await GuardedPage.OpenAsync(Playwright, device, engine, reducedMotion, await ArgumentsAsync(engine));

    /// <summary>Opens the garden with the Three.js renderer, from a saved game when one is given, and waits for its first frames.</summary>
    private async Task<ILocator> OpenGardenAsync(IPage page, string? save = null, string address = Three)
    {
        if (save is not null)
        {
            await page.GotoAsync(Site.BaseAddress + "404.html");
            await page.EvaluateAsync("entry => localStorage.setItem(entry[0], entry[1])", new[] { SaveCodec.SaveKey, save });
        }

        await page.GotoAsync(Site.BaseAddress + address);
        var game = page.GetByTestId("game");
        await Expect(game).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
        return game;
    }

    private async Task ExpectThreeDrawsAsync(ILocator game)
    {
        await Expect(game).ToHaveAttributeAsync("data-renderer", "three");
        (await game.GetAttributeAsync("data-renderer-fallback")).ShouldBeNull();
    }

    private async Task ExpectStandingOnAsync(ILocator game, string tile)
    {
        await Expect(game).ToHaveAttributeAsync("data-player-tile", tile, new() { Timeout = 15_000 });
        await Expect(game).ToHaveAttributeAsync("data-moving", "false");
    }

    private async Task TapTileAsync(IPage page, string device, int tilesRight, int tilesDown)
    {
        var canvas = page.GetByTestId("game-canvas");
        var box = await canvas.BoundingBoxAsync() ?? throw new InvalidOperationException("The canvas has no box.");
        var tile = Math.Max(Math.Min(box.Width, box.Height) / Camera.ShortSideTiles, Math.Max(box.Width, box.Height) / Camera.LongSideTiles);
        var position = new Position { X = (float)((box.Width / 2) + (tilesRight * tile)), Y = (float)((box.Height / 2) + (tilesDown * tile)) };
        if (HasTouch(device))
        {
            await canvas.TapAsync(new() { Position = position });
        }
        else
        {
            await canvas.ClickAsync(new() { Position = position });
        }
    }

    /// <summary>What may not happen in any test of the garden: the list of the canvas tests.</summary>
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

    /// <summary>
    /// The pixels the renderer drew in a rectangle of the play area (CSS pixels, from the middle of the canvas), as
    /// 0xRRGGBB: read from the WebGL canvas itself, in the frame it was drawn in.
    /// </summary>
    private static Task<int[]> PixelsAsync(IPage page, double left, double top, double width, double height) => page.EvaluateAsync<int[]>(
        """
        box => new Promise(resolve => requestAnimationFrame(() => {
            const canvas = document.getElementById('game-canvas');
            const ratio = canvas.width / canvas.clientWidth;
            const x = Math.round((canvas.clientWidth / 2 + box[0]) * ratio), y = Math.round((canvas.clientHeight / 2 + box[1]) * ratio);
            const w = Math.max(1, Math.round(box[2] * ratio)), h = Math.max(1, Math.round(box[3] * ratio));
            const copy = document.createElement('canvas');
            copy.width = w;
            copy.height = h;
            const context = copy.getContext('2d');
            context.drawImage(canvas, x, y, w, h, 0, 0, w, h);
            const data = context.getImageData(0, 0, w, h).data;
            const pixels = [];
            for (let at = 0; at < data.length; at += 4) { pixels.push((data[at] << 16) | (data[at + 1] << 8) | data[at + 2]); }
            resolve(pixels);
        }))
        """,
        new[] { left, top, width, height });

    private static bool Near(int pixel, int colour, int within) =>
        Math.Abs((pixel >> 16) - (colour >> 16)) <= within && Math.Abs(((pixel >> 8) & 255) - ((colour >> 8) & 255)) <= within && Math.Abs((pixel & 255) - (colour & 255)) <= within;

    /// <summary>
    /// The pelvic zone of the player's rig as it lies on the screen, from the middle of the play area where the
    /// player stands: logical pixels, two pixels inside the zone's own outline.
    /// </summary>
    private static (double Left, double Top, double Width, double Height) PelvicZone(Rig rig)
    {
        var zone = rig.Zones.Single(each => each.Id == "pelvis");
        double boneY = 0;
        for (var bone = rig.Bones[rig.BoneIndex(zone.Bone)]; ; bone = rig.Bones[rig.BoneIndex(bone.Parent)])
        {
            boneY += bone.Y;
            if (bone.Parent is null)
            {
                break;
            }
        }

        var xs = zone.Points.Where((_, index) => index % 2 == 0).ToList();
        var ys = zone.Points.Where((_, index) => index % 2 == 1).ToList();
        var half = Math.Min(Math.Abs(xs.Min()), Math.Abs(xs.Max())) - 2;
        const double FeetBelowTheMiddleOfTheTile = 8;
        return (-half, FeetBelowTheMiddleOfTheTile + boneY + ys.Min() + 2, half * 2, ys.Max() - ys.Min() - 4);
    }

    private static async Task KeepScreenshotAsync(IPage page, string name)
    {
        var folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "three-screenshots");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name.Replace(' ', '-') + ".png");
        await page.ScreenshotAsync(new() { Path = path, Caret = ScreenshotCaret.Initial, Animations = ScreenshotAnimations.Allow });
        TestContext.AddTestAttachment(path);
    }

    // For a screenshot Playwright adds a style sheet of its own; on WebKit the policy of the site refuses it and the
    // browser says so (GardenTests explains). At most one such line for each screenshot is set aside.
    private static void SetAsideRefusedStyleSheets(GuardedPage guarded, int screenshots)
    {
        const string RefusedStyleSheet = "Refused to apply a stylesheet because its hash, its nonce, or 'unsafe-inline' does not appear in the style-src directive of the Content Security Policy.";
        guarded.Errors.Count(error => error == RefusedStyleSheet).ShouldBeLessThanOrEqualTo(screenshots);
        guarded.Errors.RemoveAll(error => error == RefusedStyleSheet);
    }

    private static Task<double[]> FrameTimesAsync(IPage page) => page.EvaluateAsync<double[]>(
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

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheGardenWithRendererThree_ShouldDrawItWithWebGlAndTheVerdictOk(string device, string engine)
    {
        var spawn = Garden().Map.Spawn("adam");
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;

        var game = await OpenGardenAsync(page, SaveAt(spawn.X, spawn.Y));

        await ExpectThreeDrawsAsync(game);
        await Expect(game).ToHaveAttributeAsync("data-player-tile", spawn.ToString());
        await Expect(game).ToHaveAttributeAsync("data-motion", "on");
        await Expect(page.GetByTestId("status-line")).ToHaveTextAsync(GameText.CentralGlade);
        var canvas = await page.GetByTestId("game-canvas").EvaluateAsync<int[]>("canvas => [canvas.width, canvas.height, canvas.clientWidth, canvas.clientHeight]");
        canvas.ShouldAllBe(side => side >= 300);
        (await page.GetByTestId("game-canvas").EvaluateAsync<bool>("canvas => canvas.getContext('webgl2') !== null && canvas.getContext('2d') === null")).ShouldBeTrue();
        var box = await page.GetByTestId("game-canvas").BoundingBoxAsync() ?? throw new InvalidOperationException("The canvas has no box.");
        var pixels = await PixelsAsync(page, -box.Width / 2, -box.Height / 2, box.Width, box.Height);
        pixels.Distinct().Count().ShouldBeGreaterThan(200, "the picture is not blank: light and shadow make many colours");
        pixels.Count(pixel => Skin.Any(colour => Near(pixel, colour, 3))).ShouldBeGreaterThan(50, "the two figures are drawn in the colours of their rigs, without light");
        await KeepScreenshotAsync(page, $"three-{device}-glade");
        await page.WaitForTimeoutAsync(250);
        SetAsideRefusedStyleSheets(guarded, 1);
        TestContext.Out.WriteLine($"WebGL of the test browser ({engine}): {(engine == "chromium" ? webGlOfChromium : "as WebKit has it")}.");
        await ExpectACleanRunAsync(guarded, game);
    }

    [Test]
    public async Task Walk_WithTheArrowKeysAndWasdWithRendererThree_ShouldMoveOneTileForEachPress()
    {
        var open = OpenGround();
        await using var guarded = await OpenPageAsync("Desktop Chrome", "chromium");
        var page = guarded.Page;
        var game = await OpenGardenAsync(page, SaveAt(open.X - 1, open.Y));
        await ExpectThreeDrawsAsync(game);
        (string Key, int X, int Y, string Facing)[] presses =
        [
            ("ArrowRight", 1, 0, "E"), ("ArrowLeft", -1, 0, "W"), ("ArrowUp", 0, -1, "N"), ("ArrowDown", 0, 1, "S"),
            ("d", 1, 0, "E"), ("a", -1, 0, "W"), ("w", 0, -1, "N"), ("s", 0, 1, "S"),
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

    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Walk_WithTheDpadAndWithATapAcrossTheStreamWithRendererThree_ShouldMoveOneTileAndThenWalkAroundTheWater(string device, string engine)
    {
        var map = Garden().Map;
        map.KindAt(new TilePos(31, 20)).ShouldBe(TileKind.Water);
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;
        var game = await OpenGardenAsync(page, SaveAt(28, 20));
        await ExpectThreeDrawsAsync(game);

        await page.GetByTestId("dpad-right").TapAsync();
        await ExpectStandingOnAsync(game, Tile(29, 20));
        await page.GetByTestId("dpad-up").TapAsync();
        await ExpectStandingOnAsync(game, Tile(29, 19));
        await page.GetByTestId("dpad-down").TapAsync();
        await ExpectStandingOnAsync(game, Tile(29, 20));
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
    public async Task Walk_InAllEightFacingsWithRendererThree_ShouldHaveTheVerdictOkAndNoSkinInThePelvicZoneOnTheScreen(string device, string engine, PlayerCharacter character)
    {
        var open = OpenGround();
        var garden = Garden();
        var zone = PelvicZone(character == PlayerCharacter.Adam ? garden.Adam : garden.Woman);
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;
        var game = await OpenGardenAsync(page, SaveAt(open.X, open.Y, character));
        await ExpectThreeDrawsAsync(game);
        var box = await page.GetByTestId("game-canvas").BoundingBoxAsync() ?? throw new InvalidOperationException("The canvas has no box.");
        var scale = Math.Max(Math.Min(box.Width, box.Height) / Camera.ShortSideTiles, Math.Max(box.Width, box.Height) / Camera.LongSideTiles) / garden.Map.TileSize;
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
            var pixels = await PixelsAsync(page, zone.Left * scale, zone.Top * scale, zone.Width * scale, zone.Height * scale);
            pixels.Length.ShouldBeGreaterThan(100);
            if (!Enum.Parse<Facing>(facing.Name).IsTurnedAway())
            {
                // Facing the viewer or sideways the zone is behind the companion foliage: what the screen shows
                // there is foliage, in every pixel. Turned away, the zone is the figure's own back.
                pixels.Count(pixel => Skin.Any(colour => Near(pixel, colour, 24))).ShouldBe(0, $"facing {facing.Name}: the pelvic zone shows skin on the screen");
            }

            await KeepScreenshotAsync(page, $"three-m1-{device}-{character}-{facing.Name}");
        }

        await page.WaitForTimeoutAsync(250);
        SetAsideRefusedStyleSheets(guarded, EightFacings.Length);
        Tile(x, y).ShouldBe(open.ToString());
        await ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium", false)]
    [TestCase("Desktop Chrome", "chromium", true)]
    [TestCase("Pixel 7", "chromium", true)]
    [TestCase("iPhone 13", "webkit", true)]
    public async Task Stand_BesideTheStreamWithRendererThree_ShouldMoveTheWaterUnlessThePlayerAsksForReducedMotion(string device, string engine, bool reducedMotion)
    {
        // The stream is two tiles east of the player, who stands in the middle of the play area; nothing else is there.
        var map = Garden().Map;
        map.KindAt(new TilePos(31, 20)).ShouldBe(TileKind.Water);
        await using var guarded = await OpenPageAsync(device, engine, reducedMotion);
        var page = guarded.Page;
        var game = await OpenGardenAsync(page, SaveAt(29, 20));
        await ExpectThreeDrawsAsync(game);
        var box = await page.GetByTestId("game-canvas").BoundingBoxAsync() ?? throw new InvalidOperationException("The canvas has no box.");
        var tile = Math.Max(Math.Min(box.Width, box.Height) / Camera.ShortSideTiles, Math.Max(box.Width, box.Height) / Camera.LongSideTiles);

        var first = await PixelsAsync(page, tile * 1.6, -tile * 0.4, tile * 0.8, tile * 0.8);
        await page.WaitForTimeoutAsync(700);
        var second = await PixelsAsync(page, tile * 1.6, -tile * 0.4, tile * 0.8, tile * 0.8);

        await Expect(game).ToHaveAttributeAsync("data-motion", reducedMotion ? "off" : "on");
        first.Count(pixel => (pixel & 255) > ((pixel >> 16) & 255) + 20).ShouldBeGreaterThan(first.Length / 2, "the rectangle read is water");
        if (reducedMotion)
        {
            second.ShouldBe(first);
        }
        else
        {
            second.ShouldNotBe(first);
        }

        await ExpectACleanRunAsync(guarded, game);
    }

    [Test]
    public async Task Open_TheTitleAndTheGardenWithoutChoosingARenderer_ShouldAskForNoFileThatIsLoadedOnDemand()
    {
        var spawn = Garden().Map.Spawn("adam");
        await using var guarded = await OpenPageAsync("Pixel 7", "chromium");
        var page = guarded.Page;
        var asked = new List<string>();
        page.Request += (_, request) => asked.Add(new Uri(request.Url).AbsolutePath.TrimStart('/'));
        using var listed = JsonDocument.Parse(await (await page.APIRequest.GetAsync(Site.BaseAddress + "_health/files.json")).TextAsync());
        var onDemand = listed.RootElement.GetProperty("onDemand").EnumerateArray().Select(path => path.GetString()!).ToList();
        await page.GotoAsync(Site.BaseAddress + "404.html");
        await page.EvaluateAsync("entry => localStorage.setItem(entry[0], entry[1])", new[] { SaveCodec.SaveKey, SaveAt(spawn.X, spawn.Y) });

        await page.GotoAsync(Site.BaseAddress);
        await page.GetByTestId("continue").ClickAsync();
        var game = page.GetByTestId("game");
        await Expect(game).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await page.GetByTestId("dpad-down").TapAsync();
        await ExpectStandingOnAsync(game, Tile(spawn.X, spawn.Y + 1));
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var firstVisit = asked.ToList();

        await page.GetByTestId("menu-button").TapAsync();
        await page.GetByTestId("renderer-three").CheckAsync();
        await Expect(game).ToHaveAttributeAsync("data-renderer", "three", new() { Timeout = 30_000 });

        onDemand.ShouldBe(["js/render-three.js", "lib/three/LICENSE.txt", "lib/three/README.md", "lib/three/three.core.min.js", "lib/three/three.module.min.js"], ignoreOrder: true);
        await Expect(game).ToHaveAttributeAsync("data-renderer", "three");
        firstVisit.ShouldContain("js/render.js");
        firstVisit.Intersect(onDemand).ShouldBeEmpty();
        firstVisit.ShouldAllBe(path => !path.StartsWith("lib/", StringComparison.Ordinal));
        asked.Skip(firstVisit.Count).ShouldBe(["js/render-three.js", "lib/three/three.module.min.js", "lib/three/three.core.min.js"], ignoreOrder: true);
        await ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Choose_TheRendererInTheSettings_ShouldChangeWhatDrawsKeepThePlaceAndNotBeSaved(string device, string engine)
    {
        var open = OpenGround();
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;
        var game = await OpenGardenAsync(page, SaveAt(open.X, open.Y), "garden");
        await Expect(game).ToHaveAttributeAsync("data-renderer", "canvas");
        await page.GetByTestId("menu-button").ClickAsync();
        await Expect(page.GetByTestId("renderer-trial")).ToContainTextAsync(GameText.RendererTrial);
        await Expect(page.GetByTestId("renderer-canvas")).ToBeCheckedAsync();

        await page.GetByTestId("renderer-three").CheckAsync();

        await ExpectThreeDrawsAsync(game);
        await Expect(game).ToHaveAttributeAsync("data-player-tile", open.ToString());
        await Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
        (await page.GetByTestId("game-canvas").EvaluateAsync<bool>("canvas => canvas.getContext('webgl2') !== null")).ShouldBeTrue();
        (await page.EvaluateAsync<string?>("key => sessionStorage.getItem(key)", RendererTrial.SessionKey)).ShouldBe("three");
        (await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", SaveCodec.SettingsKey) ?? string.Empty).ShouldNotContain("three", Case.Insensitive);
        (await page.EvaluateAsync<string?>("key => localStorage.getItem(key)", SaveCodec.SaveKey) ?? string.Empty).ShouldNotContain("three", Case.Insensitive);

        await page.ReloadAsync();
        game = page.GetByTestId("game");
        await Expect(game).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await ExpectThreeDrawsAsync(game);
        await page.GetByTestId("menu-button").ClickAsync();
        await page.GetByTestId("renderer-canvas").CheckAsync();

        await Expect(game).ToHaveAttributeAsync("data-renderer", "canvas");
        (await page.GetByTestId("game-canvas").EvaluateAsync<bool>("canvas => canvas.getContext('2d') !== null")).ShouldBeTrue();
        await page.GetByTestId("settings-close").ClickAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await ExpectStandingOnAsync(game, Tile(open.X, open.Y + 1));
        await ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheGardenWithRendererThreeInABrowserWithoutWebGl_ShouldDrawWithTheCanvasSaySoAndNotAskForThree(string device, string engine)
    {
        var spawn = Garden().Map.Spawn("adam");
        await using var guarded = await GuardedPage.OpenAsync(Playwright, device, engine);
        var page = guarded.Page;
        var asked = new List<string>();
        page.Request += (_, request) => asked.Add(new Uri(request.Url).AbsolutePath.TrimStart('/'));
        // A browser without WebGL: a canvas gives no WebGL context.
        await page.AddInitScriptAsync(
            """
            const getContext = HTMLCanvasElement.prototype.getContext;
            HTMLCanvasElement.prototype.getContext = function (kind, ...rest) {
                return String(kind).startsWith('webgl') ? null : getContext.call(this, kind, ...rest);
            };
            """);

        var game = await OpenGardenAsync(page);

        await Expect(game).ToHaveAttributeAsync("data-renderer", "canvas");
        await Expect(game).ToHaveAttributeAsync("data-renderer-fallback", "webgl-unavailable");
        (await page.GetByTestId("game-canvas").EvaluateAsync<bool>("canvas => canvas.getContext('2d') !== null")).ShouldBeTrue();
        asked.ShouldContain("js/render-three.js");
        asked.ShouldAllBe(path => !path.StartsWith("lib/", StringComparison.Ordinal));
        await page.Keyboard.PressAsync("ArrowDown");
        await ExpectStandingOnAsync(game, Tile(spawn.X, spawn.Y + 1));
        await ExpectACleanRunAsync(guarded, game);
    }

    [Test]
    public async Task Walk_OnAPixel7WithTheProcessorSlowedFourTimes_ShouldReportTheFramesOfBothRenderers()
    {
        // The measurement of GardenTests, with the same walk, for each renderer in turn. The canvas has its budget
        // there (a median of 20 ms). Here nothing is required of the Three.js number: a headless browser of a build
        // machine draws WebGL in software, on the processor, which says nothing about a phone's graphics chip.
        var lines = new List<string>();
        foreach (var renderer in new[] { RendererKind.Canvas, RendererKind.Three })
        {
            await using var guarded = await OpenPageAsync("Pixel 7", "chromium");
            var page = guarded.Page;
            var game = await OpenGardenAsync(page, SaveAt(3, 17), "garden?renderer=" + RendererTrial.NameOf(renderer));
            await Expect(game).ToHaveAttributeAsync("data-renderer", RendererTrial.NameOf(renderer));
            var session = await page.Context.NewCDPSessionAsync(page);
            await session.SendAsync("Emulation.setCPUThrottlingRate", new Dictionary<string, object> { ["rate"] = 4 });
            await page.GetByTestId("dpad-right").DispatchEventAsync("pointerdown");
            await Expect(game).ToHaveAttributeAsync("data-moving", "true", new() { Timeout = 30_000 });

            var frames = await FrameTimesAsync(page);

            await page.GetByTestId("dpad-right").DispatchEventAsync("pointerup");
            await session.SendAsync("Emulation.setCPUThrottlingRate", new Dictionary<string, object> { ["rate"] = 1 });
            var sorted = frames.Order().ToArray();
            sorted.Length.ShouldBeGreaterThan(0);
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"{RendererTrial.NameOf(renderer),-6}: {frames.Length} frames in 5 s, median {sorted[sorted.Length / 2]:0.0} ms, 95th percentile {sorted[(int)(sorted.Length * 0.95)]:0.0} ms, longest {sorted[^1]:0.0} ms"));
            await ExpectACleanRunAsync(guarded, game);
        }

        var report = $"Frame times, Pixel 7 profile, CPU slowed 4 times, WebGL by {webGlOfChromium}:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
        TestContext.Out.WriteLine(report);
        var folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "three-screenshots");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "frame-times.txt");
        await File.WriteAllTextAsync(path, report + Environment.NewLine);
        TestContext.AddTestAttachment(path);
    }
}
