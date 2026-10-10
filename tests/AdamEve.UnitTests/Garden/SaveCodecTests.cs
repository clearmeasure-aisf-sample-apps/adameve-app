using System.Reflection;
using AdamEve.Core.Saves;
using AdamEve.Core.Story;
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

        json.ShouldBe("""{"schemaVersion":1,"character":"Adam","tileX":26,"tileY":22,"facing":"S","chapter":"GardenLife","beat":"B17","creationWatched":false,"namedAnimals":[]}""");
    }

    [Test]
    public void SaveGame_EveryField_ShouldBeANumberAChoiceFromAListOrASwitch()
    {
        var fields = typeof(SaveGame).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Concat(typeof(GameSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.Name != "EqualityContract");

        fields.ShouldNotBeEmpty();
        fields.ShouldAllBe(property => property.PropertyType == typeof(int) || property.PropertyType == typeof(bool) || property.PropertyType.IsEnum
            || property.Name == nameof(SaveGame.NamedAnimals));
        typeof(SaveGame).GetProperty(nameof(SaveGame.NamedAnimals))!.PropertyType.ShouldBe(typeof(string[]), "ids of the content's list of kind-names, which reading checks one by one: never typed text");
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

    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"Title","beat":"B0"}""", Description = "the title is not saved")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"Creation","beat":"B8"}""", Description = "a beat of another chapter")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"Formation","beat":"B3"}""", Description = "a beat of another chapter")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"Exodus","beat":"B1"}""", Description = "not a chapter of the list")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"Creation","beat":"B77"}""", Description = "not a beat of the list")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"Creation","beat":"B1","creationWatched":"yes"}""")]
    public void Read_ASavedGameWhoseStoryCannotBe_ShouldBeCorrupt(string json)
    {
        var result = SaveCodec.Read(json, Map());

        result.ShouldBe(new SaveReadResult(SaveState.Corrupt, null));
    }

    [Test]
    public void Read_ASavedGameOfSliceS2WithoutAChapter_ShouldStandInTheGardenWithBothCharactersAndNothingToSkip()
    {
        var json = """{"schemaVersion":1,"character":"Woman","tileX":1,"tileY":1,"facing":"E"}""";

        var result = SaveCodec.Read(json, Map());

        result.State.ShouldBe(SaveState.Loaded);
        result.Save.ShouldBe(new SaveGame { Character = PlayerCharacter.Woman, TileX = 1, TileY = 1, Facing = Facing.E, Chapter = StoryChapter.GardenLife, Beat = StoryBeat.B17, CreationWatched = false });
        StoryState.FromSave(result.Save!).WomanCreated.ShouldBeTrue();
    }

    [TestCase(PlayerCharacter.Adam)]
    [TestCase(PlayerCharacter.Woman)]
    public void Read_ASavedGameOfSliceS4AfterTheDaysOfCreation_ShouldBeReadAndStandAtTheFormationWithNoAnimalNamed(PlayerCharacter character)
    {
        // What slice S4 wrote when the days of creation were over: no kind-names, and the beat after the seventh day.
        var json = $$"""{"schemaVersion":1,"character":"{{character}}","tileX":1,"tileY":1,"facing":"S","chapter":"Formation","beat":"B8","creationWatched":true}""";

        var result = SaveCodec.Read(json, Map());

        result.State.ShouldBe(SaveState.Loaded);
        result.Save.ShouldBe(new SaveGame { Character = character, TileX = 1, TileY = 1, Chapter = StoryChapter.Formation, Beat = StoryBeat.B8, CreationWatched = true });
        result.Save!.NamedAnimals.ShouldBeEmpty();
        var state = StoryState.FromSave(result.Save);
        state.Man.ShouldBe(ManLabel.TheMan);
        StoryMachine.PlaysTheGarden(state).ShouldBe(character == PlayerCharacter.Adam, "the man's path plays the formation; the woman's is not built yet and walks the garden as before");
    }

    [Test]
    public void Read_ASavedGameInTheNaming_ShouldKeepTheKindNamesChosenAsTheirIds()
    {
        var chosen = AnimalRoster.Animals.Take(5).Select((animal, index) => animal.KindNames[index % AnimalRoster.KindNamesEach]).ToArray();
        var save = new SaveGame { Character = PlayerCharacter.Adam, TileX = 1, TileY = 1, Chapter = StoryChapter.Naming, Beat = StoryBeat.B13, CreationWatched = true, NamedAnimals = chosen };

        var json = SaveCodec.Write(save);
        var result = SaveCodec.Read(json, Map());

        result.State.ShouldBe(SaveState.Loaded);
        result.Save.ShouldBe(save);
        result.Save!.NamedAnimals.ShouldBe(chosen);
        json.ShouldContain("\"namedAnimals\":[\"" + string.Join("\",\"", chosen) + "\"]");
        var state = StoryState.FromSave(result.Save);
        state.NamedAnimals.ShouldBe(chosen);
        state.Man.ShouldBe(ManLabel.Adam);
    }

    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"Naming","beat":"B13","namedAnimals":["a stub name"]}""", Description = "typed text is no kind-name")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"Naming","beat":"B13","namedAnimals":["swine"]}""", Description = "a kind-name of another animal")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"Naming","beat":"B13","namedAnimals":null}""")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"Naming","beat":"B13","namedAnimals":"pachyderm"}""")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"Command","beat":"B11","namedAnimals":["pachyderm"]}""", Description = "a name before the naming")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"WomanFormed","beat":"B14","namedAnimals":["pachyderm"]}""", Description = "after the naming every animal has its name")]
    [TestCase("""{"schemaVersion":1,"character":"Woman","tileX":0,"tileY":0,"facing":"S","chapter":"Naming","beat":"B13"}""", Description = "the woman is not made yet when the animals are named")]
    [TestCase("""{"schemaVersion":1,"character":"Woman","tileX":0,"tileY":0,"facing":"S","chapter":"Command","beat":"B11"}""", Description = "the command is given to the man")]
    [TestCase("""{"schemaVersion":1,"character":"Adam","tileX":0,"tileY":0,"facing":"S","chapter":"Command","beat":"B13"}""", Description = "a beat of another chapter")]
    public void Read_ASavedGameWhoseNamingCannotBe_ShouldBeCorrupt(string json)
    {
        var result = SaveCodec.Read(json, Map());

        result.ShouldBe(new SaveReadResult(SaveState.Corrupt, null));
    }

    [Test]
    public void Read_ASavedGameAfterTheNaming_ShouldHoldAllTwentyFourKindNames()
    {
        var chosen = AnimalRoster.Animals.Select(animal => animal.KindNames[2]).ToArray();
        var save = new SaveGame { Character = PlayerCharacter.Adam, TileX = 1, TileY = 1, Chapter = StoryChapter.WomanFormed, Beat = StoryBeat.B14, NamedAnimals = chosen };

        var result = SaveCodec.Read(SaveCodec.Write(save), Map());

        result.State.ShouldBe(SaveState.Loaded);
        result.Save!.NamedAnimals.Length.ShouldBe(24);
        StoryState.FromSave(result.Save).WomanCreated.ShouldBeFalse();
    }

    [Test]
    public void Read_ASavedGameInADayOfCreation_ShouldKeepItsBeatAndWhetherTheDaysWereWatched()
    {
        var save = new SaveGame { Character = PlayerCharacter.Adam, TileX = 1, TileY = 1, Chapter = StoryChapter.Creation, Beat = StoryBeat.B5, CreationWatched = true };

        var result = SaveCodec.Read(SaveCodec.Write(save), Map());

        result.State.ShouldBe(SaveState.Loaded);
        result.Save.ShouldBe(save);
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
