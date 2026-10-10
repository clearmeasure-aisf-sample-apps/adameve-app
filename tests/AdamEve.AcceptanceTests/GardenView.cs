using System.Text.Json;
using AdamEve.Content;
using AdamEve.Content.Garden;
using AdamEve.Core.Rigs;
using AdamEve.Core.Saves;
using AdamEve.Core.World;
using Microsoft.Playwright;

namespace AdamEve.AcceptanceTests;

/// <summary>Pixels the renderer drew in a rectangle of the play area, as 0xRRGGBB, row by row.</summary>
internal sealed record PixelBox(int Width, int Height, int[] Pixels)
{
    public int At(int x, int y) => Pixels[(y * Width) + x];
}

/// <summary>What the renderer says of its last frame (the export <c>probe</c> of <c>js/render-three.js</c>).</summary>
internal sealed record RendererProbe(int DrawCalls, int Triangles, int ShadowMap, double PixelRatio, bool Antialias, bool ContextLost, int Trees, int Parts, double[] Parallax, (double X, double Y)[] Screen);

/// <summary>
/// What the tests of the garden share: opening it from a saved game, the camera of the game for the play area of
/// the page (the tests ask the projection model of Core where a tile lies on the screen, as the game does), a tap
/// on a tile, and the pixels the renderer drew.
/// </summary>
internal static class GardenView
{
    public const int Skin = RigStructure.Skin;
    public const int SkinInShade = RigStructure.SkinInShade;
    public const int Hair = RigStructure.Hair;
    public const double FeetBelowTheMiddleOfTheTile = 8;

    public static GardenContent Garden() => GameContent.LoadEmbedded().Content?.Garden
        ?? throw new InvalidOperationException("The content of the game did not load.");

    public static string Tile(int x, int y) => new TilePos(x, y).ToString();

    public static string SaveAt(int x, int y, PlayerCharacter character = PlayerCharacter.Adam, Facing facing = Facing.S) =>
        SaveCodec.Write(new SaveGame { Character = character, TileX = x, TileY = y, Facing = facing });

    /// <summary>
    /// A tile of the glade with open ground this many tiles around it, away from where the other character stands
    /// and from the edge of the map.
    /// </summary>
    public static TilePos OpenGround(int around = 1)
    {
        var map = Garden().Map;
        var glade = map.Regions.Single(region => region.Id == "central-glade");
        for (var y = glade.Y + around + 1; y < glade.Y + glade.Height - around - 1; y++)
        {
            for (var x = glade.X + glade.Width - around - 2; x >= glade.X + around + 1; x--)
            {
                var near = Enumerable.Range(-around, (2 * around) + 1).SelectMany(dy => Enumerable.Range(-around, (2 * around) + 1).Select(dx => new TilePos(x + dx, y + dy)));
                var far = new[] { map.Spawn("adam"), map.Spawn("woman") }.All(spawn => Math.Abs(spawn.X - x) > around + 3 || Math.Abs(spawn.Y - y) > around + 3);
                if (far && near.All(map.IsWalkable))
                {
                    return new TilePos(x, y);
                }
            }
        }

        throw new InvalidOperationException("The glade has no open ground of that size.");
    }

    /// <summary>Opens the garden, from a saved game when one is given, and waits for its first frames.</summary>
    public static async Task<ILocator> OpenAsync(IPage page, string? save = null, string? settings = null, string address = "garden")
    {
        if (save is not null || settings is not null)
        {
            // A page of the site that starts no runtime: the storage of the origin is written before the game loads.
            await page.GotoAsync(Site.BaseAddress + "404.html");
            await page.EvaluateAsync(
                "entries => { for (const [key, value] of entries) { if (value !== null) { localStorage.setItem(key, value); } } }",
                new[] { new[] { SaveCodec.SaveKey, save }, new[] { SaveCodec.SettingsKey, settings } });
        }

        await page.GotoAsync(Site.BaseAddress + address);
        var game = page.GetByTestId("game");
        await Assertions.Expect(game).ToHaveAttributeAsync("data-ready", "true", new() { Timeout = 30_000 });
        await Assertions.Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
        return game;
    }

