using System.Text.Json;
using System.Text.Json.Serialization;
using AdamEve.Core.Story;
using AdamEve.Core.World;

namespace AdamEve.Core.Saves;

/// <summary>
/// Writes and reads the saved game and the settings as JSON, with source-generated serializers, and holds the
/// version of the format (design, section 7.3). It does no I/O: the client keeps the text in <c>localStorage</c>.
/// </summary>
public static class SaveCodec
{
    /// <summary>The version of the format this build writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The <c>localStorage</c> key of the saved game.</summary>
    public const string SaveKey = "adameve.save.v1";

    /// <summary>The <c>localStorage</c> key of the settings.</summary>
    public const string SettingsKey = "adameve.settings.v1";

    /// <summary>The <c>localStorage</c> key a saved game that cannot be read is moved to.</summary>
    public const string CorruptKey = "adameve.save.corrupt";

    /// <summary>The saved game as JSON.</summary>
    /// <param name="save">The saved game.</param>
    public static string Write(SaveGame save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return JsonSerializer.Serialize(save, SaveJsonContext.Default.SaveGame);
    }

    /// <summary>
    /// Reads a saved game. It is read only when it is JSON of a version this build knows, every choice in it is one
    /// of the fixed lists, its beat is one of its chapter, and its tile is one a character may stand on.
    /// </summary>
    /// <param name="json">The text kept under <see cref="SaveKey"/>; null or empty when there is none.</param>
    /// <param name="map">The map of the garden.</param>
    public static SaveReadResult Read(string? json, TileMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (string.IsNullOrEmpty(json))
        {
            return new SaveReadResult(SaveState.None, null);
        }

        try
        {
            var save = JsonSerializer.Deserialize(json, SaveJsonContext.Default.SaveGame);
            if (save is not null
                && save.SchemaVersion == CurrentVersion
                && Enum.IsDefined(save.Character)
                && Enum.IsDefined(save.Facing)
                && StoryMachine.IsBeatOf(save.Chapter, save.Beat)
                && map.IsWalkable(new TilePos(save.TileX, save.TileY)))
            {
                return new SaveReadResult(SaveState.Loaded, save);
            }
        }
        catch (JsonException)
        {
            // Not JSON, or not this format: the caller keeps the text aside.
        }

        return new SaveReadResult(SaveState.Corrupt, null);
    }

    /// <summary>The settings as JSON.</summary>
    /// <param name="settings">The settings.</param>
    public static string WriteSettings(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(settings, SaveJsonContext.Default.GameSettings);
    }

    /// <summary>Reads the settings. Settings that cannot be read are the defaults.</summary>
    /// <param name="json">The text kept under <see cref="SettingsKey"/>; null or empty when there is none.</param>
    public static GameSettings ReadSettings(string? json)
    {
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var settings = JsonSerializer.Deserialize(json, SaveJsonContext.Default.GameSettings);
                if (settings is not null && settings.SchemaVersion == CurrentVersion && Enum.IsDefined(settings.TextSize))
                {
                    return settings;
                }
            }
            catch (JsonException)
            {
                // Not JSON, or not this format: the defaults.
            }
        }

        return new GameSettings();
    }
}

[JsonSourceGenerationOptions(UseStringEnumConverter = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SaveGame))]
[JsonSerializable(typeof(GameSettings))]
internal sealed partial class SaveJsonContext : JsonSerializerContext;
