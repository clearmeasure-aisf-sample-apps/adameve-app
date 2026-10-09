using AdamEve.Core;

namespace AdamEve.UnitTests;

[TestFixture]
public class GameInfoTests
{
    [Test]
    public void Title_ShouldBeTheTitleOfTheGame()
    {
        GameInfo.Title.ShouldBe("Adam and woman in the garden of Eden");
    }
}
