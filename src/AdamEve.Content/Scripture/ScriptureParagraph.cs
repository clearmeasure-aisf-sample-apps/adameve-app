namespace AdamEve.Content.Scripture;

/// <summary>One paragraph of the canonical text.</summary>
/// <param name="Lines">The lines of the paragraph, as written in the file.</param>
/// <param name="Refs">The references of the verses the paragraph holds, in order.</param>
public sealed record ScriptureParagraph(IReadOnlyList<string> Lines, IReadOnlyList<VerseRef> Refs);
