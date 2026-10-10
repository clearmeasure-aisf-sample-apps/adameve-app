using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AdamEve.Core.Game;
using AdamEve.Core.Input;
using AdamEve.Core.World;
using AdamEve.Host;

namespace AdamEve.UnitTests;

/// <summary>
/// The vendored Three.js and the modules that draw the garden with it (design, section 7.1 and decision D18): the
/// files are the ones the folder's README records, the host does not let a browser keep them for a year, the
/// renderer asks no other origin, takes its camera from the game, and keeps the lines rule M1 rests on.
/// </summary>
[TestFixture]
public partial class ThreeVendorTests
{
    private static string ClientRoot() => Path.Combine(CanonicalFile.RepositoryRoot(), "src", "AdamEve.Client", "wwwroot");

    private static string Folder() => Path.Combine(ClientRoot(), "lib", "three");

    private static string Module() => File.ReadAllText(Path.Combine(ClientRoot(), "js", "render-three.js"));

    private static string Shell() => File.ReadAllText(Path.Combine(ClientRoot(), "js", "shell.js"));

    private static string CanvasModule() => File.ReadAllText(Path.Combine(ClientRoot(), "js", "render.js"));

    private static Dictionary<string, string> Recorded() => Row().Matches(File.ReadAllText(Path.Combine(Folder(), "README.md")))
        .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);

    [Test]
    public void Readme_EveryVendoredFile_ShouldBeRecordedWithItsSha256()
    {
        var recorded = Recorded();

        var files = Directory.GetFiles(Folder()).Select(Path.GetFileName).Where(name => name != "README.md").ToList();

        files.ShouldBe(["LICENSE.txt", "three.core.min.js", "three.module.min.js"], ignoreOrder: true);
        recorded.Keys.ShouldBe(files, ignoreOrder: true);
        foreach (var name in files)
        {
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Folder(), name!)))).ToLowerInvariant().ShouldBe(recorded[name!], name);
        }
    }

    [Test]
    public void Readme_TheVersion_ShouldBeTheOneTheNoticeNames()
    {
        var version = Version().Match(File.ReadAllText(Path.Combine(Folder(), "README.md"))).Groups[1].Value;

        version.ShouldNotBeEmpty();
        File.ReadAllText(Path.Combine(CanonicalFile.RepositoryRoot(), "NOTICE")).ShouldContain($"Three.js {version}");
        File.ReadAllText(Path.Combine(Folder(), "README.md")).ShouldContain($"three-{version}.tgz");
    }

    [Test]
    public void Module_TheBuild_ShouldImportOnlyTheCoreBesideIt()
    {
        var imports = From().Matches(File.ReadAllText(Path.Combine(Folder(), "three.module.min.js"))).Select(match => match.Groups[1].Value).Distinct();

        imports.ShouldBe(["./three.core.min.js"]);
        From().Matches(File.ReadAllText(Path.Combine(Folder(), "three.core.min.js"))).ShouldBeEmpty();
    }

    [TestCase("/lib/three/three.module.min.js")]
    [TestCase("/lib/three/three.core.min.js")]
    [TestCase("/lib/three/LICENSE.txt")]
    [TestCase("/js/render-three.js")]
    [TestCase("/js/render.js")]
    [TestCase("/js/shell.js")]
    public void CacheControl_AVendoredFileWithoutAFingerprint_ShouldBeAskedForAgainEachTime(string path)
    {
        SitePaths.CacheControl(path, 200).ShouldBe("no-cache");
        SitePaths.IsNavigation(path).ShouldBeFalse();
    }

    [Test]
    public void RenderThree_TheModule_ShouldAskForThreeOnlyWhenItAttachesAndForNothingElse()
    {
        var module = Module();

        StaticImport().Matches(module).Select(match => match.Groups[1].Value).ShouldBe(["./shell.js"]);
        DynamicImport().Matches(module).Select(match => match.Groups[1].Value).ShouldBe(["../lib/three/three.module.min.js"]);
        foreach (var script in new[] { module, Shell(), CanvasModule() })
        {
            script.ShouldNotContain("http://");
            script.ShouldNotContain("https://");
            script.ShouldNotContain("fetch(");
            script.ShouldNotContain("eval(");
            script.ShouldNotContain("OrbitControls");
            script.ShouldNotContain("innerHTML");
            script.ShouldNotContain(".style");
        }

        File.ReadAllText(Path.Combine(ClientRoot(), "index.html")).ShouldNotContain("three");
        File.ReadAllText(Path.Combine(ClientRoot(), "index.html")).ShouldNotContain("js/");
    }

    [Test]
    public void Shell_TheSharedModule_ShouldBeTheOnlyHomeOfTheInputAdaptersAndImportNothing()
    {
        // The trial had copied the input code of render.js into the second renderer. There is one copy now.
        StaticImport().Matches(Shell()).ShouldBeEmpty();
        DynamicImport().Matches(Shell()).ShouldBeEmpty();
        StaticImport().Matches(CanvasModule()).Select(match => match.Groups[1].Value).ShouldBe(["./shell.js"]);
        DynamicImport().Matches(CanvasModule()).ShouldBeEmpty();
        foreach (var once in new[] { "\"keydown\"", "\"pointerdown\"", "\"touchstart\"", "ArrowUp", "inputView.set(", "_unsafe_create_view", "dataset[name]", "getDotnetRuntime" })
        {
            Shell().ShouldContain(once);
            Module().ShouldNotContain(once);
            CanvasModule().ShouldNotContain(once);
        }
    }

    [Test]
    public void RenderThree_TheKindsItGivesHeight_ShouldHaveTheNumbersOfTileKind()
    {
        var kinds = Kinds().Match(Module()).Groups[1].Value;

        var numbers = Pair().Matches(kinds).ToDictionary(match => match.Groups[1].Value, match => int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));

        numbers.ShouldBe(
            new Dictionary<string, int>
            {
                ["water"] = (int)TileKind.Water,
                ["thicket"] = (int)TileKind.Thicket,
                ["crossing"] = (int)TileKind.Crossing,
                ["restingPlace"] = (int)TileKind.RestingPlace,
                ["flowers"] = (int)TileKind.Flowers,
            },
            ignoreOrder: true);
    }

    [Test]
    public void Shell_TheLayoutOfTheRenderListAndOfTheInputBlock_ShouldBeTheOneOfTheGame()
    {
        var shell = Shell();

        shell.ShouldContain($"export const HEADER = {RenderList.HeaderLength};");
        shell.ShouldContain($"export const ENTRY = {RenderList.EntryLength};");
        shell.ShouldContain($"export const CHARACTER = {RenderList.Character};");
        shell.ShouldContain($"export const ANCHORS = {RenderList.Anchors};");
        shell.ShouldContain($"export const ANCHOR_COUNT = {RenderList.AnchorCount};");
        shell.ShouldContain($"export const PERSPECTIVE = {InputBlock.Perspective};");
        shell.ShouldContain($"export const FLAT = {InputBlock.Flat};");
        shell.ShouldContain($"input[{InputBlock.Projection}] = shell.projection;");
        shell.ShouldContain($"input[{InputBlock.TapX}] = shell.tapX;");
        shell.ShouldContain($"input[{InputBlock.TapY}] = shell.tapY;");
        shell.ShouldContain($"input[{InputBlock.ViewWidth}] = shell.viewWidth;");
        shell.ShouldContain($"input[{InputBlock.ViewHeight}] = shell.viewHeight;");
        shell.ShouldContain($"input[{InputBlock.PixelRatio}] = shell.ratio;");
        shell.ShouldContain("shell.ratio = Math.min(2, window.devicePixelRatio || 1);");
        Module().ShouldContain("projection: PERSPECTIVE");
        CanvasModule().ShouldContain("projection: FLAT");
    }

    [Test]
    public void RenderThree_TheCamera_ShouldBeTheCameraOfTheGameAndNothingElseShouldMoveIt()
    {
        // The projection is a rule of the game (AdamEve.Core.World.PerspectiveCamera): the renderer reads the eye,
        // the tilt and the field of view from the render list and has no number of its own for any of them.
        var module = Module();

        module.ShouldContain($"const EYE_X = {RenderList.EyeX};");
        module.ShouldContain($"const EYE_HEIGHT = {RenderList.EyeHeight};");
        module.ShouldContain($"const EYE_Y = {RenderList.EyeY};");
        module.ShouldContain($"const TILT = {RenderList.Tilt};");
        module.ShouldContain($"const FIELD_OF_VIEW = {RenderList.FieldOfView};");
        module.ShouldContain($"const HAZE_START = {RenderList.HazeStart};");
        module.ShouldContain($"const HAZE_END = {RenderList.HazeEnd};");
        module.ShouldContain($"const FIGURE_DEPTH_HEIGHT = {RenderList.FigureDepthHeight};");
        module.ShouldContain("new THREE.PerspectiveCamera(");
        module.ShouldNotContain("OrthographicCamera");
        module.ShouldContain("const tilt = list[TILT];");
        module.ShouldContain("const view = list[FIELD_OF_VIEW];");
        module.ShouldContain("camera.position.set(list[EYE_X], list[EYE_HEIGHT], list[EYE_Y]);");
        module.ShouldContain("camera.rotation.set(-tilt, 0, 0);");
        module.ShouldContain("g.scene.fog.near = hazeStart;");
        module.ShouldContain("g.scene.fog.far = hazeEnd;");
        Regex.Count(module, @"camera\.position\.set\(").ShouldBe(1);
        Regex.Count(module, @"camera\.rotation\.set\(").ShouldBe(1);
        Regex.Count(module, @"camera\.(lookAt|quaternion|up|zoom|setViewOffset|setFocalLength)\b").ShouldBe(0);
        module.ShouldNotContain(PerspectiveCamera.TiltDegrees.ToString(System.Globalization.CultureInfo.InvariantCulture) + " *");
        module.ShouldNotContain("wheel");
        module.ShouldNotContain("pinch");
    }

    [Test]
    public void RenderThree_TheFiguresOfTheCharacters_ShouldBeFlatUnlitInOnePlaneThatFacesTheCameraAtOneDepth()
    {
        // What rule M1 rests on in this renderer (the module's header says why): a part of a character is a flat
        // shape with a material that takes no light, placed by the list's transform in the plane of its character,
        // which leans back by the camera's tilt and so is parallel to the picture; every pixel of every part of a
        // character is given one depth; the parts are painted in the order of the list.
        var module = Module();

        module.ShouldContain("gl_FragDepth = uDepth;");
        module.ShouldContain("gl_FragColor = vec4(mix(uColour, uHazeColour, uHaze), 1.0);");
        module.ShouldContain("side: THREE.DoubleSide, transparent: true, depthTest: true, depthWrite: false, fog: false,");
        module.ShouldContain("uDepth: figure.depth,");
        module.ShouldContain("figure.depth.value = g.vector.set(feetX, list[FIGURE_DEPTH_HEIGHT], feetZ).project(camera).z * 0.5 + 0.5;");
        module.ShouldContain("mesh.renderOrder = 10 + index;");
        module.ShouldContain("list[at + 1], -list[at + 3], 0, e,");
        module.ShouldContain("-list[at + 2] * cos, list[at + 4] * cos, sin, (feet - f) * cos,");
        module.ShouldContain("list[at + 2] * sin, -list[at + 4] * sin, cos, feet - (feet - f) * sin,");
        module.ShouldContain("const around = 1 / Math.cos(Math.PI / ELLIPSE_SEGMENTS);");
        Regex.Count(module, "new THREE.ShaderMaterial\\(").ShouldBe(1);
        module.ShouldNotContain("SkinnedMesh");
        module.ShouldNotContain("castShadow = true;\n        g.parts");
    }

    [Test]
    public void RenderThree_WhatKeepsAFrameLight_ShouldBeOneSmallShadowMapInstancesAndNoAddon()
    {
        var module = Module();

        module.ShouldContain("const SHADOW_MAP = 1024;");
        module.ShouldContain("g.sun.shadow.mapSize.set(SHADOW_MAP, SHADOW_MAP);");
        module.ShouldContain("const antialias = (window.devicePixelRatio || 1) < 2;");
        module.ShouldContain("new THREE.InstancedMesh(geometry, g.solidMaterial, TREE_CAPACITY)");
        Regex.Count(module, "castShadow = true").ShouldBe(5, "the sun, the land, the props, the flowers and the trees cast a shadow: nothing else, and no figure");
        module.ShouldNotContain("EffectComposer");
        module.ShouldNotContain("examples/jsm");
    }

    [Test]
    public void RenderThree_ALostContext_ShouldBeAskedBackAndOtherwiseHandedToTheGame()
    {
        var module = Module();

        module.ShouldContain("\"webglcontextlost\"");
        module.ShouldContain("\"webglcontextrestored\"");
        module.ShouldContain("event.preventDefault();");
        module.ShouldContain("page.interop.RendererLost();");
        module.ShouldContain("page.root.dataset.rendererContext = \"lost\";");
        module.ShouldContain("page.root.dataset.rendererContext = \"restored\";");
    }

    [GeneratedRegex(@"\| `([^`]+)` \| `([0-9a-f]{64})` \|")]
    private static partial Regex Row();

    [GeneratedRegex(@"Version: (\d+\.\d+\.\d+)")]
    private static partial Regex Version();

    [GeneratedRegex("from\"([^\"]+)\"")]
    private static partial Regex From();

    [GeneratedRegex("^\\s*import\\s[^\\n]*?from \"([^\"]+)\";", RegexOptions.Multiline)]
    private static partial Regex StaticImport();

    [GeneratedRegex("import\\(\"([^\"]+)\"\\)")]
    private static partial Regex DynamicImport();

    [GeneratedRegex(@"const KIND = \{([^}]*)\};")]
    private static partial Regex Kinds();

    [GeneratedRegex(@"(\w+): (\d+)")]
    private static partial Regex Pair();
}
