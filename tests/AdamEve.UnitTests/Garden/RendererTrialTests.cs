using AdamEve.Core.Game;

namespace AdamEve.UnitTests.Garden;

[TestFixture]
public class RendererTrialTests
{
    [TestCase("three", RendererKind.Three)]
    [TestCase("Three", RendererKind.Three)]
    [TestCase(" canvas ", RendererKind.Canvas)]
    public void Parse_AName_ShouldBeItsRenderer(string name, RendererKind expected)
    {
        RendererTrial.Parse(name).ShouldBe(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("webgl")]
    [TestCase("three.js")]
    public void Parse_NoNameOrAnUnknownOne_ShouldBeNull(string? name)
    {
        RendererTrial.Parse(name).ShouldBeNull();
    }

    [TestCase("https://example.test/garden?renderer=three", RendererKind.Three)]
    [TestCase("https://example.test/garden?renderer=canvas", RendererKind.Canvas)]
    [TestCase("https://example.test/garden?a=1&renderer=three#top", RendererKind.Three)]
    [TestCase("?renderer=three", RendererKind.Three)]
    public void FromAddress_AnAddressThatAsksForARenderer_ShouldBeThatRenderer(string address, RendererKind expected)
    {
        RendererTrial.FromAddress(address).ShouldBe(expected);
    }

    [TestCase(null)]
    [TestCase("https://example.test/garden")]
    [TestCase("https://example.test/garden?renderer=")]
    [TestCase("https://example.test/garden?renderer=other")]
    [TestCase("https://example.test/garden?xrenderer=three")]
    [TestCase("https://example.test/garden#renderer=three")]
    public void FromAddress_AnAddressThatAsksForNone_ShouldBeNull(string? address)
    {
        RendererTrial.FromAddress(address).ShouldBeNull();
    }

    [Test]
    public void Choose_NothingAsked_ShouldBeTheCanvas()
    {
        RendererTrial.Choose("https://example.test/garden", null).ShouldBe(RendererKind.Canvas);
    }

    [Test]
    public void Choose_TheTabKeptThree_ShouldBeThree()
    {
        RendererTrial.Choose("https://example.test/garden", "three").ShouldBe(RendererKind.Three);
    }

    [Test]
    public void Choose_TheAddressAsksForCanvasAndTheTabKeptThree_ShouldBeWhatTheAddressAsks()
    {
        RendererTrial.Choose("https://example.test/garden?renderer=canvas", "three").ShouldBe(RendererKind.Canvas);
    }

    [Test]
    public void NameOf_EachRenderer_ShouldParseBackToIt()
    {
        foreach (var kind in Enum.GetValues<RendererKind>())
        {
            RendererTrial.Parse(RendererTrial.NameOf(kind)).ShouldBe(kind);
        }
    }

    [Test]
    public void SessionKey_TheTrial_ShouldNotBeAKeyOfTheSaveFormat()
    {
        RendererTrial.SessionKey.ShouldNotBe(AdamEve.Core.Saves.SaveCodec.SaveKey);
        RendererTrial.SessionKey.ShouldNotBe(AdamEve.Core.Saves.SaveCodec.SettingsKey);
        typeof(AdamEve.Core.Saves.GameSettings).GetProperties().ShouldAllBe(property => !property.Name.Contains("Renderer", StringComparison.OrdinalIgnoreCase));
        typeof(AdamEve.Core.Saves.SaveGame).GetProperties().ShouldAllBe(property => !property.Name.Contains("Renderer", StringComparison.OrdinalIgnoreCase));
    }
}
