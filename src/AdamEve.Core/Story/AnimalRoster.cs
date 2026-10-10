namespace AdamEve.Core.Story;

/// <summary>
/// The three groups Genesis 2:20 names among the animals Adam named. The words a player reads come from the verse
/// itself; these are the ids of the content.
/// </summary>
public enum AnimalCategory
{
    /// <summary>Cattle.</summary>
    Cattle,

    /// <summary>Fowl of the air.</summary>
    Fowl,

    /// <summary>Beast of the field.</summary>
    Beast,
}

/// <summary>One animal brought to Adam: its id, its group, and the ids of its three kind-names.</summary>
/// <param name="Id">The id of the animal.</param>
/// <param name="Category">Its group.</param>
/// <param name="KindNames">The ids of its three kind-names. The words and their meanings are content (<c>content/animals.json</c>).</param>
public sealed record AnimalKind(string Id, AnimalCategory Category, IReadOnlyList<string> KindNames);

/// <summary>
/// The animals brought to Adam, in the order they are brought, and the kind-names he may choose from (design,
/// section 3.5, decision D9): 24 animals of the field, of the air and cattle, three kind-names each. No fish (the
/// text names none) and no serpent. Ids only: the story machine and the saved game check a choice against this
/// list, and the content must name exactly these (a check at load).
/// </summary>
public static class AnimalRoster
{
    /// <summary>How many animals are brought.</summary>
    public const int Count = 24;

    /// <summary>How many kind-names each animal has.</summary>
    public const int KindNamesEach = 3;

    /// <summary>The animals, in the order they are brought.</summary>
    public static IReadOnlyList<AnimalKind> Animals { get; } =
    [
        Of("elephant", AnimalCategory.Beast, "pachyderm", "proboscid", "tusker"),
        Of("sheep", AnimalCategory.Cattle, "ovine", "ruminant", "wool-bearer"),
        Of("dove", AnimalCategory.Fowl, "columbid", "columbine", "culver"),
        Of("lion", AnimalCategory.Beast, "feline", "leonine", "great-cat"),
        Of("pig", AnimalCategory.Cattle, "swine", "porcine", "hog"),
        Of("rabbit", AnimalCategory.Beast, "leporid", "coney", "lagomorph"),
        Of("eagle", AnimalCategory.Fowl, "raptor", "aquiline", "bird-of-prey"),
        Of("horse", AnimalCategory.Cattle, "equine", "steed", "courser"),
        Of("deer", AnimalCategory.Beast, "cervine", "cervid", "hart"),
        Of("rooster", AnimalCategory.Fowl, "chanticleer", "galliform", "poultry"),
        Of("bear", AnimalCategory.Beast, "ursine", "bruin", "ursid"),
        Of("goat", AnimalCategory.Cattle, "caprine", "hircine", "caprid"),
        Of("sparrow", AnimalCategory.Fowl, "passerine", "songbird", "oscine"),
        Of("camel", AnimalCategory.Cattle, "camelid", "dromedary", "humpback"),
        Of("squirrel", AnimalCategory.Beast, "sciurid", "sciurine", "rodent"),
        Of("duck", AnimalCategory.Fowl, "anatine", "anatid", "waterfowl"),
        Of("cow", AnimalCategory.Cattle, "bovine", "kine", "neat"),
        Of("wolf", AnimalCategory.Beast, "canine", "lupine", "wolfkind"),
        Of("peacock", AnimalCategory.Fowl, "peafowl", "pavonine", "phasianid"),
        Of("donkey", AnimalCategory.Cattle, "equid", "burro", "beast-of-burden"),
        Of("hedgehog", AnimalCategory.Beast, "urchin", "hedgepig", "erinaceid"),
        Of("raven", AnimalCategory.Fowl, "corvid", "corvine", "corbie"),
        Of("badger", AnimalCategory.Beast, "brock", "mustelid", "meline"),
        Of("heron", AnimalCategory.Fowl, "wader", "ardeid", "hern"),
    ];

    /// <summary>The place of an animal in the order, or -1 for an id that is not an animal of the list.</summary>
    /// <param name="animal">The id of the animal.</param>
    public static int IndexOf(string animal)
    {
        for (var index = 0; index < Animals.Count; index++)
        {
            if (Animals[index].Id == animal)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Whether kind-names are choices Adam can have made: one for each animal from the first on, each one of the
    /// three of its animal, and no more than there are animals.
    /// </summary>
    /// <param name="kindNames">The ids of the kind-names, in the order the animals are brought.</param>
    public static bool AreChoices(IReadOnlyList<string> kindNames)
    {
        ArgumentNullException.ThrowIfNull(kindNames);
        if (kindNames.Count > Animals.Count)
        {
            return false;
        }

        for (var index = 0; index < kindNames.Count; index++)
        {
            if (!Animals[index].KindNames.Contains(kindNames[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static AnimalKind Of(string id, AnimalCategory category, params string[] kindNames) => new(id, category, kindNames);
}
