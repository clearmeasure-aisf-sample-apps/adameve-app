using System.Runtime.InteropServices.JavaScript;
using AdamEve.Core.Saves;
using AdamEve.Core.World;

namespace AdamEve.Client.Game;

/// <summary>
/// The saved game and the settings in <c>localStorage</c>, and nowhere else (design, section 7.3): no account, no
/// cookie, no request. A browser that refuses storage plays without saving.
/// </summary>
public static class LocalStorageSaveStore
{
    /// <summary>
    /// Reads the saved game. One that cannot be read is moved to <see cref="SaveCodec.CorruptKey"/>.
    /// </summary>
    /// <param name="map">The map of the garden.</param>
    public static SaveReadResult Load(TileMap map)
    {
        var text = Read(SaveCodec.SaveKey);
        var result = SaveCodec.Read(text, map);
        if (result.State == SaveState.Corrupt && text is not null)
        {
            Write(SaveCodec.CorruptKey, text);
            Remove(SaveCodec.SaveKey);
        }

        return result;
    }

    /// <summary>Keeps the saved game.</summary>
    /// <param name="save">The saved game.</param>
    public static void Save(SaveGame save) => Write(SaveCodec.SaveKey, SaveCodec.Write(save));

    /// <summary>Reads the settings; the defaults when there are none.</summary>
    public static GameSettings LoadSettings() => SaveCodec.ReadSettings(Read(SaveCodec.SettingsKey));

    /// <summary>Keeps the settings.</summary>
    /// <param name="settings">The settings.</param>
    public static void SaveSettings(GameSettings settings) => Write(SaveCodec.SettingsKey, SaveCodec.WriteSettings(settings));

    private static string? Read(string key)
    {
        try
        {
            return GameInterop.GetItem(key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private static void Write(string key, string value)
    {
        try
        {
            GameInterop.SetItem(key, value);
        }
        catch (JSException)
        {
            // Storage is refused or full: the game goes on without saving.
        }
    }

    private static void Remove(string key)
    {
        try
        {
            GameInterop.RemoveItem(key);
        }
        catch (JSException)
        {
            // Storage is refused: nothing to remove.
        }
    }
}
