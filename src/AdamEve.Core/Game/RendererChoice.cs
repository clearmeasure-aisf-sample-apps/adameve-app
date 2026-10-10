namespace AdamEve.Core.Game;

/// <summary>What draws the garden.</summary>
public enum RendererKind
{
    /// <summary>
    /// The Three.js (WebGL) renderer, with the perspective camera: the renderer of the game since decision D18
    /// (design, sections 7.1 and 12).
    /// </summary>
    Three,

    /// <summary>
    /// The canvas 2D renderer, flat: the fallback where WebGL is not to be had or its context is lost for good.
    /// </summary>
    Canvas,
}

/// <summary>
/// Which renderer a visit to the garden starts with. It is Three.js; the address of the garden can force the
/// fallback (<c>?renderer=canvas</c>) for tests and for comparing the two. Nothing of it is kept: not in the saved
/// game, not in the settings, not for the tab.
/// </summary>
public static class RendererChoice
{
    /// <summary>The name of the choice in the query string of the garden's address.</summary>
    public const string QueryName = "renderer";

    /// <summary>Why the canvas draws when the address asked for it.</summary>
    public const string Asked = "asked";

    /// <summary>Why the canvas draws when the browser has no WebGL 2.</summary>
    public const string WebGlUnavailable = "webgl-unavailable";

    /// <summary>Why the canvas draws when the module of the Three.js renderer or Three.js itself could not be loaded.</summary>
    public const string LoadFailed = "load-failed";

    /// <summary>Why the canvas draws when the WebGL context was lost while playing and did not come back.</summary>
    public const string ContextLost = "webgl-context-lost";

    /// <summary>The name of a renderer, as the address and the page's <c>data-renderer</c> spell it.</summary>
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

    /// <summary>The renderer to start with: Three.js, unless the address asks for the canvas.</summary>
    /// <param name="address">The address of the page.</param>
    public static RendererKind Choose(string? address) => FromAddress(address) ?? RendererKind.Three;
}
