using System.Text.Json;
using AdamEve.Core.Story;

namespace AdamEve.Content;

/// <summary>One of the three kind-names of an animal (design, section 3.5, decision D9). Game-written text.</summary>
/// <param name="Id">The id the story and the saved game hold.</param>
/// <param name="Word">The word a player reads and chooses: a real English word for that kind of animal.</param>
/// <param name="Meaning">Its meaning, in one line.</param>
public sealed record KindName(string Id, string Word, string Meaning);

/// <summary>An animal brought to Adam, as the content lists it.</summary>
/// <param name="Id">The id of the animal.</param>
/// <param name="Category">Its group: "cattle", "fowl" or "beast", words of Genesis 2:20.</param>
/// <param name="Sprite">The id of its picture, which the game draws by code.</param>
/// <param name="KindNames">Its three kind-names.</param>
public sealed record Animal(string Id, string Category, string Sprite, IReadOnlyList<KindName> KindNames);

/// <summary>
/// The animals brought to Adam and the kind-names he may give them: <c>content/animals.json</c>, read and checked
/// by the rules of the design (section 3.5).
/// </summary>
public sealed class Animals
{
    /// <summary>The groups an animal may belong to: the three Genesis 2:20 names.</summary>
    public static readonly IReadOnlyList<string> Categories = ["cattle", "fowl", "beast"];

    /// <summary>
    /// What may be no kind-name and no animal: the fruit is never an apple, a kind-name is not a pet name, and
    /// there is no fish (the text names none) and no serpent among the animals.
    /// </summary>
    public static readonly IReadOnlyList<string> PetNames =
    [
        "spot", "rex", "fido", "rover", "fluffy", "buddy", "max", "whiskers", "leo", "kitty", "polly", "dobbin", "bessie", "porky", "tweety", "bunny",
    ];

    /// <inheritdoc cref="PetNames"/>
    public static readonly IReadOnlyList<string> NotAmongTheAnimals =
    [
        "fish", "whale", "shark", "eel", "dolphin", "serpent", "snake", "viper", "adder", "asp", "cobra", "python", "dragon",
    ];

    private readonly Dictionary<string, (Animal Animal, KindName KindName)> byKindName = [];

    private Animals(IReadOnlyList<Animal> list)
    {
        List = list;
        foreach (var animal in list)
        {
            foreach (var kindName in animal.KindNames)
            {
                byKindName[kindName.Id] = (animal, kindName);
            }
        }
    }

    /// <summary>The animals, in the order of the file.</summary>
    public IReadOnlyList<Animal> List { get; }

    /// <summary>The animal with an id.</summary>
    /// <param name="id">The id.</param>
    /// <exception cref="KeyNotFoundException">There is no such animal.</exception>
    public Animal Find(string id) => List.FirstOrDefault(animal => animal.Id == id) ?? throw new KeyNotFoundException($"There is no animal \"{id}\".");

    /// <summary>The kind-name with an id, and the animal it names; null for an id that is no kind-name.</summary>
    /// <param name="kindNameId">The id of the kind-name.</param>
    public (Animal Animal, KindName KindName)? Named(string kindNameId) => byKindName.TryGetValue(kindNameId, out var found) ? found : null;

    /// <summary>
    /// Reads <c>content/animals.json</c>: a JSON array of animals, each with <c>id</c>, <c>category</c>,
    /// <c>sprite</c> and <c>kindNames</c> (<c>id</c>, <c>word</c>, <c>meaning</c>).
    /// </summary>
    /// <param name="utf8Json">The bytes of the file.</param>
    /// <exception cref="ContentFormatException">The bytes are not that, or a rule of <see cref="Violations"/> is broken.</exception>
    public static Animals Parse(ReadOnlySpan<byte> utf8Json)
    {
        var list = new List<Animal>();
        try
        {
            var reader = new Utf8JsonReader(utf8Json);
            using var json = JsonDocument.ParseValue(ref reader);
            if (json.RootElement.ValueKind != JsonValueKind.Array || reader.Read())
            {
                throw new ContentFormatException("The animals are not one JSON array.");
            }

            foreach (var element in json.RootElement.EnumerateArray())
            {
                var kindNames = new List<KindName>();
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("kindNames", out var names) && names.ValueKind == JsonValueKind.Array)
                {
                    kindNames.AddRange(names.EnumerateArray().Select(name => new KindName(Text(name, "id"), Text(name, "word"), Text(name, "meaning"))));
                }

                list.Add(new Animal(Text(element, "id"), Text(element, "category"), Text(element, "sprite"), kindNames));
            }
        }
        catch (JsonException exception)
        {
            throw new ContentFormatException("The animals are not JSON.", exception);
        }

