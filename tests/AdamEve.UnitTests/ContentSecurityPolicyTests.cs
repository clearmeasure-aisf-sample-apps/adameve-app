using System.Security.Cryptography;
using System.Text;
using AdamEve.Host;

namespace AdamEve.UnitTests;

[TestFixture]
public class ContentSecurityPolicyTests
{
    private const string ImportMap = "{\n  \"imports\": {\n    \"./_framework/dotnet.js\": \"./_framework/dotnet.abc123defg.js\"\n  }\n}";

    private static string Page(string importMap) =>
        $"<!DOCTYPE html><html><head><title>t</title><script type=\"importmap\">{importMap}</script></head><body></body></html>";

    private static string Sha256(string text) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    [Test]
    public void For_APageWithAnImportMap_ShouldAllowThatImportMapByItsHash()
    {
        var policy = ContentSecurityPolicy.For(Page(ImportMap));

        policy.ShouldContain($"script-src 'self' 'wasm-unsafe-eval' 'sha256-{Sha256(ImportMap)}';");
    }

    [Test]
    public void For_APageWithAnImportMap_ShouldAllowNoOtherInlineScript()
    {
        var policy = ContentSecurityPolicy.For(Page(ImportMap));

        policy.ShouldNotContain("unsafe-inline");
        policy.Split("'sha256-").Length.ShouldBe(2);
    }

    [TestCase("")]
    [TestCase("   \n  ")]
    public void For_APageWithAnEmptyImportMap_ShouldAllowNoInlineScript(string importMap)
    {
        var policy = ContentSecurityPolicy.For(Page(importMap));

        policy.ShouldContain("script-src 'self' 'wasm-unsafe-eval';");
        policy.ShouldNotContain("sha256-");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("<html><head></head></html>")]
    public void For_NoPageOrAPageWithoutAnImportMap_ShouldAllowNoInlineScript(string? page)
    {
        var policy = ContentSecurityPolicy.For(page);

        policy.ShouldBe(
            "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self'; img-src 'self' data: blob:; "
            + "connect-src 'self'; font-src 'self'; media-src 'self'; worker-src 'self'; frame-ancestors 'none'");
    }

    [Test]
    public void ImportMapHash_AnImportMapWithWindowsLineEnds_ShouldHashItAsABrowserDoes()
    {
        var hash = ContentSecurityPolicy.ImportMapHash(Page(ImportMap.Replace("\n", "\r\n", StringComparison.Ordinal)));

        hash.ShouldBe(Sha256(ImportMap));
    }
}