    public static async Task ExpectThreeDrawsAsync(ILocator game)
    {
        await Assertions.Expect(game).ToHaveAttributeAsync("data-renderer", "three");
        (await game.GetAttributeAsync("data-renderer-fallback")).ShouldBeNull();
    }

    public static async Task ExpectStandingOnAsync(ILocator game, string tile)
    {
        await Assertions.Expect(game).ToHaveAttributeAsync("data-player-tile", tile, new() { Timeout = 15_000 });
        await Assertions.Expect(game).ToHaveAttributeAsync("data-moving", "false");
    }

    public static async Task<TilePos> PlayerTileAsync(ILocator game)
    {
        var parts = (await game.GetAttributeAsync("data-player-tile") ?? throw new InvalidOperationException("The game root names no tile.")).Split(',');
        return new TilePos(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    public static async Task<(double Width, double Height)> PlayAreaAsync(IPage page)
    {
        var box = await page.GetByTestId("game-canvas").BoundingBoxAsync() ?? throw new InvalidOperationException("The canvas has no box.");
        return (box.Width, box.Height);
    }

    /// <summary>The camera of the game for the play area of the page, with the player standing on a tile.</summary>
    public static async Task<PerspectiveCamera> CameraAsync(IPage page, TilePos player)
    {
        var (width, height) = await PlayAreaAsync(page);
        var map = Garden().Map;
        return PerspectiveCamera.Follow(width, height, (player.X + 0.5) * map.TileSize, (player.Y + 0.5) * map.TileSize, map);
    }

    /// <summary>Where the middle of a tile lies on the play area, through the camera of the renderer that draws.</summary>
    public static async Task<(double X, double Y)> ScreenOfAsync(IPage page, TilePos player, TilePos tile)
    {
        var map = Garden().Map;
        var (width, height) = await PlayAreaAsync(page);
        var x = (tile.X + 0.5) * map.TileSize;
        var y = (tile.Y + 0.5) * map.TileSize;
        if (await page.GetByTestId("game").GetAttributeAsync("data-renderer") == "three")
        {
            return PerspectiveCamera.Follow(width, height, (player.X + 0.5) * map.TileSize, (player.Y + 0.5) * map.TileSize, map).Project(x, y);
        }

        var flat = Camera.Follow(width, height, (player.X + 0.5) * map.TileSize, (player.Y + 0.5) * map.TileSize, map);
        return ((x - flat.X) * flat.Scale, (y - flat.Y) * flat.Scale);
    }

    /// <summary>A tap (or a click) on the middle of a tile, where the camera of the game shows it.</summary>
    public static async Task TapTileAsync(IPage page, bool touch, TilePos player, TilePos tile)
    {
        var (width, height) = await PlayAreaAsync(page);
        var (x, y) = await ScreenOfAsync(page, player, tile);
        x.ShouldBeInRange(1, width - 1, $"the tile {tile} is not on the play area");
        y.ShouldBeInRange(1, height - 1, $"the tile {tile} is not on the play area");
        var canvas = page.GetByTestId("game-canvas");
        var position = new Position { X = (float)x, Y = (float)y };
        if (touch)
        {
            await canvas.TapAsync(new() { Position = position });
        }
        else
        {
            await canvas.ClickAsync(new() { Position = position });
        }
    }

    /// <summary>What may not happen in any test of the garden.</summary>
    public static async Task ExpectACleanRunAsync(GuardedPage guarded, ILocator game)
    {
        await Assertions.Expect(game).ToHaveAttributeAsync("data-concealment", "ok");
        await Assertions.Expect(game).ToHaveAttributeAsync("data-concealment-failures", "0");
        guarded.Errors.ShouldBeEmpty();
        guarded.RequestsOutsideTheOrigin.ShouldBeEmpty();
        (await guarded.Page.EvaluateAsync<string>("() => document.cookie")).ShouldBeEmpty();
        var keys = await guarded.Page.EvaluateAsync<string[]>("() => Object.keys(localStorage)");
        keys.ShouldBeSubsetOf([SaveCodec.SaveKey, SaveCodec.SettingsKey, SaveCodec.CorruptKey]);
        (await guarded.Page.EvaluateAsync<int>("() => sessionStorage.length")).ShouldBe(0);
    }

    // For a screenshot Playwright adds a style sheet of its own to the page. On WebKit the content security policy of
    // the site refuses it (style-src 'self') and the browser says so, once for each screenshot. That is the policy at
    // work on the test's tool, not an error of the game: at most one such line for each screenshot is set aside, and
    // every other error still fails the test.
    public static async Task SetAsideRefusedStyleSheetsAsync(GuardedPage guarded, int screenshots)
    {
        const string RefusedStyleSheet = "Refused to apply a stylesheet because its hash, its nonce, or 'unsafe-inline' does not appear in the style-src directive of the Content Security Policy.";
        await guarded.Page.WaitForTimeoutAsync(250);
        guarded.Errors.Count(error => error == RefusedStyleSheet).ShouldBeLessThanOrEqualTo(screenshots);
        guarded.Errors.RemoveAll(error => error == RefusedStyleSheet);
    }

    /// <summary>A file kept with the test results for review. Never compared.</summary>
    public static string KeptPath(string folder, string name)
    {
        var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, folder);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, name.Replace(' ', '-'));
    }

