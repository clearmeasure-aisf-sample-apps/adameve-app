using AdamEve.Content;
using AdamEve.Content.Scripture;
using AdamEve.Core.Game;
using AdamEve.Core.Rigs;
using AdamEve.Core.Saves;
using AdamEve.Core.Story;
using AdamEve.Core.World;

namespace AdamEve.Client.Game;

/// <summary>One entry of the garden journal: an animal with the kind-name Adam gave it.</summary>
/// <param name="Animal">The animal.</param>
/// <param name="KindName">The kind-name chosen, with its meaning.</param>
public sealed record JournalEntry(Animal Animal, KindName KindName);

/// <summary>
/// The story that plays in the garden: the man's path before the woman, beats B8 to B13. It hands what the player
/// does to the story machine in Core, keeps what the machine answers for the page to show, puts the things of the
/// story into the garden and saves when the machine says so. It decides nothing of the story itself (design,
/// sections 3.1 and 7.2).
/// </summary>
public sealed class GardenStorySession(ContentLoadResult content)
{
    private const string SaplingKey = "sapling";
    private const string BranchKey = "branch";
    private const string MateSuffix = "-mate";

    private GardenGame? game;
    private StoryPlaces? places;
    private TileMap? map;
    private StillFigure? man;

    /// <summary>The page is to show something else.</summary>
    public event Action? Changed;

    /// <summary>The state of the story.</summary>
    public StoryState State { get; private set; } = StoryState.AtTitle(creationWatched: false);

    /// <summary>The verse on the Scripture card, as parsed from the canonical text; null when no card is shown.</summary>
    public Verse? Card { get; private set; }

    /// <summary>Who speaks in the verse of the card, or null.</summary>
    public StorySpeaker? Speaker { get; private set; }

    /// <summary>The layers of the picture of the formation, the farthest first; none outside beat B8.</summary>
    public IReadOnlyList<FormationLayer> Formation { get; private set; } = [];

    /// <summary>What the player is to do, or null.</summary>
    public StoryTask? Task { get; private set; }

    /// <summary>The animal that was brought and waits for its kind-name, or null.</summary>
    public Animal? Brought { get; private set; }

    /// <summary>The kind-name whose meaning the player asked for, or null.</summary>
    public KindName? Explained { get; private set; }

    /// <summary>Whether the journal is open.</summary>
    public bool JournalOpen { get; private set; }

    /// <summary>Whether the story of the garden plays: the man's path, beats B8 to B13, and what follows them.</summary>
    public bool Plays => StoryMachine.PlaysTheGarden(State) || State is { Character: PlayerCharacter.Adam, Chapter: StoryChapter.WomanFormed };

    /// <summary>Whether the world waits: a card, the naming or the journal is open.</summary>
    public bool Blocks => Card is not null || Brought is not null || JournalOpen;

    /// <summary>Counts what the page shows, so that it can put the keyboard on the control of each new thing.</summary>
    public int Shown { get; private set; }

    /// <summary>The journal: each animal named so far with the kind-name Adam gave it, in the order they were named.</summary>
    public IReadOnlyList<JournalEntry> Journal => content.IsLoaded
        ? [.. State.NamedAnimals.Select(id => content.Content.Animals.Named(id)).OfType<(Animal Animal, KindName KindName)>().Select(named => new JournalEntry(named.Animal, named.KindName))]
        : [];

    /// <summary>The man as the picture of the formation shows him: the rig of the garden, standing, whole, judged by the M1 check.</summary>
    public StillFigure? Man => man ??= content.IsLoaded
        ? StillFigure.Compose(content.Content.Garden.Adam, content.Content.Garden.Animations.First(animation => animation.Id == "idle"), Facing.S, Covering.None, scale: 2)
        : null;

    /// <summary>The M1 verdict of the picture of the formation: of the man when it shows him, "ok" otherwise.</summary>
    public string FormationConcealment => Formation.Contains(FormationLayer.Man) && Man is { } figure ? figure.Concealment : StillFigure.Ok;

    /// <summary>The garden started: the story goes on from the saved game, each beat from its beginning.</summary>
    /// <param name="started">The game of the garden.</param>
    /// <param name="save">The saved game, or null when there is none.</param>
    public void Begin(GardenGame started, SaveGame? save)
    {
        ArgumentNullException.ThrowIfNull(started);
        game = started;
        map = content.IsLoaded ? content.Content.Garden.Map : null;
        places = map is null ? null : new StoryPlaces(map);
        JournalOpen = false;
        if (save is null)
        {
            State = StoryState.AtTitle(creationWatched: false);
            Show([]);
            return;
        }

        var step = StoryMachine.Resume(StoryState.FromSave(save));
        State = step.State;
        Show(step.Effects);
    }

    /// <summary>The player left the garden.</summary>
    public void End()
    {
        game = null;
        JournalOpen = false;
    }

    /// <summary>The game as it is saved: with the story as it stands now.</summary>
    /// <param name="playing">The game of the garden.</param>
    public SaveGame SaveOf(GardenGame playing)
    {
        ArgumentNullException.ThrowIfNull(playing);
        return State.Character is null ? playing.ToSave() : playing.ToSave(State);
    }

    /// <summary>The player's one action: the next page, when a card is shown.</summary>
    public void Primary()
    {
        if (Card is not null && !JournalOpen)
        {
            Take(StoryMachine.Apply(State, new DialogueAdvanced()));
        }
    }

