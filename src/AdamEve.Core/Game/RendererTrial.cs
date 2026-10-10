namespace AdamEve.Core.Game;

/// <summary>What draws the garden.</summary>
public enum RendererKind
{
    /// <summary>The canvas 2D renderer of the design (section 7.1): the default.</summary>
    Canvas,

    /// <summary>The Three.js (WebGL) renderer of the spike, loaded only when it is chosen.</summary>
    Three,
}

/// <summary>
/// The trial of a second renderer (docs/spike-threejs.md): which one a player asked for. The choice is asked for in
/// the address of the garden (<c>?renderer=three</c>) or in the settings, and is kept for the tab only: it is not
/// part of the saved game or of the kept settings.
/// </summary>
public static class RendererTrial
{
    /// <summary>The name of the choice in the query string of the garden's address.</summary>
    public const string QueryName = "renderer";

    /// <summary>The key of the choice in <c>sessionStorage</c>: it ends with the tab.</summary>
    public const string SessionKey = "adameve.trial.renderer";

    /// <summary>The name of a renderer, as the address, the storage and the page's <c>data-renderer</c> spell it.</summary>
    /// <param name="kind">The renderer.</param>
    public static string NameOf(RendererKind kind) => kind == RendererKind.Three ? "three" : "canvas";

    /// <summary>The renderer a name means; null for no name or one that names none.</summary>
    /// <param name="name">The name.</param>
    public static RendererKind? Parse(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "canvas" => RendererKind.Canvas,
        "three" => RendererKind.Three,
        _ => null,
    };

    /// <summary>The renderer the query string of an address asks for; null when it asks for none.</summary>
    /// <param name="address">An address, or only its query string.</param>
    public static RendererKind? FromAddress(string? address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return null;
        }

        var question = address.IndexOf('?', StringComparison.Ordinal);
        var query = question < 0 ? string.Empty : address[(question + 1)..];
        var fragment = query.IndexOf('#', StringComparison.Ordinal);
        if (fragment >= 0)
        {
            query = query[..fragment];
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0 && string.Equals(pair[..equals], QueryName, StringComparison.Ordinal))
            {
                return Parse(pair[(equals + 1)..]);
            }
        }

        return null;
    }

    /// <summary>
    /// The renderer to start with: what the address asks for, otherwise what the tab kept, otherwise the canvas.
    /// </summary>
    /// <param name="address">The address of the page.</param>
    /// <param name="kept">The text kept under <see cref="SessionKey"/>, or null.</param>
    public static RendererKind Choose(string? address, string? kept) =>
        FromAddress(address) ?? Parse(kept) ?? RendererKind.Canvas;
}