    /// <summary>A screenshot of the page, kept with the test results for review. Never compared.</summary>
    public static async Task KeepScreenshotAsync(IPage page, string folder, string name)
    {
        var path = KeptPath(folder, name + ".png");
        await page.ScreenshotAsync(new() { Path = path, Caret = ScreenshotCaret.Initial, Animations = ScreenshotAnimations.Allow });
        TestContext.AddTestAttachment(path);
    }

    /// <summary>
    /// The pixels the renderer drew in a rectangle of the play area (CSS pixels from its left and top edges): read
    /// from the canvas itself, in the frame it was drawn in.
    /// </summary>
    public static async Task<PixelBox> PixelsAsync(IPage page, double left, double top, double width, double height)
    {
        var numbers = await page.EvaluateAsync<int[]>(
            """
            box => new Promise(resolve => requestAnimationFrame(() => {
                const canvas = document.getElementById('game-canvas');
                const ratio = canvas.width / canvas.clientWidth;
                const x = Math.round(box[0] * ratio), y = Math.round(box[1] * ratio);
                const w = Math.max(1, Math.round(box[2] * ratio)), h = Math.max(1, Math.round(box[3] * ratio));
                const copy = document.createElement('canvas');
                copy.width = w;
                copy.height = h;
                const context = copy.getContext('2d');
                context.drawImage(canvas, x, y, w, h, 0, 0, w, h);
                const data = context.getImageData(0, 0, w, h).data;
                const pixels = [w, h];
                for (let at = 0; at < data.length; at += 4) { pixels.push((data[at] << 16) | (data[at + 1] << 8) | data[at + 2]); }
                resolve(pixels);
            }))
            """,
            new[] { left, top, width, height });
        return new PixelBox(numbers[0], numbers[1], numbers[2..]);
    }

    public static bool Near(int pixel, int colour, int within) =>
        Math.Abs((pixel >> 16) - (colour >> 16)) <= within && Math.Abs(((pixel >> 8) & 255) - ((colour >> 8) & 255)) <= within && Math.Abs((pixel & 255) - (colour & 255)) <= within;

    public static bool IsSkin(int pixel, int within) => Near(pixel, Skin, within) || Near(pixel, SkinInShade, within);

