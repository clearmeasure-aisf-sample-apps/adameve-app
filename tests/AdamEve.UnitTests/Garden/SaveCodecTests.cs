using System.Reflection;
using AdamEve.Core.Saves;
using AdamEve.Core.World;

namespace AdamEve.UnitTests.Garden;

[TestFixture]
public class SaveCodecTests
{
    private static TileMap Map() => StubMap.FromRows("..~", "...");

    [Test]
    public void Read_ASaveJustWritten_ShouldBeTheSameSave()
    {
        var save = new SaveGame { Character = PlayerCharacter.Woman, TileX = 1, TileY = 1, Facing = Facing.NW };

        var result = SaveCodec.Read(SaveCodec.Write(save), Map());

        result.State.ShouldBe(SaveState.Loaded);
        result.Save.ShouldBe(save);
    }

    [Test]
    public void Write_ASave_ShouldHoldNumbersAndNamesFromFixedListsOnly()
    {
        var json = SaveCodec.Write(new SaveGame { Character = PlayerCharacter.Adam, TileX = 26, TileY = 22, Facing = Facing.S });

        json.ShouldBe("""{"schemaVersion":1,"character":"Adam","tileX":26,"tileY":22,"facing":"S"}""");
    }

    [Test]
    public void SaveGame_EveryField_ShouldBeANumberAChoiceFromAListOrASwitch()
    {
        var fields = typeof(SaveGame).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Concat(typeof(GameSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.Name != "EqualityContract");

        fields.ShouldNotBeEmpty();
        fields.ShouldAllBe(property => property.PropertyType == typeof(int) || property.PropertyType == typeof(bool) || property.PropertyType.IsEnum);
    }

    [TestCase(null)]
    [TestCase("")]
    public void Read_NoSavedGame_ShouldStartANewGame(string? json)
    {
        var result = SaveCodec.Read(json, Map());

        result.ShouldBe(new SaveReadResult(SaveState.None, null));
    }

    [TestCase("not a save")]
    [TestCase("{")]
    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("""{"schemaVersion":2,"character":"Adam","tileX":0,"tileY":0,"facing":"S"}""", Description = "a version this build does not know")]
    [TestCase("""{"schemaVersion":0,"character":"Adam","tileX":0,"tileY":0,"facing":"S"}""")]
    [TestCase("""{"schemaVersion":1,"character":"Serpent","tileX":0,"tileY":0,"facing":"S"}""", Description = "not a character of the list")]
    [TestCase("""{"schemaVersion":1,"character":7,"tileX":0,"tileY":0,"facing":"S"}""")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"Up"}""")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":2,"tileY":0,"facing":"S"}""", Description = "a tile in the water")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":9,"tileY":9,"facing":"S"}""", Description = "a tile outside the map")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":"one","tileY":0,"facing":"S"}""")]
    public void Read_ASavedGameThatCannotBeRead_ShouldBeCorrupt(string json)
    {
        var result = SaveCodec.Read(json, Map());

        result.ShouldBe(new SaveReadResult(SaveState.Corrupt, null));
    }

    [Test]
    public void Read_ASaveWithTextItDoesNotKnow_ShouldLeaveTheTextOut()
    {
        var json = """{"schemaVersion":1,"character":"Adam","tileX":1,"tileY":1,"facing":"E","name":"a stub name"}""";

        var result = SaveCodec.Read(json, Map());

        result.State.ShouldBe(SaveState.Loaded);
        SaveCodec.Write(result.Save!).ShouldNotContain("stub");
    }

    [Test]
    public void ReadSettings_SettingsJustWritten_ShouldBeTheSameSettings()
    {
        var settings = new GameSettings { TextSize = TextSize.XL, Sound = false };

        var read = SaveCodec.ReadSettings(SaveCodec.WriteSettings(settings));

        read.ShouldBe(settings);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not settings")]
    [TestCase("""{"schemaVersion":3,"textSize":"XL","sound":false}""")]
    [TestCase("""{"schemaVersion":1,"textSize":"Huge","sound":false}""")]
    [TestCase("""{"schemaVersion":1,"textSize":9,"sound":false}""")]
    public void ReadSettings_SettingsThatCannotBeRead_ShouldBeTheDefaults(string? json)
    {
        var read = SaveCodec.ReadSettings(json);

        read.ShouldBe(new GameSettings());
        read.TextSize.ShouldBe(TextSize.M);
        read.Sound.ShouldBeTrue();
    }

    [Test]
    public void Keys_TheStorage_ShouldBeTheKeysOfTheDesign()
    {
        SaveCodec.SaveKey.ShouldBe("adameve.save.v1");
        SaveCodec.SettingsKey.ShouldBe("adameve.settings.v1");
        SaveCodec.CorruptKey.ShouldBe("adameve.save.corrupt");
    }
}
