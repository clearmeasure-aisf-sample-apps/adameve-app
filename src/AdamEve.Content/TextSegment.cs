namespace AdamEve.Content;

/// <summary>A run of a verse's text: plain, or a glossary word with its entry.</summary>
/// <param name="Text">The characters of the verse this run covers, unchanged.</param>
/// <param name="Entry">The glossary entry of the run, or null for plain text.</param>
public sealed record TextSegment(string Text, GlossaryEntry? Entry);