    /// <summary>
    /// Asks the renderer what it drew in its last frame and where its own camera puts points of the garden on the
    /// screen (each point: x on the ground, height, y on the ground).
    /// </summary>
    public static async Task<RendererProbe> ProbeAsync(IPage page, params (double X, double Height, double Y)[] points)
    {
        var answer = await page.EvaluateAsync<JsonElement>(
            "points => import(new URL('js/render-three.js', document.baseURI).href).then(module => new Promise(resolve => requestAnimationFrame(() => resolve(module.probe(points)))))",
            points.Select(point => new[] { point.X, point.Height, point.Y }).ToArray());
        answer.ValueKind.ShouldBe(JsonValueKind.Object, "the Three.js renderer does not draw");
        return new RendererProbe(
            answer.GetProperty("drawCalls").GetInt32(),
            answer.GetProperty("triangles").GetInt32(),
            answer.GetProperty("shadowMap").GetInt32(),
            answer.GetProperty("pixelRatio").GetDouble(),
            answer.GetProperty("antialias").GetBoolean(),
            answer.GetProperty("contextLost").GetBoolean(),
            answer.GetProperty("trees").GetInt32(),
            answer.GetProperty("parts").GetInt32(),
            [.. answer.GetProperty("parallax").EnumerateArray().Select(value => value.GetDouble())],
            [.. answer.GetProperty("screen").EnumerateArray().Select(point => (point[0].GetDouble(), point[1].GetDouble()))]);
    }

    /// <summary>
    /// A rectangle of a figure in the figure's own flat space (logical pixels from the feet, y downward), as it lies
    /// on the play area when the feet stand on a point of the ground: by the one scale the camera gives a figure
    /// there.
    /// </summary>
    public static (double Left, double Top, double Width, double Height) OnScreen(PerspectiveCamera camera, (double X, double Y) feet, double left, double top, double right, double bottom)
    {
        var scale = camera.ScaleAt(feet.X, feet.Y);
        var anchor = camera.Project(feet.X, feet.Y);
        return (anchor.X + (scale * left), anchor.Y + (scale * top), scale * (right - left), scale * (bottom - top));
    }

    /// <summary>Where the feet of the player are on the ground when the player stands on a tile.</summary>
    public static (double X, double Y) FeetOn(TilePos tile)
    {
        var size = Garden().Map.TileSize;
        return ((tile.X + 0.5) * size, ((tile.Y + 0.5) * size) + FeetBelowTheMiddleOfTheTile);
    }

    /// <summary>The bounds of a zone of a rig in the figure's own flat space, in a frame of the idle stance.</summary>
    public static (double Left, double Top, double Right, double Bottom) ZoneBounds(Rig rig, string zoneId, Facing facing)
    {
        var pose = new RigPose(rig);
        pose.Sample(Garden().Animations.Single(animation => animation.Id == "idle"), 0, facing, Covering.None);
        var index = rig.ZoneIndex(zoneId);
        var zone = rig.Zones[index];
        var transform = pose.ZoneTransform(index);
        var xs = Enumerable.Range(0, zone.Points.Count / 2).Select(point => transform.ApplyX(zone.Points[point * 2], zone.Points[(point * 2) + 1])).ToList();
        var ys = Enumerable.Range(0, zone.Points.Count / 2).Select(point => transform.ApplyY(zone.Points[point * 2], zone.Points[(point * 2) + 1])).ToList();
        return (xs.Min(), ys.Min(), xs.Max(), ys.Max());
    }

    /// <summary>The bounds of every part of a figure in the figure's own flat space, in a frame of the idle stance.</summary>
    public static (double Left, double Top, double Right, double Bottom) FigureBounds(Rig rig, Facing facing)
    {
        var pose = new RigPose(rig);
        pose.Sample(Garden().Animations.Single(animation => animation.Id == "idle"), 0, facing, Covering.None);
        double left = double.PositiveInfinity, top = double.PositiveInfinity, right = double.NegativeInfinity, bottom = double.NegativeInfinity;
        foreach (var placed in pose.Parts)
        {
            var part = rig.Parts[placed.PartIndex];
            foreach (var (x, y) in new[] { (-0.5, -0.5), (0.5, -0.5), (0.5, 0.5), (-0.5, 0.5) })
            {
                var flatX = placed.Transform.ApplyX(x * part.Width, y * part.Height);
                var flatY = placed.Transform.ApplyY(x * part.Width, y * part.Height);
                left = Math.Min(left, flatX);
                right = Math.Max(right, flatX);
                top = Math.Min(top, flatY);
                bottom = Math.Max(bottom, flatY);
            }
        }

        return (left, top, right, bottom);
    }
}
