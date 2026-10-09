using System.Runtime.InteropServices.JavaScript;

namespace AdamEve.Client.Game;

/// <summary>
/// The whole border between the game and the browser (design, section 7.1): the canvas module calls
/// <see cref="Frame"/> once for each frame and reads the render list from the game's memory; the game calls the
/// browser only to start and stop the canvas, to set the sound, and to read and write <c>localStorage</c>.
/// </summary>
public static partial class GameInterop
{
    private const string RenderModule = "render";
    private const string AudioModule = "audio";

    private static bool imported;

    /// <summary>Whether the modules of the game are loaded: before that, nothing of them can be called.</summary>
    internal static bool Imported => imported;

    /// <summary>What a frame runs: the session that is playing, or nothing.</summary>
    internal static Action<double>? OnFrame { get; set; }

    /// <summary>One frame of the game. The canvas module calls it from <c>requestAnimationFrame</c>.</summary>
    /// <param name="nowMs">The time of the frame, as the browser gives it.</param>
    [JSExport]
    public static void Frame(double nowMs) => OnFrame?.Invoke(nowMs);

    /// <summary>Loads the two modules of the game from the site itself, once.</summary>
    internal static async Task ImportAsync()
    {
        if (imported)
        {
            return;
        }

        await JSHost.ImportAsync(RenderModule, "../js/render.js");
        await JSHost.ImportAsync(AudioModule, "../js/audio.js");
        imported = true;
    }

    [JSImport("attach", RenderModule)]
    internal static partial Task Attach(
        [JSMarshalAs<JSType.MemoryView>] ArraySegment<double> list,
        [JSMarshalAs<JSType.MemoryView>] ArraySegment<double> input,
        [JSMarshalAs<JSType.Array<JSType.Number>>] int[] tiles,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] ground,
        [JSMarshalAs<JSType.Array<JSType.Number>>] double[] atlas,
        [JSMarshalAs<JSType.Array<JSType.String>>] string[] verdicts,
        int mapWidth,
        int mapHeight,
        int tileSize,
        int backdrop);

    [JSImport("detach", RenderModule)]
    internal static partial void Detach();

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
