using System.Globalization;
using AdamEve.Core.Saves;

namespace AdamEve.Core.Story;

/// <summary>
/// The chapters of the story (design, section 3.1), as far as the slices built so far reach; the chapters of the
/// later slices are added with them.
/// </summary>
public enum StoryChapter
{
    /// <summary>The title and the character select: beat B0.</summary>
    Title,

    /// <summary>The days of creation, Genesis 1:1 to 2:3: beats B1 to B7.</summary>
    Creation,

    /// <summary>
    /// Mist and formation, Genesis 2:4 to 2:7: beat B8. The man's path plays it. The woman's path of these beats is
    /// not built yet: for her the walk in the garden stands here.
    /// </summary>
    Formation,

    /// <summary>The garden planted, and the man put into it to dress it and to keep it, Genesis 2:8 to 2:15: beats B9 and B10.</summary>
    GardenIntro,

    /// <summary>The command, given to the man, and what follows it, Genesis 2:16 to 2:18: beats B11 and B12.</summary>
    Command,

    /// <summary>Naming the animals, Genesis 2:19 to 2:20: beat B13.</summary>
    Naming,

    /// <summary>
    /// What follows Genesis 2:20: beat B14 and after. Until its slice exists, the man walks the garden here, the
    /// animals he named about him, and the woman is not made yet.
    /// </summary>
    WomanFormed,

    /// <summary>
    /// Life in the garden, beat B17: both walk the garden. A saved game that names no chapter stands here: it was
    /// made walking the garden in slice S2, with both characters in it.
    /// </summary>
    GardenLife,
}

/// <summary>The beats of the story by their numbers in the design (section 2.2). Each is one state of the story machine.</summary>
public enum StoryBeat
{
    /// <summary>Title, character select.</summary>
    B0,

    /// <summary>Day 1: Genesis 1:1 to 1:5.</summary>
    B1,

    /// <summary>Day 2: Genesis 1:6 to 1:8.</summary>
    B2,

    /// <summary>Day 3: Genesis 1:9 to 1:13.</summary>
    B3,

    /// <summary>Day 4: Genesis 1:14 to 1:19.</summary>
    B4,

    /// <summary>Day 5: Genesis 1:20 to 1:23.</summary>
    B5,

    /// <summary>Day 6: Genesis 1:24 to 1:31.</summary>
    B6,

    /// <summary>Day 7: Genesis 2:1 to 2:3.</summary>
    B7,

    /// <summary>Mist and formation: Genesis 2:4 to 2:7. The first beat after the days of creation.</summary>
    B8,

    /// <summary>The garden planted: Genesis 2:8 to 2:14.</summary>
    B9,

    /// <summary>Dress and keep: Genesis 2:15.</summary>
    B10,

    /// <summary>The command: Genesis 2:16 to 2:17.</summary>
    B11,

    /// <summary>Not good to be alone: Genesis 2:18.</summary>
    B12,

    /// <summary>Naming the animals: Genesis 2:19 to 2:20.</summary>
    B13,

    /// <summary>Deep sleep, the woman made: Genesis 2:21 to 2:22. Not built yet.</summary>
    B14,

    /// <summary>"Bone of my bones": Genesis 2:23. Not built yet.</summary>
    B15,

    /// <summary>One flesh, not ashamed: Genesis 2:24 to 2:25. Not built yet.</summary>
    B16,

    /// <summary>Life in the garden: no new verse.</summary>
    B17,
}

/// <summary>Who speaks on a Scripture card. The only voice so far is God's, and it speaks Scripture only.</summary>
public enum StorySpeaker
{
    /// <summary>God, as chapter 1 of Genesis names him (design, section 1).</summary>
    God,

    /// <summary>The LORD God, as Genesis names him from 2:4 (design, section 1).</summary>
    LordGod,
}

