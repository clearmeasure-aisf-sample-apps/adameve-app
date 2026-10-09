
namespace AdamEve.Client.Game;

/// <summary>
/// The sound adapter (design, section 5.4). Slice S2 ships the setting and this adapter and no audio file: no cue
/// exists yet, so <see cref="Play"/> plays nothing.
/// </summary>
public static class JsAudio
{
    /// <summary>Turns sound on or off.</summary>
    /// <param name="enabled">Whether sound plays.</param>
    public static void SetEnabled(bool enabled) => GameInterop.SetSoundEnabled(enabled);

    /// <summary>Plays a cue.</summary>
    /// <param name="cueId">The id of the cue.</param>
    /// <returns>Whether the cue exists and was started.</returns>
    public static bool Play(string cueId) => GameInterop.PlayCue(cueId);
}
