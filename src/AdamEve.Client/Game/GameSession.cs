using AdamEve.Content;
using AdamEve.Core.Game;
using AdamEve.Core.Input;
using AdamEve.Core.Saves;
using AdamEve.Core.World;

namespace AdamEve.Client.Game;

/// <summary>
/// The game that is playing in this tab. It carries each frame from the canvas module to the game in Core and
/// raises <see cref="Changed"/> only when text or a menu changes: components render then, never for each frame
/// (design, section 7.2).
/// </summary>
public sealed class GameSession(ContentLoadResult content)
{
    // The render list and the input block: allocated once and pinned, so the canvas module reads and writes the
    // game's memory itself, with one call a frame.
    private readonly double[] list = GC.AllocateArray<double>(RenderList.Length, pinned: true);
    private readonly double[] input = GC.AllocateArray<double>(InputBlock.Length, pinned: true);
    private GardenGame? game;
    private bool settingsLoaded;

    /// <summary>Text or a menu changed.</summary>
    public event Action? Changed;

    /// <summary>The settings of the player.</summary>
    public GameSettings Settings { get; private set; } = new();

    /// <summary>Whether the menu with the settings is open.</summary>
    public bool MenuOpen { get; private set; }

    /// <summary>Whether the saved game could not be read and the player has not put the notice away.</summary>
    public bool SaveUnreadable { get; private set; }

    /// <summary>The id of the region the player is in, or null.</summary>
    public string? RegionId => game?.RegionId;

    /// <summary>Reads the settings, once.</summary>
    public void LoadSettings()
    {
        if (!settingsLoaded)
        {
            Settings = LocalStorageSaveStore.LoadSettings();
            settingsLoaded = true;
        }
    }

    /// <summary>Starts the garden on the canvas of the page: resumes the saved game or starts a new one.</summary>
    public async Task StartAsync()
    {
        if (!content.IsLoaded)
        {
            return;
        }

        LoadSettings();
        var garden = content.Content.Garden;
        var saved = LocalStorageSaveStore.Load(garden.Map);
        SaveUnreadable = saved.State == SaveState.Corrupt;
        MenuOpen = false;
        game = new GardenGame(garden.Map, garden.Adam, garden.Woman, garden.Animations, saved.Save) { Paused = SaveUnreadable };

        await GameInterop.ImportAsync();
        JsAudio.SetEnabled(Settings.Sound);
        GameInterop.OnFrame = Frame;
        await GameInterop.Attach(
            new ArraySegment<double>(list),
            new ArraySegment<double>(input),
            garden.Map.ToKindNumbers(),
            GroundNumbers(),
            game.Atlas.ToNumbers(),
            [.. game.VerdictNames],
            garden.Map.Width,
            garden.Map.Height,
            garden.Map.TileSize,
            PlaceholderArt.Backdrop);
        Changed?.Invoke();
    }

    /// <summary>Stops the canvas: the player left the garden's page.</summary>
    public void Stop()
    {
        if (game is null)
        {
            return;
        }

        GameInterop.OnFrame = null;
        GameInterop.Detach();
        game = null;
    }

    /// <summary>Opens the menu, or closes it.</summary>
    public void ToggleMenu()
    {
        MenuOpen = !MenuOpen;
        Pause();
        Changed?.Invoke();
    }

    /// <summary>Puts away the notice that the saved game could not be read: a new game goes on.</summary>
    public void DismissNotice()
    {
        SaveUnreadable = false;
        Pause();
        Changed?.Invoke();
    }

    /// <summary>Sets the size of the game's text and keeps it.</summary>
    /// <param name="size">The size.</param>
    public void SetTextSize(TextSize size) => Keep(Settings with { TextSize = size });

    /// <summary>Turns sound on or off and keeps it.</summary>
    /// <param name="enabled">Whether sound plays.</param>
    public void SetSound(bool enabled)
    {
        Keep(Settings with { Sound = enabled });
        JsAudio.SetEnabled(enabled);
    }

    private static double[] GroundNumbers()
    {
        var numbers = new double[TileKinds.Count * 4];
        for (var kind = 0; kind < TileKinds.Count; kind++)
        {
            var style = PlaceholderArt.GroundOf((TileKind)kind);
            numbers[kind * 4] = style.Colour;
            numbers[(kind * 4) + 1] = style.AlternateColour;
            numbers[(kind * 4) + 2] = style.MarkColour;
            numbers[(kind * 4) + 3] = style.MarkSize;
        }

        return numbers;
    }

    private void Keep(GameSettings settings)
    {
        Settings = settings;
        LocalStorageSaveStore.SaveSettings(settings);
        Changed?.Invoke();
    }

    private void Pause()
    {
        if (game is not null)
        {
            game.Paused = MenuOpen || SaveUnreadable;
        }
    }

    private void Frame(double nowMilliseconds)
    {
        if (game is null)
        {
            return;
        }

        var events = game.Frame(nowMilliseconds, input, list);
        if ((events & FrameEvents.Stopped) != 0)
        {
            LocalStorageSaveStore.Save(game.ToSave());
        }

        if ((events & FrameEvents.Menu) != 0)
        {
            ToggleMenu();
        }
        else if ((events & FrameEvents.RegionChanged) != 0)
        {
            Changed?.Invoke();
        }
    }
}
