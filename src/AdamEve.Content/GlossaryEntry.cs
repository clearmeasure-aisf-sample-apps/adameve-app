namespace AdamEve.Content;

/// <summary>One word of the King James Version the game explains. The definition is game text, never Scripture.</summary>
/// <param name="Term">The word or words, spelled as the verses spell them.</param>
/// <param name="Definition">The short definition, written by the game and reviewed.</param>
public sealed record GlossaryEntry(string Term, string Definition);
