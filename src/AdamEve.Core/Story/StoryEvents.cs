using AdamEve.Core.Saves;

namespace AdamEve.Core.Story;

/// <summary>Something the player did (design, section 3.1).</summary>
public abstract record StoryEvent;

/// <summary>The player chose a character at the title.</summary>
/// <param name="Character">The character.</param>
public sealed record CharacterChosen(PlayerCharacter Character) : StoryEvent;

/// <summary>The player turned the page: the next Scripture card.</summary>
public sealed record DialogueAdvanced : StoryEvent;

/// <summary>
/// The player made the one gesture of a day of creation. It uncovers what God made: the story then shows the card
/// on which God speaks. The player makes nothing.
/// </summary>
public sealed record Revealed : StoryEvent;

/// <summary>The player chose one of the choices offered.</summary>
/// <param name="Choice">The choice.</param>
public sealed record ChoiceMade(StoryChoice Choice) : StoryEvent;

/// <summary>The player came to a place the story waits for (design, section 3.1).</summary>
/// <param name="Area">The place.</param>
public sealed record ReachedArea(StoryArea Area) : StoryEvent;

/// <summary>
/// The player chose a kind-name for the animal that was brought (design, section 3.5, decision D9). Any of the
/// three of that animal is accepted; anything else is refused.
/// </summary>
/// <param name="KindName">The id of the kind-name.</param>
public sealed record AnimalNamed(string KindName) : StoryEvent;

/// <summary>Something the game does in answer (design, section 3.1).</summary>
public abstract record StoryEffect;

/// <summary>Show a verse on a Scripture card, with its reference.</summary>
/// <param name="Ref">The verse.</param>
/// <param name="Speaker">Who speaks in the verse, or null when nobody does.</param>
public sealed record ShowScripture(ScriptureRef Ref, StorySpeaker? Speaker) : StoryEffect;

/// <summary>Show the picture of a day of creation as far as the verses shown so far tell it.</summary>
/// <param name="Layers">The layers of the picture, the farthest first.</param>
public sealed record ShowScene(IReadOnlyList<SceneLayer> Layers) : StoryEffect;

/// <summary>
/// Show the picture of the formation of the man (Genesis 2:4 to 2:7) as far as the verses shown so far tell it.
/// </summary>
/// <param name="Layers">The layers of the picture, the farthest first.</param>
public sealed record ShowFormation(IReadOnlyList<FormationLayer> Layers) : StoryEffect;

/// <summary>Say what the player is to do now: a line of narration, never Scripture.</summary>
/// <param name="Task">The task.</param>
public sealed record ShowTask(StoryTask Task) : StoryEffect;

/// <summary>Change what the game calls the man, as the verse shown with it does (design, decision D3).</summary>
/// <param name="Man">The label from now on.</param>
public sealed record SetLabel(ManLabel Man) : StoryEffect;

/// <summary>Bring an animal to Adam, to see what he would call it (Genesis 2:19), and offer its three kind-names.</summary>
/// <param name="Animal">The id of the animal.</param>
/// <param name="KindNames">The ids of its three kind-names.</param>
public sealed record BringAnimal(string Animal, IReadOnlyList<string> KindNames) : StoryEffect;

/// <summary>Write the kind-name Adam gave an animal into the journal.</summary>
/// <param name="Animal">The id of the animal.</param>
/// <param name="KindName">The id of the kind-name chosen.</param>
public sealed record AddJournal(string Animal, string KindName) : StoryEffect;

/// <summary>Stand the animals in pairs (design, section 2.2, beat B13).</summary>
public sealed record ShowPairs : StoryEffect;

/// <summary>Wait for the one gesture of the day: the next card is shown when the player makes it.</summary>
public sealed record AwaitReveal : StoryEffect;

/// <summary>Offer choices.</summary>
/// <param name="Choices">The choices, never more than four.</param>
public sealed record OfferChoices(IReadOnlyList<StoryChoice> Choices) : StoryEffect;

/// <summary>Save the game: a beat began.</summary>
public sealed record SaveStory : StoryEffect;

/// <summary>What an event led to.</summary>
/// <param name="State">The state after the event.</param>
/// <param name="Effects">What the game does, in order. Empty when the event was refused.</param>
public sealed record StoryStep(StoryState State, IReadOnlyList<StoryEffect> Effects);
