using AdamEve.Host;

namespace AdamEve.UnitTests;

[TestFixture]
public class BuildInfoTests
{
    [TestCase("1.0.42", "1.0.42")]
    [TestCase("1.0.42+0123456789abcdef", "1.0.42")]
    [TestCase(" 1.0.42 ", "1.0.42")]
    public void VersionOf_AnInformationalVersion_ShouldBeTheVersionWithoutTheCommit(string informationalVersion, string expected)
    {
        BuildInfo.VersionOf(informationalVersion).ShouldBe(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("+0123456789abcdef")]
    public void VersionOf_NoVersion_ShouldBeZero(string? informationalVersion)
    {
        BuildInfo.VersionOf(informationalVersion).ShouldBe("0.0.0");
    }

    [Test]
    public void Version_OfThisBuild_ShouldBeThreeNumbers()
    {
        BuildInfo.Version.ShouldMatch(@"^\d+\.\d+\.\d+$");
    }
}
