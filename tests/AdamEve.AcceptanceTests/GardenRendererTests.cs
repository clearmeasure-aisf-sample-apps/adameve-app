using System.Globalization;
using System.Text.Json;
using AdamEve.Content;
using AdamEve.Core.Game;
using AdamEve.Core.Rigs;
using AdamEve.Core.Saves;
using AdamEve.Core.World;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace AdamEve.AcceptanceTests;

/// <summary>
/// What draws the garden (design, section 7.1 and decisions D18): the Three.js renderer with the perspective camera
/// of the game, and the canvas renderer as its fallback. These tests read what no attribute of the page can say:
/// the pixels the renderer drew, where its own camera puts points of the garden, how much it drew, and which files
/// the page asked for. The third check of the amended rule M1 is here: the rendered image of both figures in all
/// eight facings.
/// </summary>
[TestFixture]
public class GardenRendererTests : PlaywrightTest
{
    private const string Shots = "renderer-screenshots";
    private const string Sheets = "m1-sheets";

    // The eight facings as one walk that ends where it began, each a step to a neighbouring tile.
    private static readonly (Facing Facing, int X, int Y)[] EightFacings =
    [
        (Facing.N, 0, -1), (Facing.S, 0, 1), (Facing.E, 1, 0), (Facing.W, -1, 0), (Facing.NE, 1, -1), (Facing.SW, -1, 1), (Facing.SE, 1, 1), (Facing.NW, -1, -1),
    ];

    // The order of the eight facings on a review sheet, from left to right: turning once around, from the front.
    private static readonly Facing[] SheetOrder = [Facing.S, Facing.SE, Facing.E, Facing.NE, Facing.N, Facing.NW, Facing.W, Facing.SW];

    private static readonly string[] OnDemandOfThree = ["js/audio.js", "js/render-three.js", "js/shell.js", "lib/three/three.core.min.js", "lib/three/three.module.min.js"];

    private bool HasTouch(string device) => Playwright.Devices[device].HasTouch == true;

