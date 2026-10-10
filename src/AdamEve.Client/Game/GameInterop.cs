using System.Runtime.InteropServices.JavaScript;

namespace AdamEve.Client.Game;

/// <summary>
/// The whole border between the game and the browser (design, section 7.1): the renderer module calls
/// <see cref="Frame"/> once for each frame and reads the render list from the game's memory; the game calls the
/// browser only to start and stop the renderer, to set the sound, and to read and write <c>localStorage</c>. No
/// module of the game is loaded before the garden: the title and the reader ask for none of them.
/// </summary>
public static partial class GameInterop
{
    private const string CanvasModule = "render";
    private const string AudioModule = "audio";
    private const string ThreeModule = "render-three";

    private static bool imported;
    private static bool canvasImported;
    private static bool threeImported;

    /// <summary>Whether the sound module is loaded: before that, nothing of it can be called.</summary>
    internal static bool Imported => imported;

    /// <summary>Whether the module of the canvas renderer is loaded: before that, nothing of it can be called.</summary>
    internal static bool CanvasImported => canvasImported;

    /// <summary>Whether the module of the Three.js renderer is loaded: before that, nothing of it can be called.</summary>
    internal static bool ThreeImported => threeImported;

    /// <summary>What a frame runs: the session that is playing, or nothing.</summary>
    internal static Action<double>? OnFrame { get; set; }

    /// <summary>What runs when the Three.js renderer lost its WebGL context for good: the session that is playing, or nothing.</summary>
    internal static Action? OnRendererLost { get; set; }

    /// <summary>One frame of the game. The renderer module calls it from <c>requestAnimationFrame</c>.</summary>
    /// <param name="nowMs">The time of the frame, as the browser gives it.</param>
    [JSExport]
    public static void Frame(double nowMs) => OnFrame?.Invoke(nowMs);

    /// <summary>
    /// The Three.js renderer lost its WebGL context and the browser did not give it back: the game goes on with
    /// the canvas renderer. The renderer module calls it.
    /// </summary>
    [JSExport]
    public static void RendererLost() => OnRendererLost?.Invoke();

    /// <summary>Loads the sound module from the site itself, once.</summary>
    internal static async Task ImportAsync()
    {
        if (imported)
        {
            return;
        }

        await JSHost.ImportAsync(AudioModule, "../js/audio.js");
        imported = true;
    }

    /// <summary>
    /// Loads the module of the canvas renderer from the site itself, once, and only when it has to draw: where
    /// WebGL is not to be had, or the address of the garden asks for it.
    /// </summary>
    internal static async Task ImportCanvasAsync()
    {
        if (canvasImported)
        {
            return;
        }

        await JSHost.ImportAsync(CanvasModule, "../js/render.js");
        canvasImported = true;
    }

    /// <summary>Starts the canvas renderer on the canvas of the page.</summary>
    [JSImport("attach", CanvasModule)]
    internal static partial Task Attach(
        [JSMarshalAs<JSType.MemoryView>] ArraySegment<double> list,
        [JSMarshalAs<JSType.MemoryView>] ArraySegment<double> input,
        [JSMarshalAs<JSType.Array<JSType.Number>>] int[] tiles,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] ground,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] atlas,
        [JSMarshalAs<JSType.Array<JSType.Number>>] int[] kinds,
        [JSMarshalAs<JSType.Array<JSType.Number>>] int[] cover,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] verdicts,
        int mapWidth,
        int mapHeight,
        int tileSize,
        int backdrop);

    [JSImport("detach", CanvasModule)]
    internal static partial void Detach();

    /// <summary>
    /// Loads the module of the Three.js renderer from the site itself, once, when the garden starts. The module
    /// asks for Three.js itself when it attaches, and only where the browser has WebGL 2.
    /// </summary>
    internal static async Task ImportThreeAsync()
    {
        if (threeImported)
        {
            return;
        }

        await JSHost.ImportAsync(ThreeModule, "../js/render-three.js");
        threeImported = true;
    }

    /// <summary>
    /// Starts the Three.js renderer on the canvas of the page: the same arguments as <see cref="Attach"/>.
    /// </summary>
    /// <returns>False when the browser has no WebGL 2: nothing was started, and the canvas is still free.</returns>
    [JSImport("attach", ThreeModule)]
    [return: JSMarshalAs<JSType.Promise<JSType.Boolean>>]
    internal static partial Task<bool> AttachThree(
        [JSMarshalAs<JSType.MemoryView>] ArraySegment<double> list,
        [JSMarshalAs<JSType.MemoryView>] ArraySegment<double> input,
        [JSMarshalAs<JSType.Array<JSType.Number>>] int[] tiles,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] ground,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] atlas,
        [JSMarshalAs<JSType.Array<JSType.Number>>] int[] kinds,
        [JSMarshalAs<JSType.Array<JSType.Number>>] int[] cover,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] verdicts,
        int mapWidth,
        int mapHeight,
        int tileSize,
        int backdrop);

    [JSImport("detach", ThreeModule)]
    internal static partial void DetachThree();

    [JSImport("setEnabled", AudioModule)]
    internal static partial void SetSoundEnabled(bool enabled);

    [JSImport("play", AudioModule)]
    internal static partial bool PlayCue(string id);

    [JSImport("globalThis.localStorage.getItem")]
    internal static partial string? GetItem(string key);

    [JSImport("globalThis.localStorage.setItem")]
    internal static partial void SetItem(string key, string value);

    [JSImport("globalThis.localStorage.removeItem")]
    internal static partial void RemoveItem(string key);
}
