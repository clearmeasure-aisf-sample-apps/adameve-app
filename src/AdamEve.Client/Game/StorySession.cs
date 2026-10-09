using AdamEve.Content;
using AdamEve.Content.Scripture;
using AdamEve.Core.Rigs;
using AdamEve.Core.Saves;
using AdamEve.Core.Story;
using AdamEve.Core.World;

namespace AdamEve.Client.Game;

/// <summary>
/// The story that is playing in this tab: the title and the days of creation. It hands what the player does to the
/// story machine in Core, keeps what the machine answers for the page to show, and saves when the machine says so.
/// It decides nothing of the story itself (design, sections 3.1 and 7.2).
/// </summary>
public sealed class StorySession(ContentLoadResult content)
{
    private IReadOnlyList<StillFigure>? selectFigures;
    private IReadOnlyList<StillFigure>? farFigures;

    /// <summary>The page is to show something else.</summary>
    public event Action? Changed;

    /// <summary>The state of the story.</summary>
    public StoryState State { get; private set; } = StoryState.AtTitle(creationWatched: false);

    /// <summary>The verse on the Scripture card, as parsed from the canonical text; null when no card is shown.</summary>
    public Verse? Card { get; private set; }

    /// <summary>Who speaks in the verse of the card, or null.</summary>
    public StorySpeaker? Speaker { get; private set; }

    /// <summary>The layers of the picture, the farthest first.</summary>
    public IReadOnlyList<SceneLayer> Layers { get; private set; } = [];

    /// <summary>Whether the story waits for the one gesture of the day.</summary>
    public bool AwaitsReveal { get; private set; }

    /// <summary>Whether the days of creation may be left: only after a first completion.</summary>
    public bool SkipOffered { get; private set; }

    /// <summary>The chapter of the saved game there is to go on with, or null.</summary>
    public StoryChapter? SavedChapter { get; private set; }

    /// <summary>Adam and the woman as the character select draws them: the rigs of the garden, standing, judged by the M1 check.</summary>
    public IReadOnlyList<StillFigure> SelectFigures => selectFigures ??= Compose(scale: 2, light: null);

    /// <summary>The two figures of light of the sixth day: the same rigs in one pale colour, judged by the same check.</summary>
    public IReadOnlyList<StillFigure> FarFigures => farFigures ??= Compose(scale: 1, light: CreationPicture.FigureLight);

    /// <summary>The M1 verdict of the picture: of the figures it draws, or "ok" when it draws no character.</summary>
    public string Concealment => Layers.Contains(SceneLayer.Figures) ? StillFigure.VerdictOf(FarFigures) : StillFigure.Ok;

    /// <summary>The story stands at the title: beat B0.</summary>
    public void OpenTitle()
    {
        var saved = content.IsLoaded ? LocalStorageSaveStore.Peek(content.Content.Garden.Map).Save : null;
        SavedChapter = saved?.Chapter;
        State = StoryState.AtTitle(saved?.CreationWatched ?? false);
        Show([]);
    }

    /// <summary>The player chose a character: a new game begins.</summary>
    /// <param name="character">The character.</param>
    public void Choose(PlayerCharacter character)
    {
        if (!content.IsLoaded)
        {
            return;
        }

        var saved = LocalStorageSaveStore.Load(content.Content.Garden.Map).Save;
        State = StoryState.AtTitle(saved?.CreationWatched ?? false);
        Take(StoryMachine.Apply(State, new CharacterChosen(character)));
    }

    /// <summary>Goes on with the saved game, from the beginning of its beat.</summary>
    /// <returns>Whether there is a saved game.</returns>
    public bool Resume()
    {
        if (!content.IsLoaded || LocalStorageSaveStore.Load(content.Content.Garden.Map).Save is not { } saved)
        {
            return false;
        }

        var step = StoryMachine.Resume(StoryState.FromSave(saved));
        State = step.State;
        Show(step.Effects);
        return true;
    }

    /// <summary>The player turned the page.</summary>
    public void TurnThePage() => Take(StoryMachine.Apply(State, new DialogueAdvanced()));

    /// <summary>The player made the one gesture of the day.</summary>
    public void Reveal() => Take(StoryMachine.Apply(State, new Revealed()));

    /// <summary>The player left the days of creation.</summary>
    public void Skip() => Take(StoryMachine.Apply(State, new ChoiceMade(StoryChoice.SkipCreation)));

    private void Take(StoryStep step)
    {
        if (step.Effects.Count == 0)
        {
            // The machine refused the event: nothing changes.
            return;
        }

        State = step.State;
        Show(step.Effects);
    }

    private void Show(IReadOnlyList<StoryEffect> effects)
    {
        Card = null;
        Speaker = null;
        Layers = [];
        AwaitsReveal = false;
        SkipOffered = false;
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case ShowScene scene:
                    Layers = scene.Layers;
                    break;
                case ShowScripture scripture when content.IsLoaded:
                    Card = content.Content.Scripture.Find(scripture.Ref);
                    Speaker = scripture.Speaker;
                    break;
                case AwaitReveal:
                    AwaitsReveal = true;
                    break;
                case OfferChoices offer:
                    SkipOffered = offer.Choices.Contains(StoryChoice.SkipCreation);
                    break;
                case SaveStory when content.IsLoaded && State.Character is { } character:
                    var spawn = content.Content.Garden.Map.Spawn(character == PlayerCharacter.Adam ? "adam" : "woman");
                    LocalStorageSaveStore.Save(SaveGame.Of(State, spawn));
                    break;
            }
        }

        Changed?.Invoke();
    }

    private IReadOnlyList<StillFigure> Compose(double scale, int? light)
    {
        if (!content.IsLoaded)
        {
            return [];
        }

        var garden = content.Content.Garden;
        var idle = garden.Animations.First(animation => animation.Id == "idle");
        return
        [
            StillFigure.Compose(garden.Adam, idle, Facing.S, Covering.None, scale, light),
            StillFigure.Compose(garden.Woman, idle, Facing.S, Covering.None, scale, light),
        ];
    }
}
