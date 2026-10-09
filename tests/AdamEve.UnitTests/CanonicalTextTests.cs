using System.Security.Cryptography;
using AdamEve.Content;

namespace AdamEve.UnitTests;

[TestFixture]
public class CanonicalTextTests
{
    [Test]
    public void Sha256_OfTheFileInTheRepository_ShouldBeThePinnedValue()
    {
        var file = Path.Combine(RepositoryRoot(), CanonicalText.RepositoryPath);

        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));

        hash.ShouldBe(CanonicalText.Sha256);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AdamEve.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("AdamEve.slnx was not found above the test directory.");
    }
}