/// <summary>
/// What the game calls the man (design, section 2.1, decision D3): the text calls him "the man" until Genesis 2:19,
/// where "Adam" first appears.
/// </summary>
public enum ManLabel
{
    /// <summary>Until Genesis 2:19.</summary>
    TheMan,

    /// <summary>From Genesis 2:19.</summary>
    Adam,
}

/// <summary>What a player may choose, from a fixed list.</summary>
public enum StoryChoice
{
    /// <summary>Leave the days of creation for what follows them. Offered only after a first completion.</summary>
    SkipCreation,
}

/// <summary>
/// The reference of one verse, as the story names it: chapter and verse. The story holds references only, never the
/// text of a verse (design, section 2.1, rule 1).
/// </summary>
/// <param name="Chapter">The chapter of Genesis, from 1.</param>
/// <param name="Verse">The verse within the chapter, from 1.</param>
public readonly record struct ScriptureRef(int Chapter, int Verse)
{
    /// <summary>The reference as the canonical text marks it: chapter, a colon, verse.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Chapter}:{Verse}");
}

/// <summary>
/// The state of the story (design, section 3.1): immutable, and saved. It holds choices from fixed lists and
/// numbers only.
/// </summary>
public sealed record StoryState
{
    /// <summary>Whom the player plays; null until the character is chosen.</summary>
    public PlayerCharacter? Character { get; init; }

    /// <summary>The chapter.</summary>
    public StoryChapter Chapter { get; init; }

    /// <summary>The beat.</summary>
    public StoryBeat Beat { get; init; }

    /// <summary>
    /// Where the beat stands, from 0; -1 when nothing of it is shown yet. In a day of creation it is the Scripture
    /// card shown; in a beat of the garden (<see cref="GardenStory"/>) it is the step of the beat.
    /// </summary>
    public int Card { get; init; } = -1;

    /// <summary>Whether the days of creation were watched to the end once: only then may they be skipped.</summary>
    public bool CreationWatched { get; init; }

    /// <summary>What the game calls the man: "The man" until Genesis 2:19 is shown, "Adam" from then on.</summary>
    public ManLabel Man { get; init; }

    /// <summary>
    /// The kind-names chosen so far, one for each animal named, in the order the animals are brought
    /// (<see cref="AnimalRoster"/>): each is the id of one of the three kind-names of its animal, never typed text.
    /// </summary>
    public IReadOnlyList<string> NamedAnimals { get; init; } = [];

    /// <summary>
    /// Whether the woman is in the garden. On the man's path she is not made before Genesis 2:22, which is not built
    /// yet: through all the beats of this path she is absent.
    /// </summary>
    public bool WomanCreated => Character == PlayerCharacter.Woman || Chapter == StoryChapter.GardenLife;

    /// <summary>The state at the title, before a character is chosen.</summary>
    /// <param name="creationWatched">Whether the days of creation were watched to the end once.</param>
    public static StoryState AtTitle(bool creationWatched) => new() { CreationWatched = creationWatched };

    /// <summary>The state a saved game holds. A beat is resumed from its beginning.</summary>
    /// <param name="save">The saved game.</param>
    public static StoryState FromSave(SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var state = new StoryState
        {
            Character = save.Character,
            Chapter = save.Chapter,
            Beat = save.Beat,
            CreationWatched = save.CreationWatched,
            Man = StoryMachine.ManAt(save.Beat),
            NamedAnimals = [.. save.NamedAnimals],
        };
        return StoryMachine.Resume(state).State;
    }

    /// <summary>Two states are equal when everything they hold is, the kind-names one by one.</summary>
    /// <param name="other">The other state.</param>
    public bool Equals(StoryState? other) => other is not null
        && Character == other.Character && Chapter == other.Chapter && Beat == other.Beat && Card == other.Card
        && CreationWatched == other.CreationWatched && Man == other.Man && NamedAnimals.SequenceEqual(other.NamedAnimals);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Character, Chapter, Beat, Card, CreationWatched, Man, NamedAnimals.Count);
}
