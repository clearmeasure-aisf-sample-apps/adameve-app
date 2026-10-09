using System.Globalization;

namespace AdamEve.Content.Scripture;

/// <summary>The reference of one verse of Genesis: chapter and verse, written <c>3:9</c> as the canonical text does.</summary>
/// <param name="Chapter">The chapter, from 1.</param>
/// <param name="Number">The verse within the chapter, from 1.</param>
public readonly record struct VerseRef(int Chapter, int Number)
{
    /// <summary>The reference as the canonical text marks it: chapter, a colon, verse.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Chapter}:{Number}");
}
