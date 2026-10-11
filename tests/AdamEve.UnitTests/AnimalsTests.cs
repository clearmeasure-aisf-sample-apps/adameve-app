using System.Text;
using AdamEve.Content;
using AdamEve.Content.Scripture;
using AdamEve.Core.Game;
using AdamEve.Core.Story;

namespace AdamEve.UnitTests;

/// <summary>The animals brought to Adam and their kind-names: the rules of the design (section 3.5) over the shipped content.</summary>
[TestFixture]
public class AnimalsTests
{
    private static Animals Shipped() => Animals.Parse(EmbeddedContent.Animals());

    private static Animal Stub(string id = "stub", string category = "beast", params (string Word, string Meaning)[] kindNames) =>
        new(id, category, id, [.. kindNames.Select(kindName => new KindName(kindName.Word.Replace(' ', '-'), kindName.Word, kindName.Meaning))]);

    private static byte[] Json(string text) => Encoding.UTF8.GetBytes(text);

    [Test]
    public void Parse_TheShippedAnimals_ShouldBe24AnimalsWith72KindNames()
    {
        var animals = Shipped();

        animals.List.Count.ShouldBe(24);
        animals.List.Sum(animal => animal.KindNames.Count).ShouldBe(72);
        animals.MatchTheRoster().ShouldBeTrue();
        AnimalRoster.Count.ShouldBe(24);
        AnimalRoster.Animals.Count.ShouldBe(AnimalRoster.Count);
    }

    [Test]
    public void Violations_EveryAnimal_ShouldHaveThreeDistinctKindNamesWithMeanings()
    {
        var animals = Shipped().List;

        Animals.Violations(animals).ShouldBeEmpty();
        foreach (var animal in animals)
        {
            animal.KindNames.Count.ShouldBe(3, animal.Id);
            animal.KindNames.Select(kindName => kindName.Word).ShouldBeUnique();
            animal.KindNames.ShouldAllBe(kindName => kindName.Meaning.Trim().Length > 2);
            animal.KindNames.ShouldAllBe(kindName => kindName.Meaning != kindName.Word);
        }
    }

    [Test]
    public void Violations_TheKindNames_ShouldNotRepeatAcrossAnimals()
    {
        var kindNames = Shipped().List.SelectMany(animal => animal.KindNames).ToList();

        kindNames.Select(kindName => kindName.Word).ShouldBeUnique();
        kindNames.Select(kindName => kindName.Id).ShouldBeUnique();
        Shipped().List.Select(animal => animal.Id).ShouldBeUnique();
        kindNames.Select(kindName => kindName.Word).Intersect(Shipped().List.Select(animal => animal.Id)).ShouldBeEmpty("a kind-name is not the everyday name of an animal");
    }

    [Test]
    public void Violations_TheKindNames_ShouldBeLowercaseLettersAndSpaces()
    {
        var words = Shipped().List.SelectMany(animal => animal.KindNames).Select(kindName => kindName.Word);

        words.ShouldAllBe(word => word.Length > 0 && word.All(letter => (letter >= 'a' && letter <= 'z') || letter == ' ') && word == word.Trim());
    }

    [Test]
    public void Violations_TheShippedAnimals_ShouldHoldNoFishAndNoSerpent()
    {
        var animals = Shipped().List;

        animals.ShouldAllBe(animal => Animals.Categories.Contains(animal.Category));
        animals.Select(animal => animal.Id).Intersect(Animals.NotAmongTheAnimals).ShouldBeEmpty();
        var everyWord = animals.SelectMany(animal => animal.KindNames.SelectMany(kindName => kindName.Word.Split(' ').Concat(kindName.Meaning.ToLowerInvariant().Split(' ', ',')))).ToList();
        everyWord.Intersect(Animals.NotAmongTheAnimals).ShouldBeEmpty();
        everyWord.ShouldNotContain("apple");
        Animals.NotAmongTheAnimals.ShouldContain("fish");
        Animals.NotAmongTheAnimals.ShouldContain("serpent");
        animals.Select(animal => animal.Category).Distinct().Order().ShouldBe(["beast", "cattle", "fowl"]);
    }

