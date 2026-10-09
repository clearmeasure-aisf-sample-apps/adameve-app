namespace AdamEve.Content.Scripture;

/// <summary>One verse as parsed from the canonical text.</summary>
/// <param name="Ref">The reference of the verse.</param>
/// <param name="Text">
/// The text of the verse, character for character, without its marker; the line breaks of the file's wrapping are
/// single spaces.
/// </param>
public sealed record Verse(VerseRef Ref, string Text);