    /// <summary>Opens a page on a device profile, upright or on its side.</summary>
    private async Task<GuardedPage> OpenPageAsync(string device, string engine, bool? landscape = null, bool reducedMotion = false)
    {
        var guarded = await GuardedPage.OpenAsync(Playwright, device, engine, reducedMotion);
        var size = guarded.Page.ViewportSize ?? throw new InvalidOperationException("The device has no viewport.");
        if (landscape is { } asked && asked != size.Width > size.Height)
        {
            await guarded.Page.SetViewportSizeAsync(size.Height, size.Width);
        }

        return guarded;
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

    private static List<string> Asked(IPage page)
    {
        var asked = new List<string>();
        page.Request += (_, request) => asked.Add(new Uri(request.Url).AbsolutePath.TrimStart('/'));
        return asked;
    }

    private static async Task<List<string>> OnDemandAsync(IPage page)
    {
        using var listed = JsonDocument.Parse(await (await page.APIRequest.GetAsync(Site.BaseAddress + "_health/files.json")).TextAsync());
        return [.. listed.RootElement.GetProperty("onDemand").EnumerateArray().Select(path => path.GetString()!)];
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheGarden_ShouldDrawItInPerspectiveWithWebGlHazeAndAFarLayerAndTheVerdictOk(string device, string engine)
    {
        var spawn = GardenView.Garden().Map.Spawn("adam");
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;

        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(spawn.X, spawn.Y));

        await GardenView.ExpectThreeDrawsAsync(game);
        await Expect(game).ToHaveAttributeAsync("data-player-tile", spawn.ToString());
        await Expect(game).ToHaveAttributeAsync("data-motion", "on");
        await Expect(page.GetByTestId("status-line")).ToHaveTextAsync(GameText.CentralGlade);
        (await page.GetByTestId("game-canvas").EvaluateAsync<bool>("canvas => canvas.getContext('webgl2') !== null && canvas.getContext('2d') === null")).ShouldBeTrue();
        var (width, height) = await GardenView.PlayAreaAsync(page);
        var camera = await GardenView.CameraAsync(page, spawn);
        var picture = await GardenView.PixelsAsync(page, 0, 0, width, height);
        picture.Pixels.Distinct().Count().ShouldBeGreaterThan(200, "the picture is not blank: light and shadow make many colours");
        picture.Pixels.Count(pixel => GardenView.IsSkin(pixel, 3)).ShouldBeGreaterThan(50, "the two figures are drawn in the colours of their rigs, without light");
        var sky = await GardenView.PixelsAsync(page, width * 0.1, 0, width * 0.8, 3);
        // A tall tree or the light about the tree of life may reach the top of the picture: most of it is sky.
        sky.Pixels.Count(pixel => (pixel & 255) > 190 && (pixel & 255) >= (pixel >> 16)).ShouldBeGreaterThan(sky.Pixels.Length * 3 / 5, "the top of the picture is the sky of the far layer");
        var haze = await GardenView.PixelsAsync(page, width * 0.1, camera.HazeLine + 2, width * 0.8, 2);
        // Trees, the forest behind the glade and the light about the tree of life stand before that line here and there.
        haze.Pixels.Count(pixel => GardenView.Near(pixel, 0xE2E8C6, 24)).ShouldBeGreaterThan(haze.Pixels.Length * 2 / 5, "where the haze closes, the ground has its colour");
        var near = await GardenView.PixelsAsync(page, width * 0.35, height - 6, width * 0.3, 4);
        near.Pixels.Count(pixel => ((pixel >> 8) & 255) > (pixel >> 16) && ((pixel >> 8) & 255) > (pixel & 255)).ShouldBeGreaterThan(near.Pixels.Length / 2, "the near ground is green, clear of haze");
        var probe = await GardenView.ProbeAsync(page);
        probe.PixelRatio.ShouldBeLessThanOrEqualTo(2);
        probe.ContextLost.ShouldBeFalse();
        await GardenView.KeepScreenshotAsync(page, Shots, $"garden-{device}-glade");
        await GardenView.SetAsideRefusedStyleSheetsAsync(guarded, 1);
        TestContext.Out.WriteLine($"WebGL of the test browser ({engine}): {(engine == "chromium" ? GuardedPage.WebGlOfChromium : "as WebKit has it")}.");
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium", false)]
    [TestCase("Desktop Chrome", "chromium", true)]
    [TestCase("Pixel 7", "chromium", false)]
    [TestCase("Pixel 7", "chromium", true)]
    [TestCase("iPhone 13", "webkit", false)]
    [TestCase("iPhone 13", "webkit", true)]
    public async Task Open_TheGladeWithAndWithoutReducedMotion_ShouldPlantItFillTheAirAndMoveNothingByItselfUnderReducedMotion(string device, string engine, bool reducedMotion)
    {
        // The two trees in the midst of the garden are in view from here, with the planted trees, the edge of the
        // thicket and the flowers. Everything that moves by itself has one clock: the wind, the water, the mist, the
        // pollen, the fireflies, the butterflies, the clouds, the birds, the flicker of one tree, the breath of a
        // standing figure. Under reduced motion it stands, and two frames are the same picture.
        var place = new TilePos(30, 27);
        var map = GardenView.Garden().Map;
        map.IsWalkable(place).ShouldBeTrue();
        await using var guarded = await OpenPageAsync(device, engine, reducedMotion: reducedMotion);
        var page = guarded.Page;
        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(place.X, place.Y));
        await GardenView.ExpectThreeDrawsAsync(game);
        var (width, height) = await GardenView.PlayAreaAsync(page);
        await page.WaitForTimeoutAsync(500);

        var first = await GardenView.ProbeAsync(page);
        var before = await GardenView.PixelsAsync(page, 0, 0, width, height);
        await page.WaitForTimeoutAsync(600);
        var second = await GardenView.ProbeAsync(page);
        var after = await GardenView.PixelsAsync(page, 0, 0, width, height);

        await Expect(game).ToHaveAttributeAsync("data-motion", reducedMotion ? "off" : "on");
        first.Trees.ShouldBeGreaterThanOrEqualTo(3, "the two trees in the midst of the garden and the fig tree, at least");
        first.Plants.ShouldBeGreaterThan(0, "shrubs, rocks or reeds are in view");
        first.Glows.ShouldBeGreaterThan(20, "the light about the two trees, the blossoms of the tree of life, pollen");
        first.Shafts.ShouldBeGreaterThan(0, "light falls beside the tree of life");
        if (reducedMotion)
        {
            first.Seconds.ShouldBe(0);
            second.Seconds.ShouldBe(0);
            after.Pixels.ShouldBe(before.Pixels, "nothing moves by itself");
        }
        else
        {
            second.Seconds.ShouldBeGreaterThan(first.Seconds);
            after.Pixels.ShouldNotBe(before.Pixels);
        }

        await GardenView.KeepScreenshotAsync(page, Shots, $"garden-{device}-the-two-trees{(reducedMotion ? "-reduced-motion" : string.Empty)}");
        await GardenView.SetAsideRefusedStyleSheetsAsync(guarded, 1);
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium", true)]
    [TestCase("Desktop Chrome", "chromium", false)]
    [TestCase("Pixel 7", "chromium", false)]
    [TestCase("Pixel 7", "chromium", true)]
    [TestCase("iPhone 13", "webkit", false)]
    [TestCase("iPhone 13", "webkit", true)]
    public async Task Project_PointsOfTheGardenThroughTheCameraOfTheRenderer_ShouldLieWhereTheCameraOfTheGamePutsThem(string device, string engine, bool landscape)
    {
        var open = GardenView.OpenGround(3);
        await using var guarded = await OpenPageAsync(device, engine, landscape);
        var page = guarded.Page;
        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(open.X, open.Y));
        await GardenView.ExpectThreeDrawsAsync(game);
        var (width, height) = await GardenView.PlayAreaAsync(page);
        (width > height).ShouldBe(landscape);
        var camera = await GardenView.CameraAsync(page, open);
        var feet = GardenView.FeetOn(open);
        var points = new List<(double X, double Height, double Y)>();
        // The ground under the four corners of the play area, under its centre and under the line where the haze closes.
        foreach (var (x, y) in new[] { (0.0, height), (width, height), (0.0, 0.0), (width, 0.0), (width / 2, height / 2), (0.0, camera.HazeLine), (width, camera.HazeLine), (width * 0.25, height * 0.7) })
        {
            var ground = camera.GroundAt(x, y).ShouldNotBeNull();
            points.Add((ground.X, 0, ground.Y));
        }

        // Things with height: the top of a tree two tiles north-west, a stone, and the head of the player's figure.
        points.Add(((open.X - 2) * 32, 60, (open.Y - 2) * 32));
        points.Add(((open.X + 3.5) * 32, 12, (open.Y + 1.5) * 32));
        var head = PerspectiveCamera.FigurePoint(feet.X, feet.Y, 5, 44);
        points.Add((head.X, head.Height, head.Y));

        var probe = await GardenView.ProbeAsync(page, [.. points]);

        probe.Screen.Length.ShouldBe(points.Count);
        for (var index = 0; index < points.Count; index++)
        {
            var expected = camera.Project(points[index].X, points[index].Y, points[index].Height);
            probe.Screen[index].X.ShouldBe(expected.X, 0.5, $"point {index}");
            probe.Screen[index].Y.ShouldBe(expected.Y, 0.5, $"point {index}");
        }

        probe.Screen[0].X.ShouldBe(0, 0.5);
        probe.Screen[0].Y.ShouldBe(height, 0.5);
        probe.Screen[3].X.ShouldBe(width, 0.5);
        probe.Screen[3].Y.ShouldBe(0, 0.5);
        probe.Screen[4].X.ShouldBe(width / 2, 0.5);
        probe.Screen[4].Y.ShouldBe(height / 2, 0.5);
        var figure = camera.Project(feet.X, feet.Y);
        probe.Screen[^1].X.ShouldBe(figure.X + (5 * camera.ScaleAt(feet.X, feet.Y)), 0.5, "a figure faces the camera squarely: one scale for the whole of it");
        probe.Screen[^1].Y.ShouldBe(figure.Y - (44 * camera.ScaleAt(feet.X, feet.Y)), 0.5);
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium", true)]
    [TestCase("Desktop Chrome", "chromium", false)]
    [TestCase("Pixel 7", "chromium", false)]
    [TestCase("Pixel 7", "chromium", true)]
    [TestCase("iPhone 13", "webkit", false)]
    [TestCase("iPhone 13", "webkit", true)]
    public async Task Tap_OnTilesTowardEachCornerOfThePlayArea_ShouldWalkToTheTileUnderTheTap(string device, string engine, bool landscape)
    {
        var open = GardenView.OpenGround(3);
        await using var guarded = await OpenPageAsync(device, engine, landscape);
        var page = guarded.Page;
        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(open.X, open.Y));
        await GardenView.ExpectThreeDrawsAsync(game);
        var (width, height) = await GardenView.PlayAreaAsync(page);
        var player = open;
        // Toward the top left, the bottom right, the top right and the bottom left of the play area, then home.
        (int X, int Y)[] hops = [(-3, -3), (3, 2), (3, -2), (-3, 2), (0, 1)];

        foreach (var hop in hops)
        {
            var target = new TilePos(player.X + hop.X, player.Y + hop.Y);
            var (x, y) = await GardenView.ScreenOfAsync(page, player, target);
            if (hop.X != 0)
            {
                ((x - (width / 2)) * hop.X).ShouldBeGreaterThan(width * 0.15, $"the tap toward {hop} is well off the middle");
                ((y - (height / 2)) * hop.Y).ShouldBeGreaterThan(height * 0.05);
            }

            await GardenView.TapTileAsync(page, HasTouch(device), player, target);

            await GardenView.ExpectStandingOnAsync(game, target.ToString());
            player = target;
        }

        player.ShouldBe(open);
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium", PlayerCharacter.Adam)]
    [TestCase("Desktop Chrome", "chromium", PlayerCharacter.Woman)]
    [TestCase("Pixel 7", "chromium", PlayerCharacter.Adam)]
    [TestCase("Pixel 7", "chromium", PlayerCharacter.Woman)]
    [TestCase("iPhone 13", "webkit", PlayerCharacter.Adam)]
    [TestCase("iPhone 13", "webkit", PlayerCharacter.Woman)]
    public async Task Walk_InAllEightFacings_ShouldDrawTheFigureTheCheckJudgedWithHairOverTheWomansChestAndOnlyTheFlatSkinInThePelvicZone(string device, string engine, PlayerCharacter character)
    {
        var open = GardenView.OpenGround(2);
        var garden = GardenView.Garden();
        var rig = character == PlayerCharacter.Adam ? garden.Adam : garden.Woman;
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;
        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(open.X, open.Y, character));
        await GardenView.ExpectThreeDrawsAsync(game);
        var ratio = (await GardenView.ProbeAsync(page)).PixelRatio;
        var player = open;

        foreach (var step in EightFacings)
        {
            var target = new TilePos(player.X + step.X, player.Y + step.Y);
            await GardenView.TapTileAsync(page, HasTouch(device), player, target);
            player = target;
            await GardenView.ExpectStandingOnAsync(game, player.ToString());
            await Expect(game).ToHaveAttributeAsync("data-facing", step.Facing.ToString());
            await Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
            var camera = await GardenView.CameraAsync(page, player);
            var feet = GardenView.FeetOn(player);
            var scale = camera.ScaleAt(feet.X, feet.Y);
            var why = $"{character} facing {step.Facing} on {device}";

            // The figure on the screen is the figure of the check, at the one scale of the place it stands on: its
            // outline lies where the camera of the game puts it. The stance breathes: the head rises by 0.6 at most.
            var bounds = GardenView.FigureBounds(rig, step.Facing);
            var margin = 6.0;
            var around = GardenView.OnScreen(camera, feet, bounds.Left - margin, bounds.Top - margin, bounds.Right + margin, bounds.Bottom + margin);
            var figure = await GardenView.PixelsAsync(page, around.Left, around.Top, around.Width, around.Height);
            var drawn = Enumerable.Range(0, figure.Pixels.Length).Where(index => GardenView.IsSkin(figure.Pixels[index], 3) || GardenView.Near(figure.Pixels[index], GardenView.Hair, 3)).ToList();
            drawn.Count.ShouldBeGreaterThan(200, why);
            var edge = margin * scale * ratio;
            // The hair sways in the stance, by half a logical pixel at most to either side.
            var sway = 0.5 * scale * ratio;
            ((double)drawn.Min(index => index % figure.Width)).ShouldBe(edge, 2.5 + sway, $"{why}: the left edge of the figure");
            ((double)drawn.Max(index => index % figure.Width)).ShouldBe(figure.Width - edge, 2.5 + sway, $"{why}: the right edge of the figure");
            ((double)drawn.Min(index => index / figure.Width)).ShouldBe(edge - (0.3 * scale * ratio), 2.5 + (0.3 * scale * ratio), $"{why}: the top edge of the figure");
            ((double)drawn.Max(index => index / figure.Width)).ShouldBe(figure.Height - edge, 2.5, $"{why}: the bottom edge of the figure");

            // The pelvic zone, two logical pixels inside its own outline: the body's flat skin colour and nothing
            // else. No second colour, no edge, no shape. In every facing, for both.
            var pelvis = GardenView.ZoneBounds(rig, RigStructure.Pelvis, step.Facing);
            var onPelvis = GardenView.OnScreen(camera, feet, pelvis.Left + 2, pelvis.Top + 2, pelvis.Right - 2, pelvis.Bottom - 2);
            var pelvic = await GardenView.PixelsAsync(page, onPelvis.Left, onPelvis.Top, onPelvis.Width, onPelvis.Height);
            pelvic.Pixels.Length.ShouldBeGreaterThan(100, why);
            pelvic.Pixels.Count(pixel => !GardenView.Near(pixel, GardenView.Skin, 3)).ShouldBe(0, $"{why}: the pelvic zone shows something other than the flat skin colour");
            pelvic.Pixels.Distinct().Count().ShouldBeLessThanOrEqualTo(2, $"{why}: the pelvic zone is one flat colour");

            if (character == PlayerCharacter.Woman && !step.Facing.IsTurnedAway())
            {
                // Her chest zone, not turned away: hair in every pixel, skin in none. (The zone rises with her breath
                // by 0.6 at most: one logical pixel is left out at its top and bottom, as the check insets it.)
                var chest = GardenView.ZoneBounds(rig, RigStructure.Chest, step.Facing);
                var onChest = GardenView.OnScreen(camera, feet, chest.Left, chest.Top - 0.6, chest.Right, chest.Bottom);
                var over = await GardenView.PixelsAsync(page, onChest.Left, onChest.Top, onChest.Width, onChest.Height);
                over.Pixels.Length.ShouldBeGreaterThan(100, why);
                over.Pixels.Count(pixel => GardenView.IsSkin(pixel, 24)).ShouldBe(0, $"{why}: the chest zone shows skin on the screen");
                over.Pixels.Count(pixel => !GardenView.Near(pixel, GardenView.Hair, 3)).ShouldBe(0, $"{why}: the chest zone shows something other than her hair");
            }

            // The review sheet: the figure, cut out of what the renderer drew, in the cell of its facing.
            var cell = GardenView.OnScreen(camera, feet, -20, -55, 20, 3);
            await page.EvaluateAsync(
                """
                cut => new Promise(resolve => requestAnimationFrame(() => {
                    const canvas = document.getElementById('game-canvas');
                    const ratio = canvas.width / canvas.clientWidth;
                    globalThis.reviewSheet ??= Object.assign(document.createElement('canvas'), { width: 8 * 240, height: 348 });
                    const context = globalThis.reviewSheet.getContext('2d');
                    context.imageSmoothingEnabled = false;
                    context.drawImage(canvas, cut[1] * ratio, cut[2] * ratio, cut[3] * ratio, cut[4] * ratio, cut[0] * 240, 0, 240, 348);
                    resolve();
                }))
                """,
                new[] { Array.IndexOf(SheetOrder, step.Facing), cell.Left, cell.Top, cell.Width, cell.Height });
            if (step.Facing == Facing.S)
            {
                await GardenView.KeepScreenshotAsync(page, Shots, $"garden-{device}-{character}-facing-S");
            }
        }

        var sheet = await page.EvaluateAsync<string>("() => globalThis.reviewSheet.toDataURL('image/png')");
        var path = GardenView.KeptPath(Sheets, $"m1-sheet-{device}-{character}.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(sheet[(sheet.IndexOf(',', StringComparison.Ordinal) + 1)..]));
        TestContext.AddTestAttachment(path, "The eight facings as the renderer drew them, from left to right: S, SE, E, NE, N, NW, W, SW.");
        await GardenView.SetAsideRefusedStyleSheetsAsync(guarded, 1);
        player.ShouldBe(open);
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium", PlayerCharacter.Adam)]
    [TestCase("Desktop Chrome", "chromium", PlayerCharacter.Woman)]
    [TestCase("Pixel 7", "chromium", PlayerCharacter.Adam)]
    [TestCase("Pixel 7", "chromium", PlayerCharacter.Woman)]
    [TestCase("iPhone 13", "webkit", PlayerCharacter.Adam)]
    [TestCase("iPhone 13", "webkit", PlayerCharacter.Woman)]
    public async Task Walk_InMidStepInSixFacings_ShouldDrawHairOverTheWomansChestAndOnlyTheFlatSkinInThePelvicZone(string device, string engine, PlayerCharacter character)
    {
        // The third check of the amended rule M1 (design, section 5.6) while the figure walks: the legs and arms
        // swing, the body sinks and rises, the hair moves. The pixels are read from frames in mid-step, where the
        // renderer itself says the figure stands, in the part of each zone that belongs to it in every frame of
        // the walk.
        var open = GardenView.OpenGround(3);
        var garden = GardenView.Garden();
        var rig = character == PlayerCharacter.Adam ? garden.Adam : garden.Woman;
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;
        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(open.X, open.Y, character));
        await GardenView.ExpectThreeDrawsAsync(game);
        // Out and back again, so the walk stays on the open ground: the front, both sides, the back and two diagonals.
        (string[] Keys, Facing Facing)[] ways =
        [
            (["ArrowRight"], Facing.E), (["ArrowLeft"], Facing.W), (["ArrowDown"], Facing.S), (["ArrowUp"], Facing.N),
            (["ArrowDown", "ArrowRight"], Facing.SE), (["ArrowUp", "ArrowLeft"], Facing.NW), (["ArrowDown", "ArrowLeft"], Facing.SW), (["ArrowUp", "ArrowRight"], Facing.NE),
        ];
        var read = 0;

        foreach (var (keys, facing) in ways)
        {
            foreach (var key in keys)
            {
                await page.Keyboard.DownAsync(key);
            }

            await Expect(game).ToHaveAttributeAsync("data-moving", "true");
            await Expect(game).ToHaveAttributeAsync("data-facing", facing.ToString());
            var why = $"{character} walking {facing} on {device}";
            for (var sample = 0; sample < 3; sample++)
            {
                var (figure, scale, moving, faces) = await GardenView.FigureInThisFrameAsync(page, character == PlayerCharacter.Adam ? 0 : 1);
                if (!moving || faces != facing.ToString())
                {
                    continue;
                }

                // A rectangle of the figure's own flat space (from the feet, y downward) as pixels of the box read.
                IEnumerable<int> PixelsOf((double Left, double Top, double Right, double Bottom) part, double inset)
                {
                    var (left, right) = ((int)Math.Ceiling((part.Left + inset + 20) * scale), (int)Math.Floor((part.Right - inset + 20) * scale));
                    var (top, bottom) = ((int)Math.Ceiling((part.Top + inset + 56) * scale), (int)Math.Floor((part.Bottom - inset + 56) * scale));
                    return Enumerable.Range(top, Math.Max(0, bottom - top)).SelectMany(y => Enumerable.Range(left, Math.Max(0, right - left)).Select(x => figure.At(x, y)));
                }

                var pelvic = PixelsOf(GardenView.ZoneBoundsInEveryFrameOfTheWalk(rig, RigStructure.Pelvis, facing), 2).ToList();
                pelvic.Count.ShouldBeGreaterThan(60, why);
                pelvic.Count(pixel => !GardenView.Near(pixel, GardenView.Skin, 3)).ShouldBe(0, $"{why}: the pelvic zone shows something other than the flat skin colour in mid-step");
                pelvic.Distinct().Count().ShouldBeLessThanOrEqualTo(2, $"{why}: the pelvic zone is one flat colour in mid-step");
                if (character == PlayerCharacter.Woman && !facing.IsTurnedAway())
                {
                    var chest = PixelsOf(GardenView.ZoneBoundsInEveryFrameOfTheWalk(rig, RigStructure.Chest, facing), 1).ToList();
                    chest.Count.ShouldBeGreaterThan(60, why);
                    chest.Count(pixel => GardenView.IsSkin(pixel, 24)).ShouldBe(0, $"{why}: the chest zone shows skin in mid-step");
                    chest.Count(pixel => !GardenView.Near(pixel, GardenView.Hair, 3)).ShouldBe(0, $"{why}: the chest zone shows something other than her hair in mid-step");
                }

                figure.Pixels.Count(pixel => GardenView.IsSkin(pixel, 3)).ShouldBeGreaterThan(100, $"{why}: the figure is in the box read");
                read++;
                await page.WaitForTimeoutAsync(70);
            }

            await Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
            foreach (var key in keys)
            {
                await page.Keyboard.UpAsync(key);
            }

            await Expect(game).ToHaveAttributeAsync("data-moving", "false", new() { Timeout = 15_000 });
            if (facing == Facing.S)
            {
                await GardenView.KeepScreenshotAsync(page, Shots, $"garden-{device}-{character}-after-walking");
            }
        }

        read.ShouldBeGreaterThanOrEqualTo(ways.Length, "frames in mid-step were read in the facings walked");
        await GardenView.SetAsideRefusedStyleSheetsAsync(guarded, 1);
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium", PlayerCharacter.Adam)]
    [TestCase("Pixel 7", "chromium", PlayerCharacter.Woman)]
    [TestCase("iPhone 13", "webkit", PlayerCharacter.Woman)]
    public async Task Walk_InAllEightFacingsWithTheFallbackRenderer_ShouldHaveTheVerdictOkOnEveryFrame(string device, string engine, PlayerCharacter character)
    {
        var open = GardenView.OpenGround(2);
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;
        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(open.X, open.Y, character), address: "garden?renderer=canvas");
        await Expect(game).ToHaveAttributeAsync("data-renderer", "canvas");
        var player = open;

        foreach (var step in EightFacings)
        {
            var target = new TilePos(player.X + step.X, player.Y + step.Y);
            await GardenView.TapTileAsync(page, HasTouch(device), player, target);
            player = target;

            await GardenView.ExpectStandingOnAsync(game, player.ToString());
            await Expect(game).ToHaveAttributeAsync("data-facing", step.Facing.ToString());
            await Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
        }

        await GardenView.KeepScreenshotAsync(page, Shots, $"fallback-{device}-{character}");
        await GardenView.SetAsideRefusedStyleSheetsAsync(guarded, 1);
        player.ShouldBe(open);
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium", false)]
    [TestCase("Desktop Chrome", "chromium", true)]
    [TestCase("Pixel 7", "chromium", false)]
    [TestCase("iPhone 13", "webkit", true)]
    public async Task Walk_EastInTheOpen_ShouldSlideTheFarLayerLessThanTheGroundAndNotAtAllUnderReducedMotion(string device, string engine, bool reducedMotion)
    {
        var open = GardenView.OpenGround(3);
        await using var guarded = await OpenPageAsync(device, engine, reducedMotion: reducedMotion);
        var page = guarded.Page;
        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(open.X - 2, open.Y));
        await GardenView.ExpectThreeDrawsAsync(game);
        var camera = await GardenView.CameraAsync(page, new TilePos(open.X - 2, open.Y));
        var before = await GardenView.ProbeAsync(page);

        await GardenView.TapTileAsync(page, HasTouch(device), new TilePos(open.X - 2, open.Y), new TilePos(open.X, open.Y));
        await GardenView.ExpectStandingOnAsync(game, open.ToString());
        var after = await GardenView.ProbeAsync(page);

        // Two tiles of travel: what the ground slides where the player stands, and where the haze closes.
        var ground = 64 * camera.Scale;
        var farthestGround = 64 * camera.FocalLength / camera.HazeEnd;
        var nearRidge = Math.Abs(after.Parallax[1] - before.Parallax[1]);
        var farRidge = Math.Abs(after.Parallax[0] - before.Parallax[0]);
        await Expect(game).ToHaveAttributeAsync("data-motion", reducedMotion ? "off" : "on");
        farthestGround.ShouldBeLessThan(ground);
        if (reducedMotion)
        {
            nearRidge.ShouldBe(0);
            farRidge.ShouldBe(0);
        }
        else
        {
            nearRidge.ShouldBeLessThan(farthestGround * 0.5, "the far layer slides less than the farthest ground");
            farRidge.ShouldBeLessThan(nearRidge * 0.75, "the far ridge slides less than the near ridge");
            farRidge.ShouldBeGreaterThan(1, "the far layer does slide");
        }

        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium", false)]
    [TestCase("Desktop Chrome", "chromium", true)]
    [TestCase("Pixel 7", "chromium", true)]
    [TestCase("iPhone 13", "webkit", true)]
    public async Task Stand_BesideTheStream_ShouldMoveTheWaterUnlessThePlayerAsksForReducedMotion(string device, string engine, bool reducedMotion)
    {
        // The stream is two tiles east of the player; nothing else is there.
        var map = GardenView.Garden().Map;
        var player = new TilePos(29, 20);
        var water = new TilePos(31, 20);
        map.KindAt(water).ShouldBe(TileKind.Water);
        await using var guarded = await OpenPageAsync(device, engine, reducedMotion: reducedMotion);
        var page = guarded.Page;
        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(player.X, player.Y));
        await GardenView.ExpectThreeDrawsAsync(game);
        var camera = await GardenView.CameraAsync(page, player);
        var (x, y) = camera.Project((water.X + 0.5) * 32, (water.Y + 0.5) * 32, -3);
        var side = 0.6 * 32 * camera.Scale;

        var first = await GardenView.PixelsAsync(page, x - (side / 2), y - (side / 2), side, side);
        await page.WaitForTimeoutAsync(700);
        var second = await GardenView.PixelsAsync(page, x - (side / 2), y - (side / 2), side, side);

        await Expect(game).ToHaveAttributeAsync("data-motion", reducedMotion ? "off" : "on");
        first.Pixels.Count(pixel => (pixel & 255) > ((pixel >> 16) & 255) + 20).ShouldBeGreaterThan(first.Pixels.Length / 2, "the rectangle read is water");
        if (reducedMotion)
        {
            second.Pixels.ShouldBe(first.Pixels);
        }
        else
        {
            second.Pixels.ShouldNotBe(first.Pixels);
        }

        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheTitleAndTheReader_ShouldAskForNoFileThatIsLoadedOnDemandAndTheGardenForTheRendererAndThreeJs(string device, string engine)
    {
        var spawn = GardenView.Garden().Map.Spawn("adam");
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;
        var asked = Asked(page);
        var onDemand = await OnDemandAsync(page);
        await page.GotoAsync(Site.BaseAddress + "404.html");
        await page.EvaluateAsync("entry => localStorage.setItem(entry[0], entry[1])", new[] { SaveCodec.SaveKey, GardenView.SaveAt(spawn.X, spawn.Y) });
        asked.Clear();

        await page.GotoAsync(Site.BaseAddress);
        await Expect(page.GetByTestId("character-select")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var title = asked.ToList();
        await page.GetByTestId("reader-link").ClickAsync();
        await Expect(page.GetByTestId("reader")).ToBeVisibleAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var reader = asked.Skip(title.Count).ToList();
        await page.GoBackAsync();
        await page.GetByTestId("continue").ClickAsync();
        var game = page.GetByTestId("game");
        await Expect(game).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await GardenView.ExpectThreeDrawsAsync(game);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var garden = asked.Skip(title.Count + reader.Count).ToList();

        onDemand.ShouldBe(["js/audio.js", "js/render-three.js", "js/render.js", "js/shell.js", "lib/three/LICENSE.txt", "lib/three/README.md", "lib/three/three.core.min.js", "lib/three/three.module.min.js"], ignoreOrder: true);
        title.ShouldContain(path => path.StartsWith("_framework/", StringComparison.Ordinal));
        title.Intersect(onDemand).ShouldBeEmpty("the title asks for no file that is loaded on demand");
        title.ShouldAllBe(path => !path.StartsWith("js/", StringComparison.Ordinal) && !path.StartsWith("lib/", StringComparison.Ordinal));
        reader.Intersect(onDemand).ShouldBeEmpty("the reader asks for no file that is loaded on demand");
        reader.ShouldAllBe(path => !path.StartsWith("js/", StringComparison.Ordinal) && !path.StartsWith("lib/", StringComparison.Ordinal));
        garden.Where(path => path.StartsWith("js/", StringComparison.Ordinal) || path.StartsWith("lib/", StringComparison.Ordinal)).ShouldBe(OnDemandOfThree, ignoreOrder: true);
        garden.Except(onDemand).ShouldBeEmpty("the garden asks for nothing but what is loaded on demand");
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheGardenWithRendererCanvasInItsAddress_ShouldDrawFlatWithTheFallbackSaySoAndNotAskForThreeJs(string device, string engine)
    {
        var open = GardenView.OpenGround(2);
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;
        var asked = Asked(page);

        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(open.X, open.Y), address: "garden?renderer=canvas");

        await Expect(game).ToHaveAttributeAsync("data-renderer", "canvas");
        await Expect(game).ToHaveAttributeAsync("data-renderer-fallback", RendererChoice.Asked);
        (await page.GetByTestId("game-canvas").EvaluateAsync<bool>("canvas => canvas.getContext('2d') !== null")).ShouldBeTrue();
        asked.Where(path => path.StartsWith("js/", StringComparison.Ordinal) || path.StartsWith("lib/", StringComparison.Ordinal)).ShouldBe(["js/audio.js", "js/render.js", "js/shell.js"], ignoreOrder: true);
        // Flat: a tile two rows up lies exactly two tiles of the one scale above the middle, which a perspective does not give.
        var (width, height) = await GardenView.PlayAreaAsync(page);
        var flat = Camera.Follow(width, height, (open.X + 0.5) * 32, (open.Y + 0.5) * 32, GardenView.Garden().Map);
        var (x, y) = await GardenView.ScreenOfAsync(page, open, new TilePos(open.X + 1, open.Y - 2));
        x.ShouldBe((width / 2) + (32 * flat.Scale), 1e-6);
        y.ShouldBe((height / 2) - (64 * flat.Scale), 1e-6);
        await GardenView.TapTileAsync(page, HasTouch(device), open, new TilePos(open.X + 1, open.Y - 2));
        await GardenView.ExpectStandingOnAsync(game, GardenView.Tile(open.X + 1, open.Y - 2));
        (await page.EvaluateAsync<int>("() => sessionStorage.length")).ShouldBe(0, "the choice is not kept");
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Open_TheGardenInABrowserWithoutWebGl_ShouldDrawWithTheFallbackSaySoAndNotAskForThreeJs(string device, string engine)
    {
        var spawn = GardenView.Garden().Map.Spawn("adam");
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;
        var asked = Asked(page);
        // A browser without WebGL: a canvas gives no WebGL context.
        await page.AddInitScriptAsync(
            """
            const getContext = HTMLCanvasElement.prototype.getContext;
            HTMLCanvasElement.prototype.getContext = function (kind, ...rest) {
                return String(kind).startsWith('webgl') ? null : getContext.call(this, kind, ...rest);
            };
            """);

        var game = await GardenView.OpenAsync(page);

        await Expect(game).ToHaveAttributeAsync("data-renderer", "canvas");
        await Expect(game).ToHaveAttributeAsync("data-renderer-fallback", RendererChoice.WebGlUnavailable);
        (await page.GetByTestId("game-canvas").EvaluateAsync<bool>("canvas => canvas.getContext('2d') !== null")).ShouldBeTrue();
        asked.ShouldContain("js/render-three.js");
        asked.ShouldContain("js/render.js");
        asked.ShouldAllBe(path => !path.StartsWith("lib/", StringComparison.Ordinal));
        await page.Keyboard.PressAsync("ArrowDown");
        await GardenView.ExpectStandingOnAsync(game, GardenView.Tile(spawn.X, spawn.Y + 1));
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Lose_TheWebGlContextAndGetItBack_ShouldGoOnDrawingWithThreeJs(string device, string engine)
    {
        var open = GardenView.OpenGround(2);
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;
        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(open.X, open.Y));
        await GardenView.ExpectThreeDrawsAsync(game);
        var (width, height) = await GardenView.PlayAreaAsync(page);

        await page.EvaluateAsync(
            """
            () => {
                const lose = document.getElementById('game-canvas').getContext('webgl2').getExtension('WEBGL_lose_context');
                lose.loseContext();
                setTimeout(() => lose.restoreContext(), 300);
            }
            """);

        await Expect(game).ToHaveAttributeAsync("data-renderer-context", "restored", new() { Timeout = 15_000 });
        await page.WaitForTimeoutAsync(2500);
        await GardenView.ExpectThreeDrawsAsync(game);
        (await GardenView.ProbeAsync(page)).ContextLost.ShouldBeFalse();
        var picture = await GardenView.PixelsAsync(page, 0, 0, width, height);
        picture.Pixels.Distinct().Count().ShouldBeGreaterThan(200, "the garden is drawn again");
        picture.Pixels.Count(pixel => GardenView.IsSkin(pixel, 3)).ShouldBeGreaterThan(50, "and so are the figures");
        await GardenView.TapTileAsync(page, HasTouch(device), open, new TilePos(open.X + 1, open.Y));
        await GardenView.ExpectStandingOnAsync(game, GardenView.Tile(open.X + 1, open.Y));
        guarded.Errors.RemoveAll(error => error.Contains("CONTEXT_LOST_WEBGL", StringComparison.Ordinal));
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Lose_TheWebGlContextForGood_ShouldGoOnWithTheFallbackOnTheSameTileAndSaySo(string device, string engine)
    {
        var open = GardenView.OpenGround(2);
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;
        var game = await GardenView.OpenAsync(page, GardenView.SaveAt(open.X, open.Y));
        await GardenView.ExpectThreeDrawsAsync(game);
        await GardenView.TapTileAsync(page, HasTouch(device), open, new TilePos(open.X + 1, open.Y));
        await GardenView.ExpectStandingOnAsync(game, GardenView.Tile(open.X + 1, open.Y));

        await page.EvaluateAsync("() => document.getElementById('game-canvas').getContext('webgl2').getExtension('WEBGL_lose_context').loseContext()");

        await Expect(game).ToHaveAttributeAsync("data-renderer-context", "lost");
        await Expect(game).ToHaveAttributeAsync("data-renderer", "canvas", new() { Timeout = 15_000 });
        await Expect(game).ToHaveAttributeAsync("data-renderer-fallback", RendererChoice.ContextLost);
        await Expect(game).ToHaveAttributeAsync("data-player-tile", GardenView.Tile(open.X + 1, open.Y));
        await Expect(game).ToHaveAttributeAsync("data-facing", "E");
        (await page.GetByTestId("game-canvas").EvaluateAsync<bool>("canvas => canvas.getContext('2d') !== null")).ShouldBeTrue();
        await GardenView.TapTileAsync(page, HasTouch(device), new TilePos(open.X + 1, open.Y), new TilePos(open.X + 1, open.Y + 1));
        await GardenView.ExpectStandingOnAsync(game, GardenView.Tile(open.X + 1, open.Y + 1));
        guarded.Errors.RemoveAll(error => error.Contains("CONTEXT_LOST_WEBGL", StringComparison.Ordinal));
        await GardenView.ExpectACleanRunAsync(guarded, game);
    }

    [TestCase("Desktop Chrome", "chromium")]
    [TestCase("Pixel 7", "chromium")]
    [TestCase("iPhone 13", "webkit")]
    public async Task Draw_AFrameOfTheGarden_ShouldStayWithinTheBoundOnDrawCallsTrianglesAndTheShadowMap(string device, string engine)
    {
        // The guard of the frame budget that needs no graphics chip: how much one frame asks of one. A change that
        // draws much more (a second shadow map, a mesh for each tree, a finer ground) fails here, on any machine.
        const int MostDrawCalls = 90;
        const int MostTriangles = 120_000;
        var lines = new List<string>();
        await using var guarded = await OpenPageAsync(device, engine);
        var page = guarded.Page;
        ILocator? game = null;

        // The glade with both figures; the meadows, where most trees stand; the spring at the north edge.
        foreach (var place in new[] { GardenView.Garden().Map.Spawn("adam"), new TilePos(10, 17), new TilePos(27, 4) })
        {
            game = await GardenView.OpenAsync(page, GardenView.SaveAt(place.X, place.Y, PlayerCharacter.Woman));
            await GardenView.ExpectThreeDrawsAsync(game);

            var probe = await GardenView.ProbeAsync(page);

            lines.Add(string.Create(CultureInfo.InvariantCulture, $"{device} at {place}: {probe.DrawCalls} draw calls, {probe.Triangles} triangles, shadow map {probe.ShadowMap}, pixel ratio {probe.PixelRatio:0.##}, antialiasing {(probe.Antialias ? "on" : "off")}, {probe.Trees} trees, {probe.Parts} figure parts"));
            probe.DrawCalls.ShouldBeInRange(10, MostDrawCalls);
            probe.Triangles.ShouldBeInRange(1000, MostTriangles);
            probe.ShadowMap.ShouldBe(1024);
            probe.PixelRatio.ShouldBeLessThanOrEqualTo(2);
            probe.Antialias.ShouldBe(probe.PixelRatio < 2);
        }

        var report = string.Join(Environment.NewLine, lines);
        TestContext.Out.WriteLine(report);
        var path = GardenView.KeptPath(Shots, $"draw-calls-{device}.txt");
        await File.WriteAllTextAsync(path, report + Environment.NewLine);
        TestContext.AddTestAttachment(path);
        await GardenView.ExpectACleanRunAsync(guarded, game!);
    }

    [Test]
    public async Task Walk_OnAPixel7WithTheProcessorSlowedFourTimes_ShouldKeepTheFallbackAt20MillisecondsOrLessAndReportTheFramesOfThreeJs()
    {
        // Row 17 of the map is open from the Pison meadows to the river: five seconds of walking east fit in it. The
        // canvas renderer, now the fallback, keeps its budget (a median of 20 ms). Nothing is required of the
        // Three.js number here: a headless browser of a build machine draws WebGL in software, on the processor,
        // which says nothing about a phone's graphics chip. Its guard is the bound on what a frame draws (the test
        // above); its number is reported, and has to be read on a real phone on the test site.
        var lines = new List<string>();
        foreach (var renderer in new[] { RendererKind.Canvas, RendererKind.Three })
        {
            await using var guarded = await OpenPageAsync("Pixel 7", "chromium");
            var page = guarded.Page;
            var game = await GardenView.OpenAsync(page, GardenView.SaveAt(3, 17), address: "garden?renderer=" + RendererChoice.NameOf(renderer));
            await Expect(game).ToHaveAttributeAsync("data-renderer", RendererChoice.NameOf(renderer));
            var session = await page.Context.NewCDPSessionAsync(page);
            await session.SendAsync("Emulation.setCPUThrottlingRate", new Dictionary<string, object> { ["rate"] = 4 });
            await page.GetByTestId("dpad-right").DispatchEventAsync("pointerdown");
            await Expect(game).ToHaveAttributeAsync("data-moving", "true", new() { Timeout = 30_000 });

            var frames = await FrameTimesAsync(page);

            var tile = await GardenView.PlayerTileAsync(game);
            await page.GetByTestId("dpad-right").DispatchEventAsync("pointerup");
            await session.SendAsync("Emulation.setCPUThrottlingRate", new Dictionary<string, object> { ["rate"] = 1 });
            var sorted = frames.Order().ToArray();
            sorted.Length.ShouldBeGreaterThan(0);
            var median = sorted[sorted.Length / 2];
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"{RendererChoice.NameOf(renderer),-6}: {frames.Length} frames in 5 s, median {median:0.0} ms, 95th percentile {sorted[(int)(sorted.Length * 0.95)]:0.0} ms, longest {sorted[^1]:0.0} ms"));
            if (renderer == RendererKind.Canvas)
            {
                median.ShouldBeLessThanOrEqualTo(20);
                frames.Length.ShouldBeGreaterThan(150);
                tile.X.ShouldBeGreaterThanOrEqualTo(3 + 15);
            }

            await GardenView.ExpectACleanRunAsync(guarded, game);
        }

        var report = $"Frame times, Pixel 7 profile, CPU slowed 4 times, WebGL by {GuardedPage.WebGlOfChromium}:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
        TestContext.Out.WriteLine(report);
        var path = GardenView.KeptPath(Shots, "frame-times.txt");
        await File.WriteAllTextAsync(path, report + Environment.NewLine);
        TestContext.AddTestAttachment(path);
    }
}