    [Test]
    public void Categories_TheGroupsOfTheAnimals_ShouldBeWordsOfGenesis2_20AsParsed()
    {
        var verse = KjvParser.Parse(CanonicalFile.Bytes()).Find(new VerseRef(2, 20)).Text;

        Animals.Categories.ShouldAllBe(category => verse.Contains(category, StringComparison.Ordinal));
        Animals.Categories.Count.ShouldBe(Enum.GetValues<AnimalCategory>().Length);
    }

    [TestCase("serpent")]
    [TestCase("fish")]
    [TestCase("snake")]
    [TestCase("whale")]
    public void Violations_AFishOrASerpentAmongTheAnimals_ShouldBeRefused(string id)
    {
        var violations = Animals.Violations([Stub(id, "beast", ("stub one", "a"), ("stub two", "b"), ("stub three", "c"))]);

        violations.ShouldContain(violation => violation.Contains("no fish and no serpent", StringComparison.Ordinal));
    }

    [Test]
    public void Violations_AnAnimalThatIsNotCattleFowlOrBeast_ShouldBeRefused()
    {
        var violations = Animals.Violations([Stub("stub", "creeping thing", ("stub one", "a"), ("stub two", "b"), ("stub three", "c"))]);

        violations.ShouldContain(violation => violation.Contains("not cattle, fowl or beast", StringComparison.Ordinal));
    }

    [TestCase(2)]
    [TestCase(4)]
    [TestCase(0)]
    public void Violations_AnAnimalWithoutExactlyThreeKindNames_ShouldBeRefused(int count)
    {
        var kindNames = Enumerable.Range(0, count).Select(index => ($"stub {(char)('a' + index)}", "a meaning")).ToArray();

        var violations = Animals.Violations([Stub("stub", "beast", kindNames)]);

        violations.ShouldContain(violation => violation.Contains("not three", StringComparison.Ordinal));
    }

    [Test]
    public void Violations_AKindNameThatOccursTwiceAnywhere_ShouldBeRefused()
    {
        var first = Stub("stub", "beast", ("one", "a"), ("two", "b"), ("three", "c"));
        var second = Stub("other", "fowl", ("four", "a"), ("two", "b"), ("five", "c"));

        var violations = Animals.Violations([first, second]);

        violations.ShouldContain(violation => violation.Contains("\"two\" occurs twice", StringComparison.Ordinal));
    }

    [TestCase("Stub")]
    [TestCase("stub-name")]
    [TestCase("stub2")]
    [TestCase(" stub")]
    [TestCase("stub  name")]
    [TestCase("")]
    [TestCase("stüb")]
    public void Violations_AKindNameThatIsNotLowercaseLettersAndSpaces_ShouldBeRefused(string word)
    {
        var violations = Animals.Violations([Stub("stub", "beast", (word, "a"), ("two", "b"), ("three", "c"))]);

        violations.ShouldContain(violation => violation.Contains("not lowercase letters and spaces", StringComparison.Ordinal));
    }

    [TestCase("")]
    [TestCase("  ")]
    public void Violations_AKindNameWithoutAMeaning_ShouldBeRefused(string meaning)
    {
        var violations = Animals.Violations([Stub("stub", "beast", ("one", meaning), ("two", "b"), ("three", "c"))]);

        violations.ShouldContain(violation => violation.Contains("\"one\" has no meaning", StringComparison.Ordinal));
    }

    [TestCase("apple", "says apple")]
    [TestCase("apple eater", "says apple")]
    [TestCase("spot", "is a pet name")]
    [TestCase("rex", "is a pet name")]
    public void Violations_AKindNameThatIsAppleOrAPetName_ShouldBeRefused(string word, string reason)
    {
        var violations = Animals.Violations([Stub("stub", "beast", (word, "a"), ("two", "b"), ("three", "c"))]);

        violations.ShouldContain(violation => violation.Contains(reason, StringComparison.Ordinal));
    }

