using AdamEve.Content;
using AdamEve.Core.Game;
using AdamEve.Core.Input;
using AdamEve.Core.Saves;
using AdamEve.Core.World;
using System.Runtime.InteropServices.JavaScript;

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
    private bool rendererChosen;

    /// <summary>Text or a menu changed.</summary>
    public event Action? Changed;

    /// <summary>The settings of the player.</summary>
    public GameSettings Settings { get; private set; } = new();

    /// <summary>Whether the menu with the settings is open.</summary>
    public bool MenuOpen { get; private set; }

    /// <summary>Whether the saved game could not be read and the player has not put the notice away.</summary>
    public bool SaveUnreadable { get; private set; }

    /// <summary>The class of the page that sets the size of the game's text.</summary>
    public string TextClass => Settings.TextSize switch
    {
        TextSize.S => "text-s",
        TextSize.L => "text-l",
        TextSize.XL => "text-xl",
        _ => "text-m",
    };

    /// <summary>The renderer the player asked for (the trial of docs/spike-threejs.md): the canvas unless chosen.</summary>
    public RendererKind Renderer { get; private set; }

    /// <summary>The renderer that draws the garden now: the canvas when the one asked for could not start.</summary>
    public RendererKind ActiveRenderer { get; private set; }

    /// <summary>Why the renderer asked for does not draw ("webgl-unavailable", "load-failed"); null when it does.</summary>
    public string? RendererFallback { get; private set; }

    /// <summary>
    /// Counts the canvases of the garden's page: a canvas that has drawn with one renderer cannot draw with the
    /// other, so the page makes a new one when this changes.
    /// </summary>
    public int CanvasGeneration { get; private set; }

    /// <summary>Whether the garden's page is to start the game on its canvas after it has rendered.</summary>
    public bool StartPending { get; private set; }

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

    /// <summary>
    /// Reads which renderer is asked for: by the address of the page (<c>?renderer=three</c>), otherwise as the tab
    /// kept it. What the address asks for is kept for the tab.
    /// </summary>
    /// <param name="address">The address of the page; null on a page whose address chooses nothing.</param>
    public void LoadRenderer(string? address = null)
    {
        var asked = RendererTrial.FromAddress(address);
        if (asked is null && rendererChosen)
        {
            return;
        }

        Renderer = asked ?? RendererTrial.Parse(ReadSession(RendererTrial.SessionKey)) ?? RendererKind.Canvas;
        rendererChosen = true;
        if (asked is not null)
        {
            WriteSession(RendererTrial.SessionKey, RendererTrial.NameOf(Renderer));
        }
    }

    /// <summary>
    /// Chooses the renderer and keeps the choice for the tab. In the garden the game stops, the page makes a new
    /// canvas and starts the game on it with the renderer chosen.
    /// </summary>
    /// <param name="kind">The renderer.</param>
    public void SetRenderer(RendererKind kind)
    {
        rendererChosen = true;
        if (kind == Renderer && RendererFallback is null)
        {
            return;
        }

        Renderer = kind;
        WriteSession(RendererTrial.SessionKey, RendererTrial.NameOf(kind));
        if (game is not null)
        {
            Stop();
            CanvasGeneration++;
            StartPending = true;
        }

        Changed?.Invoke();
    }

    /// <summary>Starts the garden on the canvas of the page: resumes the saved game or starts a new one.</summary>
    public async Task StartAsync()
    {
        // After another renderer was chosen the menu it was chosen in stays open.
        var again = StartPending;
        StartPending = false;
        if (!content.IsLoaded)
        {
            return;
        }

        LoadSettings();
        LoadRenderer();
        var garden = content.Content.Garden;
        var saved = LocalStorageSaveStore.Load(garden.Map);
        SaveUnreadable = saved.State == SaveState.Corrupt;
        MenuOpen = again && MenuOpen;
        var started = new GardenGame(garden.Map, garden.Adam, garden.Woman, garden.Animations, saved.Save) { Paused = SaveUnreadable || MenuOpen };
        game = started;

        await GameInterop.ImportAsync();
        JsAudio.SetEnabled(Settings.Sound);
        GameInterop.OnFrame = Frame;
        var tiles = garden.Map.ToKindNumbers();
        var ground = GroundNumbers();
        var atlas = started.Atlas.ToNumbers();
        string[] verdicts = [.. started.VerdictNames];
        ActiveRenderer = RendererKind.Canvas;
        RendererFallback = null;
        if (Renderer == RendererKind.Three)
        {
            // The trial: the same numbers go to the other module. Where it cannot start, the canvas draws.
            try
            {
                await GameInterop.ImportThreeAsync();
                if (!ReferenceEquals(game, started))
                {
                    // The player left the garden, or chose again, while the module loaded.
                    return;
                }

                var attached = await GameInterop.AttachThree(
                    new ArraySegment<double>(list), new ArraySegment<double>(input), tiles, ground, atlas, verdicts,
                    garden.Map.Width, garden.Map.Height, garden.Map.TileSize, PlaceholderArt.Backdrop);
                if (attached)
                {
                    ActiveRenderer = RendererKind.Three;
                }
                else
                {
                    RendererFallback = "webgl-unavailable";
                }
            }
            catch (JSException)
            {
                RendererFallback = "load-failed";
            }
        }

        if (ActiveRenderer == RendererKind.Canvas)
        {
            await GameInterop.Attach(
                new ArraySegment<double>(list),
                new ArraySegment<double>(input),
                tiles,
                ground,
                atlas,
                verdicts,
                garden.Map.Width,
                garden.Map.Height,
                garden.Map.TileSize,
                PlaceholderArt.Backdrop);
        }

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
        if (GameInterop.ThreeImported)
        {
            GameInterop.DetachThree();
        }

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
        if (GameInterop.Imported)
        {
            // At the title the sound module is not loaded yet: the garden sets it from the kept setting when it starts.
            JsAudio.SetEnabled(enabled);
        }
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

    private static string? ReadSession(string key)
    {
        try
        {
            return GameInterop.GetSessionItem(key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private static void WriteSession(string key, string value)
    {
        try
        {
            GameInterop.SetSessionItem(key, value);
        }
        catch (JSException)
        {
            // Storage is refused: the choice lasts as long as the page.
        }
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
