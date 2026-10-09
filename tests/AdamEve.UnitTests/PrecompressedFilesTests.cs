using AdamEve.Host;

namespace AdamEve.UnitTests;

[TestFixture]
public class PrecompressedFilesTests
{
    private static readonly string[] Copies = ["/_framework/dotnet.wasm.br", "/_framework/dotnet.wasm.gz", "/css/app.css.gz"];

    private static bool Exists(string path) => Copies.Contains(path);

    [TestCase("br", "/_framework/dotnet.wasm.br")]
    [TestCase("gzip, deflate, br", "/_framework/dotnet.wasm.br")]
    [TestCase("gzip, deflate, br, zstd", "/_framework/dotnet.wasm.br")]
    [TestCase("br;q=0.5, gzip;q=1.0", "/_framework/dotnet.wasm.br")]
    [TestCase("BR", "/_framework/dotnet.wasm.br")]
    [TestCase("gzip", "/_framework/dotnet.wasm.gz")]
    [TestCase("gzip, br;q=0", "/_framework/dotnet.wasm.gz")]
    public void Choose_ARequestThatAcceptsACoding_ShouldAnswerWithTheSmallestCopyItAccepts(string acceptEncoding, string expected)
    {
        PrecompressedFiles.Choose("/_framework/dotnet.wasm", [acceptEncoding], Exists).ShouldBe(expected);
    }

    [TestCase("")]
    [TestCase("identity")]
    [TestCase("deflate")]
    [TestCase("br;q=0, gzip;q=0")]
    public void Choose_ARequestThatAcceptsNoCodingWithACopy_ShouldAnswerWithTheFileAsItIs(string acceptEncoding)
    {
        PrecompressedFiles.Choose("/_framework/dotnet.wasm", [acceptEncoding], Exists).ShouldBeNull();
    }

    [Test]
    public void Choose_ARequestWithoutAcceptEncoding_ShouldAnswerWithTheFileAsItIs()
    {
        PrecompressedFiles.Choose("/_framework/dotnet.wasm", [], Exists).ShouldBeNull();
    }

    [Test]
    public void Choose_AFileWithAGzipCopyOnly_ShouldAnswerWithGzipToARequestThatPrefersBrotli()
    {
        PrecompressedFiles.Choose("/css/app.css", ["br, gzip"], Exists).ShouldBe("/css/app.css.gz");
    }

    [Test]
    public void Choose_AFileWithoutACopy_ShouldAnswerWithTheFileAsItIs()
    {
        PrecompressedFiles.Choose("/icon-192.png", ["br, gzip"], Exists).ShouldBeNull();
    }

    [TestCase("/css/app.css.br", true)]
    [TestCase("/css/app.css.gz", true)]
    [TestCase("/css/app.css.BR", true)]
    [TestCase("/css/app.css", false)]
    [TestCase("/", false)]
    public void IsCopy_APath_ShouldBeTrueForACompressedCopyOnly(string path, bool expected)
    {
        PrecompressedFiles.IsCopy(path).ShouldBe(expected);
    }

    [TestCase("dotnet.wasm.br", "br")]
    [TestCase("dotnet.wasm.gz", "gzip")]
    [TestCase("dotnet.wasm", null)]
    public void CodingOf_AFileName_ShouldBeTheCodingOfACopy(string fileName, string? expected)
    {
        PrecompressedFiles.CodingOf(fileName).ShouldBe(expected);
    }

    [TestCase("/_framework/dotnet.native.abc123defg.wasm", "application/wasm")]
    [TestCase("/_framework/dotnet.native.abc123defg.wasm.br", "application/wasm")]
    [TestCase("/_framework/dotnet.abc123defg.js.gz", "text/javascript")]
    [TestCase("/_framework/icudt_EFIGS.abc123defg.dat.br", "application/octet-stream")]
    [TestCase("/index.html.br", "text/html")]
    [TestCase("/css/app.css", "text/css")]
    [TestCase("/assets/theme.abc123defg.m4a", "audio/mp4")]
    [TestCase("/manifest.webmanifest", "application/manifest+json")]
    public void TryGetContentType_AFileOrItsCopy_ShouldBeTheTypeOfTheFile(string path, string expected)
    {
        var found = new PrecompressedFiles.ContentTypes().TryGetContentType(path, out var contentType);

        found.ShouldBeTrue();
        contentType.ShouldBe(expected);
    }

    [Test]
    public void TryGetContentType_AFileOfAnUnknownKind_ShouldNotBeServed()
    {
        new PrecompressedFiles.ContentTypes().TryGetContentType("/secrets.unknownkind", out _).ShouldBeFalse();
    }
}
