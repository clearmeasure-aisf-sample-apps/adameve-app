using System.Globalization;
using AdamEve.Core.Saves;

namespace AdamEve.Core.Story;

/// <summary>
/// The chapters of the story (design, section 3.1). This slice reaches the first three; the chapters of the later
/// slices are added with them.
/// </summary>
public enum StoryChapter
{
    /// <summary>The title and the character select: beat B0.</summary>
    Title,

    /// <summary>The days of creation, Genesis 1:1 to 2:3: beats B1 to B7.</summary>
    Creation,

    /// <summary>What follows Genesis 2:3: beat B8 and after. Until its slice exists, the walk in the garden stands here.</summary>
    Formation,
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
}

/// <summary>Who speaks on a Scripture card. The only voice of this slice is God's, and it speaks Scripture only.</summary>
public enum StorySpeaker
{
    /// <summary>God, as chapter 1 of Genesis names him (design, section 1).</summary>
    God,
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

    /// <summary>Which Scripture card of the beat is shown, from 0; -1 when none is shown yet.</summary>
    public int Card { get; init; } = -1;

    /// <summary>Whether the days of creation were watched to the end once: only then may they be skipped.</summary>
    public bool CreationWatched { get; init; }

    /// <summary>The state at the title, before a character is chosen.</summary>
    /// <param name="creationWatched">Whether the days of creation were watched to the end once.</param>
    public static StoryState AtTitle(bool creationWatched) => new() { CreationWatched = creationWatched };

    /// <summary>The state a saved game holds. A beat is resumed from its beginning.</summary>
    /// <param name="save">The saved game.</param>
    public static StoryState FromSave(SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var state = new StoryState { Character = save.Character, Chapter = save.Chapter, Beat = save.Beat, CreationWatched = save.CreationWatched };
        return StoryMachine.Resume(state).State;
    }
}
