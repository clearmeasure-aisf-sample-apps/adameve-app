using AdamEve.Host;

namespace AdamEve.UnitTests;

[TestFixture]
public class SitePathsTests
{
    [TestCase("/")]
    [TestCase("/garden")]
    [TestCase("/garden/east/")]
    [TestCase("/days/3")]
    public void IsNavigation_APathAPersonNavigatesTo_ShouldBeTrue(string path)
    {
        SitePaths.IsNavigation(path).ShouldBeTrue();
    }

    [TestCase("/index.html")]
    [TestCase("/css/app.css")]
    [TestCase("/missing.js")]
    [TestCase("/_framework/dotnet.js")]
    [TestCase("/_framework/missing")]
    [TestCase("/assets/missing")]
    [TestCase("/_healthcheck")]
    [TestCase("/_version")]
    [TestCase("/_build")]
    [TestCase("/_anything")]
    [TestCase("/alive")]
    public void IsNavigation_AFileOrAPathOfTheSiteItself_ShouldBeFalse(string path)
    {
        SitePaths.IsNavigation(path).ShouldBeFalse();
    }

    [TestCase("/_healthcheck", true)]
    [TestCase("/alive", true)]
    [TestCase("/_version", true)]
    [TestCase("/_build", true)]
    [TestCase("/", false)]
    [TestCase("/index.html", false)]
    [TestCase("/_health/files.json", false)]
    [TestCase("/_framework/dotnet.abc123defg.js", false)]
    public void IsOpenToEveryOrigin_APath_ShouldBeTrueForTheFourHealthPathsOnly(string path, bool expected)
    {
        SitePaths.IsOpenToEveryOrigin(path).ShouldBe(expected);
    }

    [TestCase("/_healthcheck", 200)]
    [TestCase("/_healthcheck", 503)]
    [TestCase("/alive", 200)]
    [TestCase("/_version", 200)]
    [TestCase("/_build", 200)]
    [TestCase("/_health/files.json", 200)]
    public void CacheControl_AHealthPath_ShouldNeverBeKept(string path, int statusCode)
    {
        SitePaths.CacheControl(path, statusCode).ShouldBe("no-store");
    }

    [TestCase("/_framework/dotnet.native.abc123defg.wasm", 200)]
    [TestCase("/_framework/dotnet.native.abc123defg.wasm", 304)]
    [TestCase("/assets/tiles.abc123defg.webp", 200)]
    public void CacheControl_AFingerprintedFileThatIsThere_ShouldBeKeptForAYear(string path, int statusCode)
    {
        SitePaths.CacheControl(path, statusCode).ShouldBe("public, max-age=31536000, immutable");
    }

    [TestCase("/", 200)]
    [TestCase("/index.html", 200)]
    [TestCase("/garden/east", 200)]
    [TestCase("/css/app.css", 200)]
    [TestCase("/service-worker.js", 200)]
    [TestCase("/_framework/missing.abc123defg.wasm", 404)]
    [TestCase("/missing.js", 404)]
    public void CacheControl_EverythingElse_ShouldBeAskedForAgainEachTime(string path, int statusCode)
    {
        SitePaths.CacheControl(path, statusCode).ShouldBe("no-cache");
    }
}