        var violations = Violations(list);
        return violations.Count == 0 ? new Animals(list) : throw new ContentFormatException(violations[0]);
    }

    /// <summary>
    /// The kind-name rules of the design (section 3.5), over a list of animals: exactly three kind-names for each
    /// animal; no kind-name occurs twice anywhere; a word is lowercase letters and spaces; every kind-name has a
    /// meaning; no kind-name is "apple" or a pet name; and no animal is a fish or a serpent, each being cattle,
    /// fowl or beast.
    /// </summary>
    /// <param name="animals">The animals.</param>
    /// <returns>What is wrong, in words; empty when nothing is.</returns>
    public static IReadOnlyList<string> Violations(IReadOnlyList<Animal> animals)
    {
        ArgumentNullException.ThrowIfNull(animals);
        var violations = new List<string>();
        var words = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var animalIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var animal in animals)
        {
            if (string.IsNullOrWhiteSpace(animal.Id) || !animalIds.Add(animal.Id))
            {
                violations.Add($"The animal \"{animal.Id}\" has no id of its own.");
            }

            if (!Categories.Contains(animal.Category))
            {
                violations.Add($"The animal \"{animal.Id}\" is not cattle, fowl or beast.");
            }

            if (string.IsNullOrWhiteSpace(animal.Sprite))
            {
                violations.Add($"The animal \"{animal.Id}\" has no picture.");
            }

            if (Forbidden(animal.Id, NotAmongTheAnimals) || Forbidden(animal.Sprite, NotAmongTheAnimals))
            {
                violations.Add($"\"{animal.Id}\" is not among the animals: no fish and no serpent.");
            }

            if (animal.KindNames.Count != AnimalRoster.KindNamesEach)
            {
                violations.Add($"The animal \"{animal.Id}\" has {animal.KindNames.Count} kind-names, not three.");
            }

            foreach (var kindName in animal.KindNames)
            {
                if (kindName.Word.Length == 0 || kindName.Word != kindName.Word.Trim() || kindName.Word.Contains("  ", StringComparison.Ordinal)
                    || !kindName.Word.All(letter => letter is (>= 'a' and <= 'z') or ' '))
                {
                    violations.Add($"The kind-name \"{kindName.Word}\" is not lowercase letters and spaces.");
                }

                if (!words.Add(kindName.Word))
                {
                    violations.Add($"The kind-name \"{kindName.Word}\" occurs twice.");
                }

                if (kindName.Id != kindName.Word.Replace(' ', '-') || !ids.Add(kindName.Id))
                {
                    violations.Add($"The kind-name \"{kindName.Word}\" has no id of its own: its word, with a hyphen for each space.");
                }

                if (string.IsNullOrWhiteSpace(kindName.Meaning) || kindName.Meaning != kindName.Meaning.Trim())
                {
                    violations.Add($"The kind-name \"{kindName.Word}\" has no meaning.");
                }

                if (Forbidden(kindName.Word, ["apple"]) || kindName.Meaning.Contains("apple", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"The kind-name \"{kindName.Word}\" says apple.");
                }

                if (PetNames.Contains(kindName.Word))
                {
                    violations.Add($"The kind-name \"{kindName.Word}\" is a pet name.");
                }

                if (Forbidden(kindName.Word, NotAmongTheAnimals))
                {
                    violations.Add($"The kind-name \"{kindName.Word}\" names a fish or a serpent.");
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Whether the animals are exactly those the story brings (<see cref="AnimalRoster"/>): the same ids in the
    /// same order, each of its group, with the same three kind-name ids.
    /// </summary>
    public bool MatchTheRoster() => List.Count == AnimalRoster.Animals.Count && List.Zip(AnimalRoster.Animals).All(pair =>
        pair.First.Id == pair.Second.Id
        && pair.First.Category == Categories[(int)pair.Second.Category]
        && pair.First.KindNames.Select(kindName => kindName.Id).SequenceEqual(pair.Second.KindNames));

    private static bool Forbidden(string text, IReadOnlyList<string> words) =>
        text.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries).Any(part => words.Contains(part, StringComparer.OrdinalIgnoreCase));

    private static string Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
