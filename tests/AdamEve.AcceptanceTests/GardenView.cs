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
internal sealed record RendererProbe(int DrawCalls, int Triangles, int ShadowMap, double PixelRatio, bool Antialias, bool ContextLost, int Trees, int Parts, double[] Parallax, (double X, double Y)[] Screen)
{
    /// <summary>What stands on the tiles in view and is not a tree: shrubs, rocks, reeds.</summary>
    public int Plants { get; init; }

    /// <summary>The shafts of light, the glows (halos, mist, fireflies, pollen) and the wings of butterflies drawn.</summary>
    public int Shafts { get; init; }

    /// <inheritdoc cref="Shafts"/>
    public int Glows { get; init; }

    /// <inheritdoc cref="Shafts"/>
    public int Wings { get; init; }

    /// <summary>The clock of everything that moves by itself, in seconds: it stands under reduced motion.</summary>
    public double Seconds { get; init; }

    /// <summary>Where the feet of each character stood on the ground in that frame, in the order of their numbers.</summary>
    public (double X, double Y)[] Anchors { get; init; } = [];
}

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
        // The gallery and the review sheets are kept by their own names where the build keeps the results of these
        // tests (TestResults/acceptance/<folder>/<name>): an attachment alone is filed under the number of its test.
        var root = new DirectoryInfo(TestContext.CurrentContext.WorkDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AdamEve.slnx")))
        {
            root = root.Parent;
        }

        var directory = root is not null && folder is "gallery" or "m1-sheets"
            ? Path.Combine(root.FullName, "TestResults", "acceptance", folder)
            : Path.Combine(TestContext.CurrentContext.WorkDirectory, folder);
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

    /// <summary>
    /// Every pixel of the picture the renderer drew, as one number computed in the page from all of them, in the
    /// frame it was drawn in: two frames have the same number exactly when they are the same picture (but for a
    /// chance of one in four thousand million).
    /// </summary>
    public static Task<string> PictureAsync(IPage page) => page.EvaluateAsync<string>(
        """
        () => new Promise(resolve => requestAnimationFrame(() => {
            const canvas = document.getElementById('game-canvas');
            const copy = document.createElement('canvas');
            copy.width = canvas.width;
            copy.height = canvas.height;
            const context = copy.getContext('2d');
            context.drawImage(canvas, 0, 0);
            const data = new Uint32Array(context.getImageData(0, 0, copy.width, copy.height).data.buffer);
            let sum = 2166136261;
            for (let at = 0; at < data.length; at++) { sum = Math.imul(sum ^ data[at], 16777619); }
            resolve(`${copy.width}x${copy.height}:${sum >>> 0}`);
        }))
        """);

    public static bool Near(int pixel, int colour, int within) =>
        Math.Abs((pixel >> 16) - (colour >> 16)) <= within && Math.Abs(((pixel >> 8) & 255) - ((colour >> 8) & 255)) <= within && Math.Abs((pixel & 255) - (colour & 255)) <= within;

    public static bool IsSkin(int pixel, int within) => Near(pixel, Skin, within) || Near(pixel, SkinInShade, within);

    /// <summary>
    /// Whether a pixel has a colour of the hair: one of the tones the rule allows hair (<see cref="RigStructure.AllowedColours"/>:
    /// the hair, a lock that catches the light, the sheen), or, where two locks meet and the renderer smooths
    /// the edge between them, a colour between two of those tones. Nothing else: no skin, and no mix with skin.
    /// </summary>
    public static bool IsHair(int pixel, int within)
    {
        var tones = RigStructure.AllowedColours(PartRole.Hair);
        foreach (var from in tones)
        {
            foreach (var to in tones)
            {
                for (var step = 0; step <= 32; step++)
                {
                    int Mixed(int shift) => (int)Math.Round((((from >> shift) & 255) * (32 - step) / 32.0) + (((to >> shift) & 255) * step / 32.0));
                    if (Near(pixel, (Mixed(16) << 16) | (Mixed(8) << 8) | Mixed(0), within))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

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
            [.. answer.GetProperty("screen").EnumerateArray().Select(point => (point[0].GetDouble(), point[1].GetDouble()))])
        {
            Plants = answer.GetProperty("plants").GetInt32(),
            Shafts = answer.GetProperty("shafts").GetInt32(),
            Glows = answer.GetProperty("glows").GetInt32(),
            Wings = answer.GetProperty("wings").GetInt32(),
            Seconds = answer.GetProperty("seconds").GetDouble(),
            Anchors = [.. answer.GetProperty("anchors").EnumerateArray().Select(point => (point[0].GetDouble(), point[1].GetDouble()))],
        };
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

    /// <summary>
    /// The part of a zone of a rig that belongs to the zone in every frame of the walk in one facing, in the
    /// figure's own flat space: what all the frames at 60 Hz have in common. A pixel read there lies in the zone
    /// whichever frame of the step the renderer drew.
    /// </summary>
    public static (double Left, double Top, double Right, double Bottom) ZoneBoundsInEveryFrameOfTheWalk(Rig rig, string zoneId, Facing facing)
    {
        var walk = Garden().Animations.Single(animation => animation.Id == "walk");
        var pose = new RigPose(rig);
        var index = rig.ZoneIndex(zoneId);
        var zone = rig.Zones[index];
        double left = double.NegativeInfinity, top = double.NegativeInfinity, right = double.PositiveInfinity, bottom = double.PositiveInfinity;
        for (var frame = 0; frame <= Math.Ceiling(walk.Duration * 60); frame++)
        {
            pose.Sample(walk, frame / 60.0, facing, Covering.None);
            var transform = pose.ZoneTransform(index);
            var xs = Enumerable.Range(0, zone.Points.Count / 2).Select(point => transform.ApplyX(zone.Points[point * 2], zone.Points[(point * 2) + 1])).ToList();
            var ys = Enumerable.Range(0, zone.Points.Count / 2).Select(point => transform.ApplyY(zone.Points[point * 2], zone.Points[(point * 2) + 1])).ToList();
            // A zone that leans with the body is taken by the box inside its corners.
            xs.Sort();
            ys.Sort();
            left = Math.Max(left, xs[(xs.Count / 2) - 1]);
            right = Math.Min(right, xs[xs.Count / 2]);
            top = Math.Max(top, ys[(ys.Count / 2) - 1]);
            bottom = Math.Min(bottom, ys[ys.Count / 2]);
        }

        return (left, top, right, bottom);
    }

    /// <summary>
    /// The figure of a character as the renderer drew it in one frame, read in that very frame: where its feet are
    /// on the screen, the one scale it is drawn at, and the pixels of the box around it (40 logical pixels wide, from
    /// 56 above the feet to 2 below). The character may be in mid-step: the renderer itself says where it stands.
    /// </summary>
    public static async Task<(PixelBox Pixels, double Scale, bool Moving, string Facing)> FigureInThisFrameAsync(IPage page, int character)
    {
        var answer = await page.EvaluateAsync<JsonElement>(
            """
            ask => import(new URL('js/render-three.js', document.baseURI).href).then(module => new Promise(resolve => requestAnimationFrame(() => {
                const root = document.getElementById('game');
                const [x, y] = module.probe([]).anchors[ask[0]];
                // The feet, and a point of the figure's plane 40 above them: the plane leans back by the tilt.
                const [feet, above] = module.probe([[x, 0, y], [x, 40 * Math.cos(ask[1]), y - 40 * Math.sin(ask[1])]]).screen;
                const scale = (feet[1] - above[1]) / 40;
                const canvas = document.getElementById('game-canvas');
                const ratio = canvas.width / canvas.clientWidth;
                const left = Math.round((feet[0] - 20 * scale) * ratio), top = Math.round((feet[1] - 56 * scale) * ratio);
                const w = Math.round(40 * scale * ratio), h = Math.round(58 * scale * ratio);
                const copy = document.createElement('canvas');
                copy.width = w;
                copy.height = h;
                const context = copy.getContext('2d');
                context.drawImage(canvas, left, top, w, h, 0, 0, w, h);
                const data = context.getImageData(0, 0, w, h).data;
                const pixels = [];
                for (let at = 0; at < data.length; at += 4) { pixels.push((data[at] << 16) | (data[at + 1] << 8) | data[at + 2]); }
                resolve({ scale: scale * ratio, width: w, height: h, moving: root.dataset.moving === 'true', facing: root.dataset.facing, pixels });
            })))
            """,
            new[] { character, PerspectiveCamera.TiltRadians });
        var box = new PixelBox(answer.GetProperty("width").GetInt32(), answer.GetProperty("height").GetInt32(), [.. answer.GetProperty("pixels").EnumerateArray().Select(pixel => pixel.GetInt32())]);
        return (box, answer.GetProperty("scale").GetDouble(), answer.GetProperty("moving").GetBoolean(), answer.GetProperty("facing").GetString() ?? string.Empty);
    }

    /// <summary>
    /// The bounds of every part of a figure in the figure's own flat space, in a frame of the idle stance: of the
    /// outline of each part as it is drawn (an ellipse that is turned reaches less far than the box it lies in, and
    /// so does a rounded corner).
    /// </summary>
    public static (double Left, double Top, double Right, double Bottom) FigureBounds(Rig rig, Facing facing)
    {
        var pose = new RigPose(rig);
        pose.Sample(Garden().Animations.Single(animation => animation.Id == "idle"), 0, facing, Covering.None);
        double left = double.PositiveInfinity, top = double.PositiveInfinity, right = double.NegativeInfinity, bottom = double.NegativeInfinity;
        foreach (var placed in pose.Parts)
        {
            var part = rig.Parts[placed.PartIndex];
            var transform = placed.Transform;
            double halfWidth = part.Width / 2, halfHeight = part.Height / 2;
            // How far the outline reaches from its middle, across and up and down. A rounded rectangle is its
            // inner rectangle grown by the radius of its corners; a plain one has no radius.
            var round = part.Shape == PartShape.Rounded ? part.Round : 0;
            var reachX = part.Shape == PartShape.Ellipse
                ? Math.Sqrt((transform.A * halfWidth * transform.A * halfWidth) + (transform.C * halfHeight * transform.C * halfHeight))
                : (Math.Abs(transform.A) * (halfWidth - round)) + (Math.Abs(transform.C) * (halfHeight - round)) + round;
            var reachY = part.Shape == PartShape.Ellipse
                ? Math.Sqrt((transform.B * halfWidth * transform.B * halfWidth) + (transform.D * halfHeight * transform.D * halfHeight))
                : (Math.Abs(transform.B) * (halfWidth - round)) + (Math.Abs(transform.D) * (halfHeight - round)) + round;
            left = Math.Min(left, transform.E - reachX);
            right = Math.Max(right, transform.E + reachX);
            top = Math.Min(top, transform.F - reachY);
            bottom = Math.Max(bottom, transform.F + reachY);
        }

        return (left, top, right, bottom);
    }
}
