using AdamEve.Core.Game;

namespace AdamEve.UnitTests.Garden;

[TestFixture]
public class RendererChoiceTests
{
    [TestCase("three", RendererKind.Three)]
    [TestCase("Three", RendererKind.Three)]
    [TestCase(" canvas ", RendererKind.Canvas)]
    public void Parse_AName_ShouldBeItsRenderer(string name, RendererKind expected)
    {
        RendererChoice.Parse(name).ShouldBe(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("webgl")]
    [TestCase("three.js")]
    public void Parse_NoNameOrAnUnknownOne_ShouldBeNull(string? name)
    {
        RendererChoice.Parse(name).ShouldBeNull();
    }

    [TestCase("https://example.test/garden?renderer=three", RendererKind.Three)]
    [TestCase("https://example.test/garden?renderer=canvas", RendererKind.Canvas)]
    [TestCase("https://example.test/garden?a=1&renderer=canvas#top", RendererKind.Canvas)]
    [TestCase("?renderer=canvas", RendererKind.Canvas)]
    public void FromAddress_AnAddressThatAsksForARenderer_ShouldBeThatRenderer(string address, RendererKind expected)
    {
        RendererChoice.FromAddress(address).ShouldBe(expected);
    }

    [TestCase(null)]
    [TestCase("https://example.test/garden")]
    [TestCase("https://example.test/garden?renderer=")]
    [TestCase("https://example.test/garden?renderer=other")]
    [TestCase("https://example.test/garden?xrenderer=canvas")]
    [TestCase("https://example.test/garden#renderer=canvas")]
    public void FromAddress_AnAddressThatAsksForNone_ShouldBeNull(string? address)
    {
        RendererChoice.FromAddress(address).ShouldBeNull();
    }

    [TestCase(null)]
    [TestCase("https://example.test/garden")]
    [TestCase("https://example.test/garden?renderer=other")]
    [TestCase("https://example.test/garden?renderer=three")]
    public void Choose_NothingAskedOrThreeAsked_ShouldBeThree(string? address)
    {
        RendererChoice.Choose(address).ShouldBe(RendererKind.Three);
    }

    [Test]
    public void Choose_TheAddressAsksForTheCanvas_ShouldBeTheCanvas()
    {
        RendererChoice.Choose("https://example.test/garden?renderer=canvas").ShouldBe(RendererKind.Canvas);
    }

    [Test]
    public void NameOf_EachRenderer_ShouldParseBackToIt()
    {
        foreach (var kind in Enum.GetValues<RendererKind>())
        {
            RendererChoice.Parse(RendererChoice.NameOf(kind)).ShouldBe(kind);
        }
    }

    [Test]
    public void RendererKind_TheDefault_ShouldBeThree()
    {
        default(RendererKind).ShouldBe(RendererKind.Three);
    }

    [Test]
    public void Choice_TheRenderer_ShouldNotBePartOfTheSaveFormat()
    {
        typeof(AdamEve.Core.Saves.GameSettings).GetProperties().ShouldAllBe(property => !property.Name.Contains("Renderer", StringComparison.OrdinalIgnoreCase));
        typeof(AdamEve.Core.Saves.SaveGame).GetProperties().ShouldAllBe(property => !property.Name.Contains("Renderer", StringComparison.OrdinalIgnoreCase));
    }
}