    [TestCase("not json")]
    [TestCase("{}")]
    [TestCase("[] []")]
    [TestCase("""[{"id":"stub","category":"beast","sprite":"stub","kindNames":[{"id":"one","word":"one","meaning":"a"}]}]""")]
    [TestCase("""[{"id":"stub","category":"beast","sprite":"stub"}]""")]
    [TestCase("""[7]""")]
    public void Parse_AnimalsThatBreakARule_ShouldBeRefused(string json)
    {
        Should.Throw<ContentFormatException>(() => Animals.Parse(Json(json)));
    }

    [Test]
    public void MatchTheRoster_AnimalsOtherThanTheStoryBrings_ShouldNotMatch()
    {
        var one = Animals.Parse(Json("""[{"id":"stub","category":"beast","sprite":"stub","kindNames":[{"id":"one","word":"one","meaning":"a"},{"id":"two","word":"two","meaning":"b"},{"id":"three","word":"three","meaning":"c"}]}]"""));

        one.MatchTheRoster().ShouldBeFalse();
    }

    [Test]
    public void Named_AKindNameId_ShouldFindItsWordItsMeaningAndItsAnimal()
    {
        var animals = Shipped();

        foreach (var animal in AnimalRoster.Animals)
        {
            foreach (var id in animal.KindNames)
            {
                var found = animals.Named(id).ShouldNotBeNull();

                found.Animal.Id.ShouldBe(animal.Id);
                found.KindName.Id.ShouldBe(id);
            }
        }

        animals.Named("a stub name").ShouldBeNull();
        animals.Find("elephant").KindNames.Select(kindName => kindName.Word).ShouldBe(["pachyderm", "proboscid", "tusker"], "the starting list of the design");
        animals.Find("pig").KindNames[0].Word.ShouldBe("swine");
        animals.Find("wolf").KindNames[0].Word.ShouldBe("canine");
    }

    [Test]
    public void AreChoices_KindNames_ShouldBeOneOfTheThreeOfEachAnimalInOrder()
    {
        AnimalRoster.AreChoices([]).ShouldBeTrue();
        AnimalRoster.AreChoices(["tusker", "ovine"]).ShouldBeTrue();
        AnimalRoster.AreChoices(["ovine"]).ShouldBeFalse();
        AnimalRoster.AreChoices([.. AnimalRoster.Animals.Select(animal => animal.KindNames[1])]).ShouldBeTrue();
        AnimalRoster.AreChoices([.. AnimalRoster.Animals.Select(animal => animal.KindNames[1]), "pachyderm"]).ShouldBeFalse();
        AnimalRoster.IndexOf("heron").ShouldBe(23);
        AnimalRoster.IndexOf("serpent").ShouldBe(-1);
    }

    [Test]
    public void Images_ThePicturesOfTheAnimals_ShouldBeAHandfulOfFlatShapesEachAndOneForEveryAnimal()
    {
        ThingArt.Images.Count.ShouldBe(AnimalRoster.Count + 3);
        foreach (var animal in Shipped().List)
        {
            var shapes = ThingArt.OfAnimal(animal.Sprite);
            var (left, top, right, bottom) = ThingArt.Bounds(shapes);

            shapes.Count.ShouldBeInRange(6, 40, animal.Id);
            (right - left).ShouldBeInRange(8, 80, animal.Id);
            (bottom - top).ShouldBeInRange(8, 70, animal.Id);
            bottom.ShouldBeLessThan(6, animal.Id + " stands on its foot");
            shapes.ShouldAllBe(shape => shape.Width > 0 && shape.Height > 0 && shape.Colour >= 0 && shape.Colour <= 0xFFFFFF);
        }

        ThingArt.Images.Select(image => string.Join(';', image)).ShouldBeUnique("no two animals are drawn alike");
        Should.Throw<ArgumentException>(() => ThingArt.OfAnimal("serpent"));
    }

    [Test]
    public void ToNumbers_ThePictures_ShouldListEveryShapeInEightNumbers()
    {
        var numbers = ThingArt.ToNumbers();

        numbers[0].ShouldBe(ThingArt.Images.Count);
        numbers.Length.ShouldBe(1 + ThingArt.Images.Count + (ThingArt.Images.Sum(image => image.Count) * 8));
        ThingArt.Sapling.ShouldBe(24);
        ThingArt.SaplingWatered.ShouldBe(25);
        ThingArt.Branch.ShouldBe(26);
    }
}
