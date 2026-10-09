using System.Diagnostics.CodeAnalysis;

namespace AdamEve.Content;

/// <summary>What the start-up content check found.</summary>
/// <param name="Content">The content, when every check passed.</param>
/// <param name="Failure">What failed, for a log; a player is never shown it.</param>
public sealed record ContentLoadResult(GameContent? Content, string? Failure)
{
    /// <summary>Whether the content loaded and every check passed.</summary>
    [MemberNotNullWhen(true, nameof(Content))]
    public bool IsLoaded => Content is not null;
}
