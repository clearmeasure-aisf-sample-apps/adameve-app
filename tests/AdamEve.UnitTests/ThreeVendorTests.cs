using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AdamEve.Core.World;
using AdamEve.Host;

namespace AdamEve.UnitTests;

/// <summary>
/// The vendored Three.js of the trial (docs/spike-threejs.md): the files are the ones the folder's README records,
/// the host does not let a browser keep them for a year, and the renderer module asks no other origin.
/// </summary>
[TestFixture]
public partial class ThreeVendorTests
{
    private static string ClientRoot() => Path.Combine(CanonicalFile.RepositoryRoot(), "src", "AdamEve.Client", "wwwroot");

    private static string Folder() => Path.Combine(ClientRoot(), "lib", "three");

    private static string Module() => File.ReadAllText(Path.Combine(ClientRoot(), "js", "render-three.js"));

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
    public void CacheControl_AVendoredFileWithoutAFingerprint_ShouldBeAskedForAgainEachTime(string path)
    {
        SitePaths.CacheControl(path, 200).ShouldBe("no-cache");
        SitePaths.IsNavigation(path).ShouldBeFalse();
    }

    [Test]
    public void RenderThree_TheModule_ShouldAskForThreeOnlyWhenItAttachesAndForNothingElse()
    {
        var module = Module();

        StaticImport().IsMatch(module).ShouldBeFalse();
        DynamicImport().Matches(module).Select(match => match.Groups[1].Value).ShouldBe(["../lib/three/three.module.min.js"]);
        module.ShouldNotContain("http://");
        module.ShouldNotContain("https://");
        module.ShouldNotContain("fetch(");
        module.ShouldNotContain("eval(");
        module.ShouldNotContain("OrbitControls");
        File.ReadAllText(Path.Combine(ClientRoot(), "js", "render.js")).ShouldNotContain("three");
        File.ReadAllText(Path.Combine(ClientRoot(), "index.html")).ShouldNotContain("three");
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
    public void RenderThree_TheLayoutOfTheRenderList_ShouldBeTheOneOfTheGame()
    {
        var module = Module();

        module.ShouldContain($"const HEADER = {AdamEve.Core.Game.RenderList.HeaderLength};");
        module.ShouldContain($"const ENTRY = {AdamEve.Core.Game.RenderList.EntryLength};");
        module.ShouldContain($"const ANCHORS = {AdamEve.Core.Game.RenderList.Anchors};");
        module.ShouldContain($"const ANCHOR_COUNT = {AdamEve.Core.Game.RenderList.AnchorCount};");
        module.ShouldContain($"const FLAGS = {AdamEve.Core.Game.RenderList.Flags};");
        module.ShouldContain($"const CHARACTER_SHIFT = {AdamEve.Core.Game.RenderList.CharacterShift};");
    }

    [Test]
    public void RenderThree_TheFiguresOfTheCharacters_ShouldBeFlatUnlitAndInOnePlaneByTheListsOrder()
    {
        // What rule M1 rests on in this renderer (the module's header says why): a part of a character is a flat
        // shape with a material that takes no light, placed by the list's transform in the plane of its character,
        // each part a step nearer the viewer than the one listed before it; and the camera cannot be turned.
        var module = Module();

        module.ShouldContain("new THREE.MeshBasicMaterial({ color: shape.colour, side: THREE.DoubleSide, fog: false })");
        module.ShouldContain("const nearer = PLANE_LIFT + parts * PART_STEP;");
        module.ShouldContain("feet - f + nearer,");
        module.ShouldContain("0, 0, 1, feet + nearer,");
        module.ShouldContain("const around = 1 / Math.cos(Math.PI / ELLIPSE_SEGMENTS);");
        module.ShouldContain("new THREE.OrthographicCamera(");
        module.ShouldNotContain("PerspectiveCamera");
        Regex.Count(module, @"g\.camera\.(lookAt|rotation|quaternion|up|zoom)\b").ShouldBe(1);
    }

    [GeneratedRegex(@"\| `([^`]+)` \| `([0-9a-f]{64})` \|")]
    private static partial Regex Row();

    [GeneratedRegex(@"Version: (\d+\.\d+\.\d+)")]
    private static partial Regex Version();

    [GeneratedRegex("from\"([^\"]+)\"")]
    private static partial Regex From();

    [GeneratedRegex(@"^\s*import\s", RegexOptions.Multiline)]
    private static partial Regex StaticImport();

    [GeneratedRegex("import\\(\"([^\"]+)\"\\)")]
    private static partial Regex DynamicImport();

    [GeneratedRegex(@"const KIND = \{([^}]*)\};")]
    private static partial Regex Kinds();

    [GeneratedRegex(@"(\w+): (\d+)")]
    private static partial Regex Pair();
}
