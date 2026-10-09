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

/// <summary>Something the game does in answer (design, section 3.1).</summary>
public abstract record StoryEffect;

/// <summary>Show a verse on a Scripture card, with its reference.</summary>
/// <param name="Ref">The verse.</param>
/// <param name="Speaker">Who speaks in the verse, or null when nobody does.</param>
public sealed record ShowScripture(ScriptureRef Ref, StorySpeaker? Speaker) : StoryEffect;

/// <summary>Show the picture of a day of creation as far as the verses shown so far tell it.</summary>
/// <param name="Layers">The layers of the picture, the farthest first.</param>
public sealed record ShowScene(IReadOnlyList<SceneLayer> Layers) : StoryEffect;

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