    /// <summary>The player chose a kind-name for the animal that was brought.</summary>
    /// <param name="kindName">The kind-name.</param>
    public void Name(KindName kindName)
    {
        ArgumentNullException.ThrowIfNull(kindName);
        if (Brought is not null && !JournalOpen)
        {
            Take(StoryMachine.Apply(State, new AnimalNamed(kindName.Id)));
        }
    }

    /// <summary>The player asked what a kind-name means, or put the meaning away.</summary>
    /// <param name="kindName">The kind-name, or null.</param>
    public void Explain(KindName? kindName)
    {
        Explained = kindName;
        Changed?.Invoke();
    }

    /// <summary>Opens the journal, or closes it.</summary>
    public void ToggleJournal()
    {
        JournalOpen = !JournalOpen;
        Changed?.Invoke();
    }

    /// <summary>After a frame: the story learns where the player stands, and that the action key was pressed.</summary>
    /// <param name="events">What the frame changed.</param>
    public void AfterFrame(FrameEvents events)
    {
        if (game is null || !Plays)
        {
            return;
        }

        if ((events & FrameEvents.Action) != 0)
        {
            Primary();
        }

        if (places is not null && !game.Moving && GardenStory.StepOf(State) is { Kind: GardenStepKind.Reach } step && places.Reached(step.Area, game.PlayerTile))
        {
            Take(StoryMachine.Apply(State, new ReachedArea(step.Area)));
        }
    }

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
        Formation = [];
        Task = null;
        Brought = null;
        Explained = null;
        var save = false;
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case ShowScripture scripture when content.IsLoaded:
                    Card = content.Content.Scripture.Find(scripture.Ref);
                    Speaker = scripture.Speaker;
                    break;
                case ShowFormation formation:
                    Formation = formation.Layers;
                    break;
                case ShowTask task:
                    Task = task.Task;
                    break;
                case BringAnimal bring when content.IsLoaded:
                    Brought = content.Content.Animals.Find(bring.Animal);
                    break;
                case SaveStory:
                    save = true;
                    break;
            }
        }

        Shown++;
        if (game is not null)
        {
            game.OtherPresent = State.Character is null || State.WomanCreated || State.Character == PlayerCharacter.Woman;
            PlaceThings(game);
            if (save)
            {
                LocalStorageSaveStore.Save(SaveOf(game));
            }
        }

        Changed?.Invoke();
    }

    // What the story stands in the garden: the sapling and the branch of beat B10, each animal that has its name at
    // its place (with its mate, once all are named), and the animal that was brought, beside the man.
    private void PlaceThings(GardenGame playing)
    {
        var things = playing.Things;
        if (places is null || map is null || !Plays)
        {
            things.Clear();
            return;
        }

        var sapling = GardenStory.SaplingOf(State);
        if (sapling == 0)
        {
            things.Remove(SaplingKey);
        }
        else if (things.Find(SaplingKey) is { } standing)
        {
            standing.Art = sapling == 1 ? ThingArt.Sapling : ThingArt.SaplingWatered;
        }
        else
        {
            things.Place(new GardenThing(SaplingKey, sapling == 1 ? ThingArt.Sapling : ThingArt.SaplingWatered, places.Sapling.X, places.Sapling.Y));
        }

        if (!GardenStory.BranchLies(State))
        {
            things.Remove(BranchKey);
        }
        else if (things.Find(BranchKey) is null)
        {
            things.Place(new GardenThing(BranchKey, ThingArt.Branch, places.Branch.X, places.Branch.Y));
        }

        var pairs = GardenStory.InPairs(State);
        for (var index = 0; index < AnimalRoster.Count; index++)
        {
            var animal = AnimalRoster.Animals[index];
            var place = places.AnimalPlaces[index];
            var isBrought = Brought?.Id == animal.Id;
            if (index >= State.NamedAnimals.Count && !isBrought)
            {
                things.Remove(animal.Id);
                things.Remove(animal.Id + MateSuffix);
                continue;
            }

            var thing = things.Find(animal.Id);
            if (isBrought)
            {
                // Brought to the man: it comes from the side where there is ground, and stands before him.
                var side = map.IsWalkable(new TilePos(playing.PlayerTile.X + 1, playing.PlayerTile.Y)) && map.IsWalkable(new TilePos(playing.PlayerTile.X + 2, playing.PlayerTile.Y)) ? 1 : -1;
                thing ??= Placed(things, new GardenThing(animal.Id, index, playing.PlayerTile.X + (side * 5), playing.PlayerTile.Y));
                thing.TargetX = playing.PlayerTile.X + (side * 1.5);
                thing.TargetY = playing.PlayerTile.Y;
                continue;
            }

            thing ??= Placed(things, new GardenThing(animal.Id, index, place.X, place.Y));
            thing.TargetX = place.X;
            thing.TargetY = place.Y;
            if (pairs && things.Find(animal.Id + MateSuffix) is null)
            {
                things.Place(new GardenThing(animal.Id + MateSuffix, index, place.X + 1, place.Y) { LooksWest = true });
            }
        }
    }

    private static GardenThing Placed(GardenThings things, GardenThing thing)
    {
        things.Place(thing);
        return thing;
    }
}
